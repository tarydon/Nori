// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TopoMesh.cs
// ║║║║╬║╔╣║ TopoMesh represents a basic mesh for topology operations (only positions, no normals)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class TopoMesh -----------------------------------------------------------------------------
/// <summary>Represents a mesh made of triangles in 3D</summary>
/// The core data is a set of points (with no duplicates), and a set of integers that 
/// are indices into this set of points. These integers, taken 3 at a time, define the
/// triangles by their corners. We don't store any normals in this data structure
public class TopoMesh {
   // Constructors -------------------------------------------------------------
   /// <summary>Creates a TopoMesh from a list of points representing triangle corners</summary>
   /// This list of pts, taken 3 at a time, represent triangle vertices. The triangles are 
   /// de-duplicated using a tolerance of Delta
   public TopoMesh (IEnumerable<Point3f> input) {
      List<int> idx = []; List<Point3f> pts = []; 
      Dictionary<Point3f, int> map = new (Point3fComparer.Delta);
      foreach (var pt in input) {
         if (!map.TryGetValue (pt, out int n)) { map.Add (pt, n = pts.Count); pts.Add (pt); }
         idx.Add (n);
      }
      RemoveEmpty (idx);
      Pts = [.. pts]; Triangle = [.. idx];
   }

   /// <summary>Creates a TopoMesh from an indexed list of points</summary>
   /// <param name="input">A list of points</param>
   /// <param name="tris">Indices into these points, taken 3 at a time to define triangles</param>
   public TopoMesh (IList<Point3f> input, IEnumerable<int> tris) {
      List<int> idx = []; List<Point3f> pts = [];
      Dictionary<Point3f, int> map = new (Point3fComparer.Delta);
      foreach (var tri in tris) {
         Point3f pt = input[tri];
         if (!map.TryGetValue (pt, out int n)) { map.Add (pt, n = pts.Count); pts.Add (pt); }
         idx.Add (n);
      }
      RemoveEmpty (idx);
      Pts = [.. pts]; Triangle = [.. idx];
   }

   /// <summary>Create a TopoMesh by combining a set of meshes</summary>
   public TopoMesh (IEnumerable<TopoMesh> meshes) {
      List<Point3f> pts = []; List<int> idx = [];
      foreach (var mesh in meshes) {
         int n = pts.Count; pts.AddRange (mesh.Pts); 
         idx.AddRange (mesh.Triangle.Select (a => a + n));
      }
      Pts = [.. pts]; Triangle = [.. idx];
   }

   /// <summary>Creates a TopoMesh from a list of Point3 representing triangle corners</summary>
   public TopoMesh (IEnumerable<Point3> input) : this (input.Select (a => (Point3f)a)) { }

   /// <summary>Creates a TopoMesh given the core Pts and Index arrays (useful during serialization)</summary>
   public TopoMesh (ImmutableArray<Point3f> pts, ImmutableArray<int> index) => (Pts, Triangle) = (pts, index);

   // Properties ---------------------------------------------------------------
   /// <summary>The unique list of points</summary>
   public readonly ImmutableArray<Point3f> Pts;

   /// <summary>A descriptive tag that is attached with the TopoMesh</summary>
   public string? Tag;

   /// <summary>Indices into Pts representing triangles (taken 3 at time)</summary>
   public readonly ImmutableArray<int> Triangle;

   // Methods ------------------------------------------------------------------
   /// <summary>Loads a TopoMesh from a binary format (uncompressed)</summary>
   public static TopoMesh Load (Stream stm) {
      int sign = stm.ReadInt32 (), version = stm.ReadInt32 ();
      if (sign != SIGN || version != 1) throw new IOException ($"Not a TMSH file");
      Point3f[] pts = new Point3f[stm.ReadInt32 ()];
      int[] idx = new int[stm.ReadInt32 ()];
      string? tag = stm.ReadString ();
      stm.ReadExactly (MemoryMarshal.AsBytes (pts.AsSpan ()));
      if (pts.Length < 65535) {
         ushort[] uidx = new ushort[idx.Length];
         stm.ReadExactly (MemoryMarshal.AsBytes (uidx.AsSpan ()));
         for (int i = 0; i < idx.Length; i++) idx[i] = uidx[i];
      } else
         stm.ReadExactly (MemoryMarshal.AsBytes (idx.AsSpan ()));
      return new TopoMesh (pts.ToIArray (), idx.ToIArray ()) { Tag = tag };
   }

   /// <summary>Saves a TopoMesh file to a binary format (no compression here)</summary>
   public void Save (Stream stm) {
      stm.WriteInt32 (SIGN); stm.WriteInt32 (1);
      stm.WriteInt32 (Pts.Length).WriteInt32 (Triangle.Length);
      stm.WriteString (Tag);
      stm.Write (MemoryMarshal.AsBytes (Pts.AsSpan ()));
      if (Pts.Length < 65535)
         stm.Write (MemoryMarshal.AsBytes (Triangle.Select (a => (ushort)a).ToList ().AsSpan ()));
      else
         stm.Write (MemoryMarshal.AsBytes (Triangle.AsSpan ()));
   }
   const int SIGN = 'T' + ('M' << 8) + ('S' << 16) + ('H' << 24);

   /// <summary>Returns a new TopoMesh with T junctions removed</summary>
   /// They are removed by adding additional diagonals where needed
   public TopoMesh TJointsRemoved () => new TJointRemover (this).Process ();

   /// <summary>Converts a TopoMesh to a Mesh3 (typically for rendering)</summary>
   public Mesh3 ToMesh (bool wireframe) {
      List<Mesh3.Node> nodes = []; List<int> tris = [], wires = [];
      for (int i = 0; i < Triangle.Length; i += 3) {
         int n = nodes.Count;
         Point3 pa = (Point3)Pts[Triangle[i]], pb = (Point3)Pts[Triangle[i + 1]], pc = (Point3)Pts[Triangle[i + 2]];
         Vector3 vec = ((pb - pa) * (pc - pa)).Normalized ();
         nodes.Add (new (pa, vec)); nodes.Add (new (pb, vec)); nodes.Add (new (pc, vec));
         for (int j = 0; j < 3; j++) { 
            tris.Add (n + j);
            if (wireframe) { wires.Add (n + j); wires.Add (n + (j + 1) % 3); }
         }
      }
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Multiply a TopoMesh by a given transform</summary>
   public static TopoMesh operator * (TopoMesh mesh, Matrix3 xfm) {
      if (xfm.IsIdentity) return mesh;
      ImmutableArray<Point3f> pts = [.. mesh.Pts.Select (a => a * xfm)];
      return new (pts, mesh.Triangle);
   }

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"TopoMesh {Pts.Length} pts, {Triangle.Length / 3} tris";

   void RemoveEmpty (List<int> idx) {
      for (int i = idx.Count - 3; i >= 0; i -= 3) {
         int a = idx[i], b = idx[i + 1], c = idx[i + 2];
         if (a == b || b == c || a == c) idx.RemoveRange (i, 3);
      }
   }
}
#endregion

#region class TJointRemover ------------------------------------------------------------------------
/// <summary>Algorithm to remove T-Joints from a TopoMesh</summary>
class TJointRemover {
   // Constructors -------------------------------------------------------------
   /// <summary>Constructs a T-Joint remover given a TopoMesh to work with</summary>
   public TJointRemover (TopoMesh tm) => mMesh = tm;
   readonly TopoMesh mMesh;

   // Methods ------------------------------------------------------------------
   /// <summary>Processes and returns an updated mesh</summary>
   /// If the input mesh has no T joints, this returns the same mesh unchanged
   public TopoMesh Process () {
      GatherFreeEdges ();
      for (int i = 0; i < mFreeEdges.Count; i++)
         ProcessFreeEdge (i);
      if (Index == null) return mMesh;
      return new TopoMesh (mMesh.Pts, [.. Index]);
   }

   // Implementation -----------------------------------------------------------
   void GatherFreeEdges () {
      // In this loop, we walk through each edge of the triangle mesh, and check if we have
      // already seen its 'reverse edge' - the paired edge going the other way (we use 
      // start:end packed into a 64-bit value as the key). In a perfect manifold mesh, each
      // edge a..b will have a corresponding edge b..a. When we see the first of this 'pair',
      // it gets added to mFreeEdgeMap and when we see the second one, it is removed. 
      // Finally, mFreeEdgeMap will contain only the unpaired edges.
      var index = mMesh.Triangle;
      for (int i = 0; i < index.Length; i += 3) {
         for (int j = 0; j < 3; j++) {
            ulong a = (ulong)index[i + j], b = (ulong)index[i + (j + 1) % 3];
            if (!mFreeEdgeMap.Remove ((a << 32) + b))
               mFreeEdgeMap.Add ((b << 32) + a, i + j);
         }
      }

      // Create an ordered list of all the free edges, longest first. 
      var pts = mMesh.Pts;
      foreach (var e in mFreeEdgeMap.Keys) {
         int a = (int)(e >> 32), b = (int)(e & 0xffffffff);
         double length = pts[a].DistToSq (pts[b]);
         mFreeEdges.Add ((a, b, (float)length)); 
      }
      mFreeEdges.Sort ((a, b) => b.Length.CompareTo (a.Length));
   }
   // This maps each edge (stored as a ulong encoding StartIndex..EndIndex in 64 bits,
   // to an Index (the edge is the one starting at this location - it is found in the
   // triangle number Index/3). This contains only unpaired edges - ones with no corresponding
   // opposite-direction co-edge. 
   Dictionary<ulong, int> mFreeEdgeMap = [];
   // A list of all unpaired edges - each edge is represented by the start and end vertex
   // numbers, and the length of the edge
   List<(int A, int B, float Length)> mFreeEdges = [];

   // This takes a long edge connecting two points A and B (say this is part of a triangle ABC),
   // and tries to find a path using shorter edges (say A..D, D..E and E..B that also make the
   // same connection. Then, if all the intermediate nodes of these shorter edge set (in this
   // case, D and E) lie ON the long edge A..B then there are two T joints (at D and E).
   // Both of these can be fixed by drawing diagonals from D and E to the other vertex of the 
   // original triangle. That is, we add the diagonals D..C and E..C dividing the original triangle
   // ABC into 3 triangles: ADC, DEC, EBC, thus removing both T joints. 
   void ProcessFreeEdge (int nEdge) {
      var (start, end, _) = mFreeEdges[nEdge];
      if (FindAlternatePath (end, start, nEdge)) {
         // At this point, we've found an alternate path from start to end (and the intermediate
         // nodes in that are stored in mPathNodes). If all the nodes in that lie on the original
         // line, then we have multiple T junctions on that line 
         var pts = mMesh.Pts;
         Point3f pa = pts[start], pb = pts[end];
         Index ??= [.. mMesh.Triangle];
         int target = mFreeEdgeMap[((ulong)start << 32) + (ulong)end];
         // triStart is the position in the Index array of the 3 nodes making up the
         // triangle we are going to split, and oppositeNode is the other vertex to which we
         // are going to draw diagonals from the T-junction points. The value stored in the
         // mFreeEdgeMap is the vertex that 'starts' this long edge (going to be split). 
         // - (target % 3) is the position within this triangle of that start vertex (0,1,2)
         // - ((target + 1) % 3) is the position of the end vertex within that triangle (1,2,0)
         // - ((target + 2) % 3) is therefore the position of the 'opposite' vertex to which 
         //   we have to draw diagonals
         bool first = true; 
         int triStart = 3 * (target / 3), oppositeNode = Index[triStart + (target + 2) % 3];
         mPathNodes.Add (start);    // Simplifies logic below
         foreach (var node in mPathNodes) {
            double dist = mMesh.Pts[node].DistToLineSq (pa, pb);
            if (!dist.IsZero (1e-5)) continue;
            if (first) {
               // If this is the first diagonal we are adding, we can overwrite the large triangle
               // with a new smaller one. Otherwise, we need to add a new triangle
               Index[triStart] = end; Index[triStart + 1] = node; Index[triStart + 2] = oppositeNode;
               first = false;
            } else {
               Index.Add (end); Index.Add (node); Index.Add (oppositeNode);
            }
            end = node;
         }

         // Remove the edges we have used up (from alternate path)
         mPathEdges.OrderDescending ().ForEach (mFreeEdges.RemoveAt);
      }
   }
   List<int> mPathEdges = [], mPathNodes = [];
   List<int>? Index;

   // Given a start and end point of a _long_ edge (these are the two endpoints of the
   // edge stored at nEdgeIndex in the mFreeEdges list), this routine tries to find
   // an alternate path between start and end. 
   // - The path should be made up of free (unpaired) edges
   // - We can only use edges shorter than nEdgeIndex (this is ensured by just taking
   //   the edges after nEdgeIndex in the array, since we've already sorted it by decreasing
   //   length)
   bool FindAlternatePath (int start, int end, int nEdgeIndex) {
      mPathNodes.Clear (); 
      mPathEdges.Clear (); 
      for (; ; ) {
         int oldStart = start;
         for (int i = nEdgeIndex + 1; i < mFreeEdges.Count; i++) {
            var (s, e, _) = mFreeEdges[i];
            if (s != start || mPathEdges.Contains (i)) continue; 
            mPathEdges.Add (i);
            if ((start = e) == end) return true; 
            mPathNodes.Add (start);
         }
         // If we were not able to find any (unused) edge leading out from start, 
         // we are at a dead end and cannot find a path, return
         if (start == oldStart) return false;
      }
   }
}
#endregion
