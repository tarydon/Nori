// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TopoMesh.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

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
      Pts = [.. pts]; Index = [.. idx];
   }

   public TopoMesh (ImmutableArray<Point3f> pts, ImmutableArray<int> index)
      => (Pts, Index) = (pts, index);

   /// <summary>
   /// Creates a TopoMesh from a list of points representing triangle corners
   /// </summary>
   public TopoMesh (IEnumerable<Point3> input) {
      List<int> idx = []; List<Point3f> pts = [];
      Dictionary<Point3f, int> map = new (Point3fComparer.Delta);
      foreach (var pt0 in input) {
         Point3f pt = (Point3f)pt0;
         if (!map.TryGetValue (pt, out int n)) { map.Add (pt, n = pts.Count); pts.Add (pt); }
         idx.Add (n);
      }
      RemoveEmpty (idx);
      Pts = [.. pts]; Index = [.. idx];
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
      Pts = [.. pts]; Index = [.. idx];
   }

   void RemoveEmpty (List<int> idx) {
      for (int i = idx.Count - 3; i >= 0; i -= 3) {
         int a = idx[i], b = idx[i + 1], c = idx[i + 2];
         if (a == b || b == c || a == c) idx.RemoveRange (i, 3); 
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The unique list of points</summary>
   public readonly ImmutableArray<Point3f> Pts;

   /// <summary>Indices into Pts representing triangles (taken 3 at time)</summary>
   public readonly ImmutableArray<int> Index;

   // Methods ------------------------------------------------------------------
   public TopoMesh RemoveTJoints () {
      return new TJointRemover (this).Process ();
   }
}

class TJointRemover {
   public TJointRemover (TopoMesh tm) => mMesh = tm;
   readonly TopoMesh mMesh;

   public TopoMesh Process () {
      GatherFreeEdges ();
      for (int i = 0; i < mFreeEdges.Count; i++)
         ProcessFreeEdge (i);
      if (Index == null) return mMesh;
      return new TopoMesh (mMesh.Pts, [.. Index]);
   }

   void GatherFreeEdges () {
      // In this loop, we walk through each edge of the triangle mesh, and check if we have
      // already seen its 'reverse edge' - the paired edge going the other way (we use 
      // start:end packed into a 64-bit value as the key). In a perfect manifold mesh, each
      // edge a..b will have a corresponding edge b..a. When we see the first of this 'pair',
      // it gets added to mFreeEdgeMap and when we see the second one, it is removed. 
      // Finally, mFreeEdgeMap will contain only the unpaired edges.
      var index = mMesh.Index;
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
         Index ??= [.. mMesh.Index];
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
      mPathNodes.Clear (); mPathEdges.Clear (); 
      for (; ; ) {
         int oldStart = start;
         for (int i = nEdgeIndex + 1; i < mFreeEdges.Count; i++) {
            var (s, e, _) = mFreeEdges[i];
            if (s != start) continue; 
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
