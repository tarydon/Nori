// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SurfHelper.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

class CurveUnlofter {
   public CurveUnlofter (Edge3 edge) {
      mEdge = edge;
      const int tDiv = 8;
      var domain = edge.Domain;
      double dt = domain.Length / tDiv;

      // Build a set of T3D points
      for (int i = 0; i <= tDiv; i++) 
         mNodes.Add (new T3D (mEdge, domain.Min + i * dt));
      // Then, make Segs out of them
      for (int i = 0; i < tDiv; i++)

   }
   readonly Edge3 mEdge;

   /// <summary>
   /// This struct holds a T value and the corresponding point in 3D space
   /// </summary>
   readonly struct T3D {
      public T3D (Edge3 edge, double t) {
         T = (float)t; 
         XYZ = (Point3f)edge.GetPoint (t);
      }

      public readonly float T;
      public readonly Point3f XYZ; 
   }

   readonly struct Seg {
      // Constructs a Seg, given the 2 end points making up the Seg.
      // a = Index of the left point (in owner's mNodes array)
      // b = Index of the right point
      // level = The seg level
      public Seg (CurveUnlofter owner, int a, int b, int level) {
      }
   }

   readonly List<T3D> mNodes = [];
   readonly List<Seg> mSegs = [];
}

/// <summary>This computes a Mesh3 for a surface</summary>
class SurfaceMesher {
   public SurfaceMesher (E3Surface surf) => mSurf = surf;
   readonly E3Surface mSurf;

   public Mesh3 Build (double tolerance, double maxAngStep) {
      // First, we flatten each trimming curve into the UV space, and compute a
      // 2D triangular tessellation in the UV space. At this point, we compute the
      // following set of data:
      mTolerance = tolerance;
      List<Point3> pts = [];  // Discretization of all the trimming curves of the surface
      List<int> splits = [0]; // Split points that divide pts into individual contours
      List<int> wires = [];   // Elements taken as pairs that defined the silhouette wires
      foreach (var contour in mSurf.Contours) {
         int a = pts.Count;
         contour.Discretize (pts, tolerance, maxAngStep);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }
      // Now we can use the 2D tessellator to compute the following:
      var uvs = pts.Select (mSurf.GetUV).ToList (); // Same as the set of pts, flattened to UV space
      var tris = Lib.Tessellate (uvs, splits);  // The indices (taken 3 at a time) forming the tessellation in UV space
      for (int i = pts.Count; i < uvs.Count; i++)
         pts.Add (mSurf.GetPoint (uvs[i]));

      // The UV tessellation will have some triangles, but not all of them can directly be lofted
      // and used in the 3D. Some of them will need to be further subdivided into smaller triangles
      // (to cope with the curvature)
      for (int i = 0; i < uvs.Count; i++) {
         Point2 uv = uvs[i];
         mNodes.Add (new (uv, (Point3f)pts[i], (Vec3H)mSurf.GetNormal (uv)));
      }
      Dictionary<Point2, int> cache = new (new PointComparer (1e-6));
      for (int i = 0; i < tris.Count; i += 3)
         AddTriangle (tris[i], tris[i + 1], tris[i + 2]);

      // The AddTriangle calls above are all potentially recursive, subdividing the input
      // fed in into smaller and smaller triangles until each one is sufficiently flat
      // (to within the tessellation error we specify). As additional nodes are added in,
      // the nodes array gets expanded and the final set of triangles is stored in 'triangles'.
      // Note that the wires[] array is still valid into this expanded set of nodes, since
      // that is made up of the original boundary edges only (and not one of the interior
      // nodes we added as a part of curvature subdivision).
      var mnodes = mNodes.Select (a => new Mesh3.Node (a.Pos, a.Normal)).ToImmutableArray ();
      return new Mesh3 (mnodes, [.. mTris], [.. wires]);
   }
   readonly List<Node> mNodes = [];
   readonly List<int> mTris = [];
   double mTolerance;

   void AddTriangle (int a, int b, int c) {
      // Take each of the midpoints and see which one has the worst deviation,
      // that will be where we split
      Node na = mNodes[a], nb = mNodes[b], nc = mNodes[c];
      Point2 p2ab = na.UV.Midpoint (nb.UV), p2bc = nb.UV.Midpoint (nc.UV), p2ca = nc.UV.Midpoint (na.UV);
      Point3 p3ab = mSurf.GetPoint (p2ab), p3bc = mSurf.GetPoint (p2bc), p3ca = mSurf.GetPoint (p2ca);
      double dab = Dist (p3ab, na.Pos, nb.Pos), dbc = Dist (p3bc, nb.Pos, nc.Pos), dca = Dist (p3ca, nc.Pos, na.Pos);

      if (dab > mTolerance && dbc > mTolerance && dca > mTolerance) {   // Split into 4 triangles
         int ab = AddNode (p2ab, p3ab), bc = AddNode (p2bc, p3bc), ca = AddNode (p2ca, p3ca);
         AddTriangle (a, ab, ca); AddTriangle (b, bc, ab); AddTriangle (c, ca, bc);
         AddTriangle (ab, bc, ca);
      } else if (dab >= dbc && dab >= dca && dab > mTolerance) {   // Try splitting ab
         int n = AddNode (p2ab, p3ab);
         AddTriangle (a, n, c); AddTriangle (n, b, c);
      } else if (dbc >= dab && dbc >= dca && dbc > mTolerance) {    // Try splitting bc
         int n = AddNode (p2bc, p3bc);
         AddTriangle (a, b, n); AddTriangle (n, c, a);
      } else if (dca >= dab && dca >= dbc && dca > mTolerance) {    // Try splitting ca
         int n = AddNode (p2ca, p3ca);
         AddTriangle (a, b, n); AddTriangle (n, b, c);
      } else {       // No splitting required, triangle is flat enough to add
         mTris.Add (a); mTris.Add (b); mTris.Add (c);
      }

      static double Dist (Point3 pt, Point3f a, Point3f b)
         => pt.DistToLine ((Point3)a, (Point3)b);
   }

   int AddNode (Point2 uv, Point3 pt) {
      if (mCache.TryGetValue (uv, out int n)) return n;
      mNodes.Add (new (uv, (Point3f)pt, (Vec3H)mSurf.GetNormal (uv)));
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
