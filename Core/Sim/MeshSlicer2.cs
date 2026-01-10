namespace Nori;
using static Math;

public class MeshSlicer {
   // Constructor --------------------------------------------------------------
   public MeshSlicer (ImmutableArray<Mesh3> meshes) => mMeshes = meshes;

   // Methods ------------------------------------------------------------------
   public List<Polyline3> Compute (PlaneDef def) {     
      mAbsNormal = (mDef = def).Normal.Abs ();
      mPtMap.Clear (); mOutChains.Clear ();

      foreach (var mesh in mMeshes) {
         if (!PrepMesh (mesh)) continue;  // Quick skip meshes that cannot possibly intersect

         ResetWorkBuffers ();
         BuildAdjacency (mesh.Triangle);
         BuildAdjacencyNew (mesh.Triangle);
         PrepareVisited (mRaw.Count);
         WalkAllChains ();
         WalkAllChainsNew ();
      }
      return MergePolylines ();
   }
   PlaneDef mDef;                         // The PlaneDef we're working with
   Vector3 mAbsNormal;                    // The absolute value of the plane normal   
   ImmutableArray<Mesh3> mMeshes;         // List of meshes to test against
   ImmutableArray<Mesh3.Node> mVertex;    // List of vertices of the current mesh
   double[] mDist = [];                   // Signed distances of these nodes from the plane
   Dictionary<long, int> mEdgeMap2 = []; 

   struct Node {
      public Node (Point3f pt) { Pt = pt; Links = 0; Visited = false; }
      public Point3f Pt;
      // Each Node could be linked with 0, 1 and 2 nodes. 
      // Links = 0 : not connected to any nodes
      // Links < 0 : this node is connected to the node at -Links
      // Links > 0 : this node is connected to nodes A and B, such that A+B = Links
      // Note that this last case (where we store both the links in one integer value)
      // does not allow us to retrieve either of the links. However, given ONE link C,
      // we can get the other link as Links-C.
      public int Links;
      public bool Visited;
   }
   Node[] mNodes = new Node[8];
   int mUsedNodes = 0;

   // Implementation -----------------------------------------------------------
   // Prepares for testing one of the meshes. If the mesh completely misses the plane
   // (being fully on one side of it), this returns false. Otherwise, it returns true,
   // and computes the signed distances of each of the nodes from the plane
   bool PrepMesh (Mesh3 mesh) {
      var bound = mesh.Bound;
      if (bound.IsEmpty) return false;

      // Project the bound extents onto the plane normal to get the maximum reach, and
      // if that is less than the center distance, the Mesh3 misses the plane
      Vector3 halfDiag = bound.DiagVector * 0.5;
      double radius = halfDiag.Dot (mAbsNormal);
      // If the center distance exceeds the projected radius, the plane cannot hit
      if (mDef.Dist (bound.Midpoint) > radius) return false;

      // If we get this far, we have to test this plane, so prepare the mDist array
      int n = (mVertex = mesh.Vertex).Length;
      if (mDist.Length < n) mDist = new double[n];
      for (int i = 0; i < n; i++) {
         // When storing the distances, avoid values that are basically 'zero' by 
         // introducing a small bias. This effectively shifts the plane of intersection
         // by a small distance
         double dist = mDef.SignedDist (mVertex[i].Pos);
         if (Abs (dist) < 1e-10) dist += 1e-8;
         mDist[i] = dist;
      }

      // Reset some intermediate data structures we're going to use (note that
      // mNodes[0] is not used)
      mUsedNodes = 1; mEdgeMap2.Clear ();
      return true; 
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
      if (mPtMap.TryGetValue (pt, out var lst))
         lst.Add (val);          // append to existing list
      else mPtMap[pt] = [val];    // create new list
   }

   // Travels through the endpoint map to merge chains into the given chain.
   void TraverseAndMerge (List<Point3f> chain, bool fromHead) {
      if (chain.Count == 0) return;

      // If fromHead is true, we are traverse backward.
      Point3f pt = fromHead ? chain[0] : chain[^1];

      // Keep traversing while we can find unvisited chains to attach.
      while (mPtMap.TryGetValue (pt, out var lst)) {
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

   // Clears all per-call working collections.
   void ResetWorkBuffers () {
      mNbr1.Clear (); mNbr2.Clear ();
      mRaw.Clear (); mEdgeMap.Clear ();
   }

   void BuildAdjacencyNew (ImmutableArray<int> tri) {
      for (int i = 0; i < tri.Length; i += 3) {
         int a = tri[i], b = tri[i + 1], c = tri[i + 2];
         double da = mDist[a], db = mDist[b], dc = mDist[c];

         // Of the three conditions below, either all will be false (meaning all the three
         // nodes of the triangle are on one side of the plane), or exactly two will be true
         // (one node on one side, the other two on the other side). We've already eliminated 
         // the possibility that any of these nodes are ON the plane (by the bias we introduce
         // when computing mDist[]
         int n1 = 0, n2 = 0;
         if (da * db < 0) n1 = GetNodeNew (a, b, da, db);
         if (db * dc < 0) { n2 = n1; n1 = GetNodeNew (b, c, db, dc); }
         if (dc * da < 0) { n2 = n1; n1 = GetNodeNew (c, a, dc, da); }

         // Here, either n1 & n2 are both zero, or they are both non-zero (two intersections
         // found among the 3 edges of this triangle). 
         if (n1 != 0) {
            // Link the nodes n1 & n2 so they are connected to each other. 
            ref Node node1 = ref mNodes[n1];
            if (node1.Links == 0) node1.Links = -n2;
            else if (node1.Links < 0) node1.Links = n2 - node1.Links;
            else throw new InvalidOperationException ();
            ref Node node2 = ref mNodes[n2];
            if (node2.Links == 0) node2.Links = -n1;
            else if (node2.Links < 0) node2.Links = n1 - node2.Links;
            else throw new InvalidOperationException ();
         }
      }
   }

   // Gets the node on the edge between a..b
   int GetNodeNew (int a, int b, double da, double db) {
      // Normalize the edge so that edges a..b and b..a both map to the
      // same key value (the key is just a 64-bit integer with the two values a and b
      // packed into it)
      long key = a < b ? (((long)a) << 32) + b : (((long)b) << 32) + a;
      if (mEdgeMap2.TryGetValue (key, out var idx)) return idx;

      // Interpolate along the edge with the signed distance gives us the intersection
      // point of this edge with the PlaneDef. Add a new node with this point, and with
      // no links to neighbors (that will get set subsequently by the caller)
      idx = mUsedNodes++;
      if (idx >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mNodes[idx] = new Node ((da / (da - db)).Along (mVertex[a].Pos, mVertex[b].Pos));
      mEdgeMap2.Add (key, idx);
      return idx;
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

   void WalkAllChainsNew () {
      for (int i = 1; i < mUsedNodes; i++) {
         ref Node node = ref mNodes[i];
         if (node.Links == 0) throw new InvalidOperationException ();
         if (node.Links < 0) Console.Write ('*'); else Console.Write ('.');
      }
      Console.WriteLine (); 
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
      mRaw.Add ((da / (da - db)).Along (mVertex[a].Pos, mVertex[b].Pos));

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
   readonly Dictionary<(int, int), int> mEdgeMap = [];
   readonly Dictionary<Point3f, List<(int idx, bool isEnd)>> mPtMap = new (Point3fComparer.Delta);
   readonly List<int> mNbr1 = [], mNbr2 = [];   // Neighbours of intersection points at each index
   readonly List<Point3f> mRaw = [];            // interpolated intersection points (Point3f)
   bool[] mVisited = [];

   // Reuse temporary lists to reduce GC pressure
   readonly List<List<Point3f>> mOutChains = [];
   readonly List<List<Point3f>> mChainPool = [];
}
