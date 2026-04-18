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
   public void Check () {
      Dictionary<uint, int> edges = [];
      for (int i = 0; i < Index.Length; i += 3) {
         for (int j = 0; j < 3; j++) {
            int a = Index[i + j], b = Index[i + (j + 1) % 3];
            if (a > b) (a, b) = (b, a);
            uint key = ((uint)a << 16) + (uint)b;
            if (!edges.TryAdd (key, i + j)) edges.Remove (key);
         }
      }

      // Gather the edges that are not paired (non-manifold edges), and sort
      // them by descending order of length
      List<(int A, int B)> unpaired = [];
      foreach (var e in edges.Keys) {
         int a = (int)(e >> 16), b = (int)(e & 0xffff);
         unpaired.Add ((a, b));
      }
      unpaired.OrderByDescending (tup => Pts[tup.A].DistToSq (Pts[tup.B]));

      List<int> loopEdges = [], loopNodes = [];
      for (int i = 0; i < unpaired.Count; i++) {
         loopNodes.Clear (); loopEdges.Clear (); loopEdges.Add (i);
         var (start, end) = unpaired[i];
         if (FindLoop (start, end, i + 1)) {
            foreach (var fid in loopEdges.Skip (1).OrderDescending ()) unpaired.RemoveAt (fid);
            string s = loopEdges.ToCSV ();
            string s2 = loopNodes.ToCSV ();
            Console.Write ("X");
         }
      }

      bool FindLoop (int s, int e, int start) {
         for (; ; ) {
            int sOld = s;
            for (int i = start; i < unpaired.Count; i++) {
               var (s1, e1) = unpaired[i];
               if (s1 != s && e1 != s || loopEdges.Contains (i)) continue;
               s = (s1 + e1) - s;
               loopEdges.Add (i);
               if (e == s) return true;
               loopNodes.Add (s);
            }
            if (s == sOld) return false;
         }
      }
   }
}

class TJointRemover {
   public TJointRemover (TopoMesh tm) => mMesh = tm;
   readonly TopoMesh mMesh;

   public void Process () {
      GatherFreeEdges ();
      for (int i = 0; i < mFreeEdgeMap.Count; i++)
         ProcessFreeEdge (i);
   }

   void GatherFreeEdges () {
      // In this loop, we walk through each edge of the triangle mesh, and try to add 
      // it as a key in mFreeEdgeMap (we use as the value the Index number that starts this edge). 
      // If the add succeeds, this is the first time we've seen that edge. If the add fails, it's
      // the second time we see this edge and we remove that key. Finally, the only keys left in the
      // map will be the edges that have no paired edge - that is, edges not shared between two
      // triangles. 
      var index = mMesh.Index;
      for (int i = 0; i < index.Length; i += 3) {
         for (int j = 0; j < 3; j++) {
            int a = index[i + j], b = index[i + (j + 1) % 3];
            if (a > b) (a, b) = (b, a);
            ulong key = ((ulong)a << 32) + (ulong)b;
            if (!mFreeEdgeMap.TryAdd (key, i + j)) mFreeEdgeMap.Remove (key);
         }
      }

      var pts = mMesh.Pts;
      foreach (var e in mFreeEdgeMap.Keys) {
         int a = (int)(e >> 32), b = (int)(e & 0xffffffff);
         double length = pts[a].DistToSq (pts[b]);
         mFreeEdges.Add ((a, b, (float)length); 
      }
      mFreeEdges.OrderByDescending (a => a.Length);
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
      if (FindAlternatePath (start, end, nEdge)) {
         // At this point, the 
         

      }
   }
   List<int> mPathEdges = [], mPathNodes = [];

   // Given a start and end point of a _long_ edge (these are the two endpoints of the
   // edge stored at nEdgeIndex in the mFreeEdges list), this routine tries to find
   // an alternate path between start and end. 
   // - The path should be made up of free (unpaired) edges
   // - We can only use edges shorter than nEdgeIndex (this is ensured by just taking
   //   the edges after nEdgeIndex in the array, since we've already sorted it by decreasing
   //   length)
   bool FindAlternatePath (int start, int end, int nEdgeIndex) {
      mPathNodes.Clear (); 
      mPathEdges.Clear (); mPathEdges.Add (nEdgeIndex);
      for (; ; ) {
         int oldStart = start;
         for (int i = nEdgeIndex + 1; i < mFreeEdges.Count; i++) {
            var (s, e, _) = mFreeEdges[i];
            if (s != start && e != start || mPathEdges.Contains (i)) continue;
            mPathEdges.Add (i);
            start = (s + e) - start;
            if (start == end) return true;   // Found a path all the way to the end!
            mPathNodes.Add (start);
         }
         // If we were not able to find any (unused) edge leading out from start, 
         // we are at a dead end and cannot find a path, return
         if (start == oldStart) return false;
      }
   }
}
