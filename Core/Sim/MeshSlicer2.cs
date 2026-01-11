namespace Nori;
using static Math;

public class MeshSlicer {
   // Constructor --------------------------------------------------------------
   public MeshSlicer (ImmutableArray<Mesh3> meshes) => mMeshes = meshes;

   // Methods ------------------------------------------------------------------
   public List<Polyline3> Compute (PlaneDef def) {
      // Prepare for this PlaneDef by resetting some plane-specific data
      mUsedNodes = 0; mNodeMap.Clear (); 
      mAbsNormal = (mDef = def).Normal.Abs ();

      // Now, process each mesh
      foreach (var mesh in mMeshes) {
         // Quickly skip meshes that cannot possibly intersect. If this routine
         // returns true (warrants further processing), it has also reset the per-mesh
         // data structures that are going to be used
         if (!PrepMesh (mesh)) continue;

         // Check each triangle in this mesh (returns a range of new nodes added in),
         // which we immediately process to link up free ends with other threads created
         // from previous meshes. Note that all the nodes created from with a mesh
         // get quickly connected using the potential 'shared edges' with neighboring
         // triangles, and this happens using the mEdgeMap dictionary
         var (start, end) = CheckTriangles (mesh.Triangle);
         // This does further connections - nodes from adjacent meshes, or even from
         // within the same mesh but with non-manifold topology will get connected up
         // here - this is just using the geometric location of the points, hashed in the
         // mNodeMap dictionary
         ProcessNewNodes (start, end);
      }

      List<Polyline3> output = [];
      if (GatherOpenCuts (output) < mUsedNodes)
         GatherClosedCuts (output);
      return output;
   }

   // Implementation -----------------------------------------------------------
   // This checks the triangles of the current mesh for intersections with the 
   // plane. For each triangle that intersects, there will be exactly two edges crossing the plane,
   // and this adds two Node entries corresponding to those edge-plane intersection points. All 
   // the connected nodes are linked together using their Link1 and Link2 connectors. 
   // This returns the range of new nodes that were added. 
   (int Start, int End) CheckTriangles (ImmutableArray<int> tri) {
      int start = mUsedNodes;
      for (int i = 0; i < tri.Length; i += 3) {
         int a = tri[i], b = tri[i + 1], c = tri[i + 2];
         double da = mDist[a], db = mDist[b], dc = mDist[c];

         // Of the three conditions below, either all will be false (meaning all the three
         // nodes of the triangle are on one side of the plane), or exactly two will be true
         // (one node on one side, the other two on the other side). We've already eliminated 
         // the possibility that any of these nodes are ON the plane (by the bias we introduce
         // when computing mDist[]
         int n1 = -1, n2 = -1;
         if (da * db < 0) n1 = GetNode (a, b, da, db);
         if (db * dc < 0) { n2 = n1; n1 = GetNode (b, c, db, dc); }
         if (dc * da < 0) { n2 = n1; n1 = GetNode (c, a, dc, da); }

         // Here, either n1 & n2 are both zero, or they are both non-zero (two intersections
         // found among the 3 edges of this triangle). 
         if (n1 != -1) LinkNodes (n1, n2);
      }
      return (start, mUsedNodes);
   }

   // This gathers the set of open cuts into the output. 
   // We process open cuts by searching for an 'endpoint' (a node that has only one link), and
   // then following the chain to the other endpoint. We do this separately first, so we don't
   // have to write code to navigate forward and backward along a doubly linked list. (That is
   // also inefficienty since we will end up having to insert at the beginning of a List, which 
   // is not a good idea). 
   // This returns the total number of points we added into these cuts. If all the nodes have
   // been used up in open cuts, we don't even have to go through the next step of gathering closed
   // cuts.
   int GatherOpenCuts (List<Polyline3> output) {
      int total = 0; 
      for (int i = 0; i < mUsedNodes; i++) {
         ref Node node = ref mNodes[i];
         if (node.Link2 >= 0 || node.Visited) continue;
         total += GatherPoints (i, -1);
         output.Add (new (0, [.. mTemp]));
      }
      return total;
   }
   List<Point3> mTemp = [];

   // This gathers closed cuts into the output. 
   // Since this is always called after GatherOpenCuts, we can assume that any unvisited
   // nodes belong to closed cuts. 
   void GatherClosedCuts (List<Polyline3> output) {
      for (int i = 0; i < mUsedNodes; i++) {
         ref Node node = ref mNodes[i];
         if (node.Visited) continue; 
         GatherPoints (i, node.Link1);
         if (!mTemp[0].EQ (mTemp[^1])) mTemp.Add (mTemp[0]); 
         output.Add (new (0, [.. mTemp]));
      }
   }

   // Gather the points from a chain of nodes starting at n into the list.
   // 'prev' is the neighbor node in the opposite direction from which we want to 
   // traverse (or -1 if we are starting at the end of an open thread). The points
   // are added into the mTemp work buffer. Returns the number of points added
   int GatherPoints (int n, int prev) {
      mTemp.Clear ();
      for (; ; ) {
         ref Node node = ref mNodes[n];
         if (node.Visited) break;
         mTemp.Add ((Point3)node.Pt);
         node.Visited = true; 
         (prev, n) = (n, node.Link1 + node.Link2 - prev);
         if (n == -1) break;
      }
      return mTemp.Count;
   }

   // Gets the node on the edge between a..b
   // da and db are the signed distances of these two vertices a and b from the plane
   int GetNode (int a, int b, double da, double db) {
      // Normalize the edge so that edges a..b and b..a both map to the
      // same key value (the key is just a 64-bit integer with the two values a and b
      // packed into it)
      long key = a < b ? (((long)a) << 32) + b : (((long)b) << 32) + a;
      if (mEdgeMap.TryGetValue (key, out var idx)) return idx;

      // Interpolate along the edge with the signed distance gives us the intersection
      // point of this edge with the PlaneDef. Add a new node with this point, and with
      // no links to neighbors (that will get set subsequently by the caller)
      idx = mUsedNodes++;
      if (idx >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mNodes[idx] = new Node ((da / (da - db)).Along (mVertex[a].Pos, mVertex[b].Pos));
      mEdgeMap.Add (key, idx);
      return idx;
   }

   // Connects together the nodes at indices a & b.
   // Each node has up to two links with neighbors - the first LinkNodes call on a particular
   // node will set up its Link1 to point to the neighbor. The next one will move that previous
   // connection over to Link2 and set up Link1 to point to the new neighbor. We don't really care
   // about the ordering of Link1 and Link2 so we use this branchless method to set up both the links,
   // that works regardless of whether this is the first or second time we call LinkNodes on a 
   // particular node. 
   void LinkNodes (int a, int b) {
      ref Node na = ref mNodes[a], nb = ref mNodes[b];
      if (na.Link2 != -1 || nb.Link2 != -1) throw new InvalidOperationException ();
      na.Link2 = na.Link1; na.Link1 = b;
      nb.Link2 = nb.Link1; nb.Link1 = a;
   }

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

      // Reset some intermediate data structures we're going to use
      mEdgeMap.Clear ();
      return true; 
   }

   // This goes through a range of newly added nodes, and tries to connect the 
   // boundary nodes (start and end of a chain) to ends of other chains that have
   // been added already. We do this after each mesh is processed, with the newly added
   // nodes of that mesh so we don't have too many 'open threads' floating about and 
   // connect them up as quickly as possible
   void ProcessNewNodes (int start, int end) {
      for (int i = start; i < end; i++) {
         ref Node node = ref mNodes[i];
         if (node.Link2 >= 0) continue;

         // We found a free node, let's see if there already a known node with the same
         // geometric endpoint (this is typically from another neighboring mesh)
         if (mNodeMap.TryGetValue (node.Pt, out var j)) {
            LinkNodes (i, j);
            mNodeMap.Remove (node.Pt);
         } else
            mNodeMap.Add (node.Pt, i);
      }
   }

   // Nested structs -----------------------------------------------------------
   struct Node {
      public Node (Point3f pt) { Pt = pt; Link1 = Link2 = -1; Visited = false; }

      // The position of this Node
      public readonly Point3f Pt;
      // The two neighboring nodes (if non-zero)
      public int Link1, Link2;
      // Have we visited this node and connected
      public bool Visited;
   }

   // Private data -------------------------------------------------------------
   PlaneDef mDef;                         // The PlaneDef we're working with
   Vector3 mAbsNormal;                    // The absolute value of the plane normal   
   ImmutableArray<Mesh3> mMeshes;         // List of meshes to test against
   ImmutableArray<Mesh3.Node> mVertex;    // List of vertices of the current mesh
   double[] mDist = [];                   // Signed distances of these nodes from the plane (for current mesh)
   Node[] mNodes = new Node[8];           // Intersections of triangle edges with the plane (across all meshes)
   int mUsedNodes = 0;                    // How many of these nodes have we used

   // Dictionary that maps edges of the triangles from the current mesh to 
   // Node objects that hold these intersections. If there are two edges from A..B and from
   // B..A, both of them hash to the same entry in this dictionary (this is the adjacency
   // information we gather)
   Dictionary<long, int> mEdgeMap = [];
   // Dictionary that maps geometric points (rounded to three decimals)
   // to mNode entries (used to do the second level of connectivity)
   Dictionary<Point3f, int> mNodeMap = new (Point3fComparer.Delta);
}
