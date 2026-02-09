// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SurfHelper.cs
// ║║║║╬║╔╣║ Implements SurfaceMesher
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class SurfaceMesher ------------------------------------------------------------------------
/// <summary>This computes a Mesh3 for a surface</summary>
/// NOTE: This is a placeholder. Since it generates a 2D tessellation and subdivides that to 
/// create a 3D tessellation, it does not generate high quality meshes in general. We keep this
/// here for now until we implement this better
class SurfaceMesher {
   public SurfaceMesher (E3Surface surf) => mSurf = surf;
   readonly E3Surface mSurf;

   public bool FullStencil = false;

   public Mesh3 Build (double tolerance, double maxAngStep) {
      // First, we flatten each trimming curve into the UV space, and compute a
      // 2D triangular tessellation in the UV space. At this point, we compute the
      // following set of data:
      mTolerance = tolerance;
      List<Point3> pts = [];  // Discretization of all the trimming curves of the surface
      List<int> splits = [0]; // Split points that divide pts into individual contours
      foreach (var contour in mSurf.Contours) {
         int a = pts.Count;
         contour.Discretize (pts, tolerance, maxAngStep);
         int b = pts.Count; splits.Add (b);
         mWires.Add (b - 1);
         for (int i = a; i < b; i++) { mWires.Add (i); mWires.Add (i); }
         mWires.RemoveLast ();
      }
      // Now we can use the 2D tessellator to compute the following:
      var uvs = pts.Select (mSurf.GetUV).ToList (); // Same as the set of pts, flattened to UV space
      var tris = Lib.Tessellate (uvs, splits);  // The indices (taken 3 at a time) forming the tessellation in UV space
      for (int i = pts.Count; i < uvs.Count; i++) {
         var uv = uvs[i];
         pts.Add (mSurf.GetPoint (uv.X, uv.Y));
      }

      // The UV tessellation will have some triangles, but not all of them can directly be lofted
      // and used in the 3D. Some of them will need to be further subdivided into smaller triangles
      // (to cope with the curvature)
      for (int i = 0; i < uvs.Count; i++) {
         Point2 uv = uvs[i];
         mNodes.Add (new (uv, (Point3f)pts[i], (Vec3H)mSurf.GetNormal (uv.X, uv.Y)));
      }
      if (FullStencil) mWires.Clear ();
      Dictionary<Point2, int> cache = new (new PointComparer (1e-6));
      for (int i = 0; i < tris.Count; i += 3)
         AddTriangle (tris[i], tris[i + 1], tris[i + 2], 0);

      // The AddTriangle calls above are all potentially recursive, subdividing the input
      // fed in into smaller and smaller triangles until each one is sufficiently flat
      // (to within the tessellation error we specify). As additional nodes are added in,
      // the nodes array gets expanded and the final set of triangles is stored in 'triangles'.
      // Note that the wires[] array is still valid into this expanded set of nodes, since
      // that is made up of the original boundary edges only (and not one of the interior
      // nodes we added as a part of curvature subdivision).
      var mnodes = mNodes.Select (a => new Mesh3.Node (a.Pos, a.Normal)).ToImmutableArray ();
      return new Mesh3 (mnodes, [.. mTris], [.. mWires]);
   }
   readonly List<Node> mNodes = [];
   readonly List<int> mTris = [];
   readonly List<int> mWires = [];
   double mTolerance;
   const int MAXLEVEL = 100;

   void AddTriangle (int a, int b, int c, int level) {
      // Take each of the midpoints and see which one has the worst deviation,
      // that will be where we split
      Node na = mNodes[a], nb = mNodes[b], nc = mNodes[c];
      Point2 p2ab = na.UV.Midpoint (nb.UV), p2bc = nb.UV.Midpoint (nc.UV), p2ca = nc.UV.Midpoint (na.UV);
      if (level == 0) {
         Vector3 va = (Vector3)(nb.UV - na.UV), vb = (Vector3)(nc.UV - nb.UV);
         double area = (va * vb).LengthSq;
         if (area < Lib.Epsilon) return; 
      }
      Point3 p3ab = mSurf.GetPoint (p2ab.X, p2ab.Y), p3bc = mSurf.GetPoint (p2bc.X, p2bc.Y), p3ca = mSurf.GetPoint (p2ca.X, p2ca.Y);
      double dab = Dist (p3ab, na.Pos, nb.Pos), dbc = Dist (p3bc, nb.Pos, nc.Pos), dca = Dist (p3ca, nc.Pos, na.Pos);

      if (level < MAXLEVEL) {
         if (dab > mTolerance && dbc > mTolerance && dca > mTolerance) {   // Split into 4 triangles
            int ab = AddNode (p2ab, p3ab), bc = AddNode (p2bc, p3bc), ca = AddNode (p2ca, p3ca);
            AddTriangle (a, ab, ca, level + 1); AddTriangle (b, bc, ab, level + 1); 
            AddTriangle (c, ca, bc, level + 1); AddTriangle (ab, bc, ca, level + 1);
         } else if (dab >= dbc && dab >= dca && dab > mTolerance) {   // Try splitting ab
            int n = AddNode (p2ab, p3ab);
            AddTriangle (a, n, c, level + 1); AddTriangle (n, b, c, level + 1);
         } else if (dbc >= dab && dbc >= dca && dbc > mTolerance) {    // Try splitting bc
            int n = AddNode (p2bc, p3bc);
            AddTriangle (a, b, n, level + 1); AddTriangle (n, c, a, level + 1);
         } else if (dca >= dab && dca >= dbc && dca > mTolerance) {    // Try splitting ca
            int n = AddNode (p2ca, p3ca);
            AddTriangle (a, b, n, level + 1); AddTriangle (n, b, c, level + 1);
         } else {       // No splitting required, triangle is flat enough to add
            mTris.Add (a); mTris.Add (b); mTris.Add (c);
            if (FullStencil) mWires.AddM ([a, b, b, c, c, a]);
         }
      } else {
         mTris.Add (a); mTris.Add (b); mTris.Add (c);
         if (FullStencil) mWires.AddM ([a, b, b, c, c, a]);
      }

      static double Dist (Point3 pt, Point3f a, Point3f b)
         => pt.DistToLine ((Point3)a, (Point3)b);
   }

   int AddNode (Point2 uv, Point3 pt) {
      if (mCache.TryGetValue (uv, out int n)) return n;
      mNodes.Add (new (uv, (Point3f)pt, (Vec3H)mSurf.GetNormal (uv.X, uv.Y)));
      mCache.Add (uv, n = mNodes.Count - 1);
      return n;
   }
   Dictionary<Point2, int> mCache = new (new PointComparer (1e-6));

   struct Node (Point2 uv, Point3f pos, Vec3H normal) {
      public readonly Point2 UV = uv;
      public readonly Point3f Pos = pos;
      public readonly Vec3H Normal = normal;
   }
}
#endregion
