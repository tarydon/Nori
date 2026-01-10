namespace Nori;

#region class PlaneMeshIntersector -----------------------------------------------------------------
/// <summary>Provides plane/mesh intersection functionality for a specific mesh.</summary>
/// This class computes polygonal intersection loops between a mesh and a plane.
/// Create an instance with a mesh collection and call `Compute` repeatedly to intersect
/// multiple planes against the same mesh set.
public class PlaneMeshIntersector (IEnumerable<Mesh3> meshes) {
   /// <summary>Computes all mesh/plane intersection polylines for the configured mesh set.</summary>
   /// For each input mesh whose bound intersects the plane:
   /// - Reuses a per-instance distance buffer sized to the mesh's vertex count.
   /// - Computes signed distances of all mesh vertices to the plane to detect crossing edges.
   /// - Walks all triangles; for each edge whose endpoints lie on opposite sides of the plane,
   ///   creates or reuses an interpolated intersection point and records adjacency between the
   ///   two intersection points produced in that triangle.
   /// - Traverses the resulting point-adjacency graph to extract raw intersection chains.
   /// - Merges chains whose endpoints coincide within a small tolerance, and discards degenerate
   ///   or zero-length results.
   /// - Converts the final chains into immutable Polyline3 instances and returns them.
   public List<Polyline3> Compute (PlaneDef plane) {
      Vector3 n = plane.Normal; double d = plane.D;
      ptMap.Clear (); mOutChains.Clear ();

      foreach (var mesh in meshes) {
         if (!Intersects (mesh.Bound, n, d)) continue;

         mVtx = mesh.Vertex;
         EnsureDistCapacity (mVtx.Length);
         ComputeDistances (n, d);

         ResetWorkBuffers ();
         BuildAdjacency (mesh.Triangle);
         PrepareVisited (mRaw.Count);
         WalkAllChains ();
      }

      return MergePolylines ();
   }

   // Makes sure mDist has enough capacity.
   void EnsureDistCapacity (int count) {
      if (mDist.Length < count)
         mDist = new double[count];
   }

   // Merges open polylines by matching endpoints and returns final polylines.
   List<Polyline3> MergePolylines () {
      List<Polyline3> polylines = [];
      RemoveClosedLoops (polylines);

      // If only one chain remains, return it directly
      if (mOutChains.Count == 1) {
         BuildPolyline (mOutChains[0], polylines);
         return polylines;
      }

      PrepareEndpointMap ();
      MergeOpenChains (polylines);

      return polylines;
   }

   // Removes closed loops from mOutChains and appends them as polylines.
   void RemoveClosedLoops (List<Polyline3> polylines) {
      for (int i = mOutChains.Count - 1; i >= 0; i--) {
         var chain = mOutChains[i];
         if (chain.Count > 0 && chain[0].EQ (chain[^1], 1e-3f)) {
            BuildPolyline (chain, polylines);
            mOutChains.RemoveAt (i);
         }
      }
   }

   // Builds the endpoint map from current chains in mOutChains.
   void PrepareEndpointMap () {
      PrepareVisited (mOutChains.Count);     // for tracking merged chains

      for (int i = 0; i < mOutChains.Count; i++) {
         var chain = mOutChains[i];
         if (chain.Count == 0) continue;
         AddToPtMap (chain[0], (i, false));  // head
         AddToPtMap (chain[^1], (i, true));  // tail
      }
   }

   // Merges open chains using the endpoint map and appends final polylines.
   void MergeOpenChains (List<Polyline3> polylines) {
      for (int i = 0; i < mOutChains.Count; i++) {
         if (mVisited[i]) continue;          // already merged

         mVisited[i] = true; var chain = mOutChains[i];

         TraverseAndMerge (chain, false);    // traverse forward
         TraverseAndMerge (chain, true);     // traverse backward

         BuildPolyline (chain, polylines);   // convert to Polyline3
      }
   }

   // Adds points to endpoint map for merging chains.
   void AddToPtMap (Point3f pt, (int idx, bool isEnd) val) {
      if (ptMap.TryGetValue (pt, out var lst))
         lst.Add (val);          // append to existing list
      else ptMap[pt] = [val];    // create new list
   }

   // Travels through the endpoint map to merge chains into the given chain.
   void TraverseAndMerge (List<Point3f> chain, bool fromHead) {
      if (chain.Count == 0) return;

      // If fromHead is true, we are traverse backward.
      Point3f pt = fromHead ? chain[0] : chain[^1];

      // Keep traversing while we can find unvisited chains to attach.
      while (ptMap.TryGetValue (pt, out var lst)) {
         if (lst.Count > 2) break;     // ambiguous case, stop here

         int nIdx = -1; bool isEnd = false;
         for (int i = 0; i < lst.Count; i++) {
            var item = lst[i];
            if (!mVisited[item.idx]) { nIdx = item.idx; isEnd = item.isEnd; break; }
         }
         if (nIdx < 0) break;          // no unvisited chain found

         var nChain = mOutChains[nIdx]; int nCount = nChain.Count; mVisited[nIdx] = true;

         if (fromHead) {   // Backward direction: attach at chain start
            if (isEnd) for (int j = nCount - 2; j >= 0; j--) chain.Insert (0, nChain[j]);
            else for (int j = 1; j < nCount; j++) chain.Insert (0, nChain[j]);
            pt = chain[0];
         } else {          // Forward direction: attach at chain end
            if (isEnd) for (int j = nCount - 2; j >= 0; j--) chain.Add (nChain[j]);
            else for (int j = 1; j < nCount; j++) chain.Add (nChain[j]);
            pt = chain[^1];
         }
      }
   }

   // Quick plane-vs-mesh test; returns false if the bound lies strictly on one side.
   bool Intersects (Bound3 bound, Vector3 n, double d) {
      if (bound.IsEmpty) return false;

      // Center (Mid) and half extents (Length/2) per axis.
      double cx = bound.X.Mid, cy = bound.Y.Mid, cz = bound.Z.Mid;
      double ex = bound.X.Length * 0.5, ey = bound.Y.Length * 0.5, ez = bound.Z.Length * 0.5;

      // Signed distance from bound center to plane: dot(n, c) + d.
      double dist = n.X * cx + n.Y * cy + n.Z * cz + d;

      // Project the bound extents onto the plane normal to get the max reach.
      double r = Math.Abs (n.X) * ex + Math.Abs (n.Y) * ey + Math.Abs (n.Z) * ez;

      // If center distance exceeds projected radius, plane cannot hit the mesh.
      return Math.Abs (dist) <= r;
   }

   // Computes signed distances of all mesh vertices to the plane.
   void ComputeDistances (Vector3 n, double dBias) {
      for (int i = 0; i < mVtx.Length; i++) {
         // Signed distance to plane: dot(n, p) + d.
         var p = mVtx[i].Pos;
         double v = n.X * p.X + n.Y * p.Y + n.Z * p.Z + dBias;
         // A tiny bias to avoid ambiguous "on plane" classification
         if (Math.Abs (v) < 1e-10) v += 1e-8; mDist[i] = v;
      }
   }

   // Clears all per-call working collections.
   void ResetWorkBuffers () {
      mNbr1.Clear (); mNbr2.Clear ();
      mRaw.Clear (); mEdgeMap.Clear ();
   }

   // Builds the adjacency lists between plane-edge intersection points produced from triangles.
   void BuildAdjacency (ImmutableArray<int> mtri) {
      for (int t = 0; t < mtri.Length; t += 3) {
         int a = mtri[t], b = mtri[t + 1], c = mtri[t + 2];
         double da = mDist[a], db = mDist[b], dc = mDist[c];

         int p1 = -1, p2 = -1;

         // For each triangle edge, a sign change means the edge crosses the plane and yields one
         // intersection point (created once per mesh edge via mEdgeMap).
         if (da * db < 0) p1 = GetOrAddEdgePoint (a, b, da, db);
         if (db * dc < 0) { int p = GetOrAddEdgePoint (b, c, db, dc); if (p1 == -1) p1 = p; else p2 = p; }
         if (dc * da < 0) { int p = GetOrAddEdgePoint (c, a, dc, da); if (p1 == -1) p1 = p; else p2 = p; }

         // A plane cutting a triangle produces exactly 2 points.
         // When we have 2, add those as neighbours to each other.
         if (p1 != -1 && p2 != -1) Link (p1, p2);
      }
   }

   // Adds a bidirectional link between two intersection points in the per-point adjacency lists.
   void Link (int a, int b) {
      if (mNbr1[a] == -1) mNbr1[a] = b; else mNbr2[a] = b;
      if (mNbr1[b] == -1) mNbr1[b] = a; else mNbr2[b] = a;
   }

   // Ensures the visited array is large enough and clears it for the current intersection run.
   void PrepareVisited (int count) {
      if (mVisited.Length < count) mVisited = new bool[count];
      else Array.Clear (mVisited, 0, count);
   }

   // Walks all unvisited intersection points to extract all polylines/chains for this plane cut.
   void WalkAllChains () {
      for (int i = 0; i < mRaw.Count; i++) {
         if (mVisited[i]) continue;
         mVisited[i] = true;

         List<Point3f> pts = GetChainList ();
         pts.Add (mRaw[i]);

         // Walk forward and backward to collect all points in the chain.
         Traverse (i, mNbr1[i], false, pts);
         Traverse (i, mNbr2[i], true, pts);

         mOutChains.Add (pts);
      }
   }

   // Converts extracted chains into Polyline3 and returns pooled lists back to the pool.
   void BuildPolyline (List<Point3f> pts, List<Polyline3> polylines) {
      int nPts = pts.Count;

      // Discard degenerate polylines
      if (nPts < 2) { ReturnChainList (pts); return; }

      // Check if all points are identical
      var prev = pts[0]; bool hasLen = false;
      for (int j = 1; j < nPts; j++) {
         var p = pts[j];
         // Break if we find a point that is not equal to previous
         if (!p.EQ (prev, 1e-3f)) { hasLen = true; break; }
         prev = p;
      }
      if (!hasLen) { ReturnChainList (pts); return; }

      // Convert to Point3 array
      var dst = new Point3[nPts];
      for (int j = 0; j < nPts; j++) dst[j] = (Point3)pts[j];

      pts.Clear ();           // Clear before returning to pool
      ReturnChainList (pts);  // Return to pool

      polylines.Add (new Polyline3 (0, ImmutableArray.Create (dst)));
   }

   // Traverses a polyline in one direction from a start point, optionally prepending points to preserve order.
   void Traverse (int from, int nextIdx, bool prepend, List<Point3f> pts) {
      int prev = from, curr = nextIdx;

      // Follow links until the chain ends (-1) or point visited.
      while (curr != -1 && !mVisited[curr]) {
         if (prepend) pts.Insert (0, mRaw[curr]);
         else pts.Add (mRaw[curr]);
         mVisited[curr] = true;

         // Pick the neighbour that is not the node we came from.
         int n1 = mNbr1[curr], n2 = mNbr2[curr];
         int next = n1 != prev ? n1 : n2;
         prev = curr; curr = next;
      }
   }

   // Gets an existing intersection point or creates it.
   int GetOrAddEdgePoint (int a, int b, double da, double db) {
      // Normalize edge key so (a,b) and (b,a) map to same point.
      var key = a < b ? (a, b) : (b, a);
      if (mEdgeMap.TryGetValue (key, out int idx)) return idx;

      // Interpolate along the edge using signed distances: t = da / (da - db).
      idx = mRaw.Count;
      mRaw.Add ((da / (da - db)).Along (mVtx[a].Pos, mVtx[b].Pos));

      // Start with no neighbours; BuildAdjacency/Link will fill these.
      mNbr1.Add (-1); mNbr2.Add (-1); mEdgeMap[key] = idx;
      return idx;
   }

   // List<Point3f> pool for chains
   List<Point3f> GetChainList () {
      if (mChainPool.Count == 0) return [];

      var lst = mChainPool[^1];  // reuse last list
      mChainPool.RemoveAt (mChainPool.Count - 1);
      return lst;
   }

   // Returns a chain list back to the pool
   void ReturnChainList (List<Point3f> pts) => mChainPool.Add (pts);

   // Per-instance working storage reused between Compute calls
   double[] mDist = [];
   ImmutableArray<Mesh3.Node> mVtx;
   readonly Dictionary<(int, int), int> mEdgeMap = [];
   readonly Dictionary<Point3f, List<(int idx, bool isEnd)>> ptMap = new (Point3fComparer.Delta);
   readonly List<int> mNbr1 = [], mNbr2 = [];   // Neighbours of intersection points at each index
   readonly List<Point3f> mRaw = [];            // interpolated intersection points (Point3f)
   bool[] mVisited = [];

   // Reuse temporary lists to reduce GC pressure
   readonly List<List<Point3f>> mOutChains = [];
   readonly List<List<Point3f>> mChainPool = [];
}
#endregion PlaneMeshIntersector
