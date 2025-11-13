// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3Base.cs
// ║║║║╬║╔╣║ Defines some the Ent3 hierarchy of classes (the abstract base classes)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Ent3 ---------------------------------------------------------------------------------
/// <summary>The base class for all Ent3</summary>
/// The hiearchy below shows the different classes derived from Ent3. When we import a STEP file,
/// all the surfaces from the BREP become objects of types derived from E3Surface (that is the level
/// at which we maintain connectivity information). When we do a sheet-metalization of that model,
/// we create entities derived from E3Thick (this is now a developable sheet-metal model).
/// Note that one could argue the E3Plane should be an E3CSSurface, but we derive it directly from
/// E3Surface for reasons of efficiency (also based on the observation that 90% of all entities we
/// will import will be E3Plane and we want to keep that as lightweight as possible). It is important
/// that E3Plane is an E3Surface mainly because of connectivity information considerations.
///
/// E3Mesh                - Wrapper around a Mesh
/// E3Thick               - Base for all sheet-metal models (entities with thickness)
///   E3Sheet             - Thick planar surface (equivalent of Flux E3Plane)
///   E3Flex              - Represents a deforming area of the sheet (bend line)
/// E3Curve               - Curves in space
/// E3Surface             - Basis for BRep model (surface with outer/inner trimming curves, connectivity)
///   E3ParaSurface       - Parametric surfaces (trimming curves projectable to UV domain for tessellation)
///     E3CSSurface       - Surfaces defined in a canonical world space and lofted up using a CS
///       E3Cylinder      - Cylindrical surface (canonical definition with base center at origin, axis along +Z)
///       E3Cone          - Conical surface (canonical definition with base center at origin, axis along +Z)
///       E3Torus         - Toroidal surface (canonical definition with center at origin, aligned to XY plane)
///       E3Sphere        - Spherical surface (canonical definition with center at origin)
///     E3NurbSurface     - Rational spline surface of arbitrary order / degree
///     E3RuledSurface    - Surface defined by drawing ruling lines between two equi-parametric curves
///	  E3PrismSurface    - Surface defined by sweeping a generatrix curve along a line
///     E3SpunSurface     - Surface defined by spinning a generatrix curve about an axis
///   E3Plane             - Plane defined by a set of Poly lofted into space
[EPropClass]
public abstract partial class Ent3 {
   protected Ent3 () { }
   public Ent3 (int id) => Id = id;

   public abstract Bound3 Bound { get; }

   public readonly int Id;

   public override string ToString () => $"{GetType ().Name} #{Id}";

   /// <summary>Is this entity selected?</summary>
   public bool IsSelected {
      get => Get (E3Flags.Selected);
      set { if (Set (E3Flags.Selected, value)) Notify (EProp.Selected); }
   }

   public E3Flags Flags => mFlags;

   // Protected ----------------------------------------------------------------
   // Bitflags for this entity
   protected E3Flags mFlags;
   // Returns true if the specified bit is set
   protected bool Get (E3Flags bit) => (mFlags & bit) != 0;
   // Sets/resets one bit from the flags, returns true if state changed
   protected bool Set (E3Flags bits, bool value) {
      var old = mFlags;
      if (value) mFlags |= bits; else mFlags &= ~bits;
      return mFlags != old;
   }
}
#endregion

[Flags]
public enum E3Flags {
   Selected = 0x1,
}

#region class E3Surface ----------------------------------------------------------------------------
public abstract class E3Surface : Ent3 {
   protected E3Surface () { }
   public E3Surface (int id, IEnumerable<Contour3> trims) : base (id) => mContours = [.. trims];

   public override Bound3 Bound => Bound3.Update (ref mBound, ComputeBound);
   Bound3 mBound = new ();

   public Mesh3 Mesh => _mesh ??= BuildMesh (Lib.FineTess);
   Mesh3? _mesh;

   public IReadOnlyList<Contour3> Contours => mContours;
   protected Contour3[] mContours = [];

   // Implementation -----------------------------------------------------------
   Bound3 ComputeBound () {
      List<Point3> pts = [];
      mContours[0].Discretize (pts, Lib.CoarseTess, 0.5410);
      return new (pts);
   }

   protected abstract Mesh3 BuildMesh (double tolerance);
}
#endregion

#region class E3ParaSurface ------------------------------------------------------------------------
public abstract class E3ParaSurface : E3Surface {
   protected E3ParaSurface () { }
   public E3ParaSurface (int id, IEnumerable<Contour3> trims) : base (id, trims) { }
   protected abstract Point3 Evaluate (Point2 pt);
   protected abstract Vector3 EvalNormal (Point2 pt);
   protected abstract Point2 Flatten (Point3 pt);

   protected override Mesh3 BuildMesh (double tolerance) {
      // First, we flatten each trimming curve into the UV space, and compute a
      // 2D triangular tessellation in the UV space. At this point, we compute the
      // following set of data:
      List<Point3> pts = [];  // Discretization of all the trimming curves of the surface
      List<int> splits = [0]; // Split points that divide pts into individual contours
      List<int> wires = [];   // Elements taken as pairs that defined the silhouette wires
      foreach (var contour in Contours) {
         int a = pts.Count;
         contour.Discretize (pts, tolerance, 0.5410);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }
      // Now we can use the 2D tessellator to compute the following:
      var uvs = pts.Select (Flatten).ToList (); // Same as the set of pts, flattened to UV space
      var tris = Lib.Tessellate (uvs, splits);  // The indices (taken 3 at a time) forming the tessellation in UV space
      for (int i = pts.Count; i < uvs.Count; i++)
         pts.Add (Evaluate (uvs[i]));

      // The UV tessellation will have some triangles, but not all of them can directly be lofted
      // and used in the 3D. Some of them will need to be further subdivided into smaller triangles
      // (to cope with the curvature)
      List<Node> nodes = [];
      List<int> triangles = [];
      for (int i = 0; i < uvs.Count; i++) {
         Point2 uv = uvs[i];
         nodes.Add (new (uv, (Vec3F)pts[i], (Vec3H)EvalNormal (uv)));
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
      var mnodes = nodes.Select (a => new Mesh3.Node (a.Pos, a.Normal)).ToImmutableArray ();
      return new Mesh3 (mnodes, [.. triangles], [.. wires]);

      // Helpers ...........................................
      void AddTriangle (int a, int b, int c) {
         // Take each of the midpoints and see which one has the worst deviation,
         // that will be where we split
         Node na = nodes[a], nb = nodes[b], nc = nodes[c];
         Point2 p2ab = na.UV.Midpoint (nb.UV), p2bc = nb.UV.Midpoint (nc.UV), p2ca = nc.UV.Midpoint (na.UV);
         Point3 p3ab = Evaluate (p2ab), p3bc = Evaluate (p2bc), p3ca = Evaluate (p2ca);
         double dab = Dist (p3ab, na.Pos, nb.Pos), dbc = Dist (p3bc, nb.Pos, nc.Pos), dca = Dist (p3ca, nc.Pos, na.Pos);

         if (dab > tolerance && dbc > tolerance && dca > tolerance) {   // Split into 4 triangles
            int ab = AddNode (p2ab, p3ab), bc = AddNode (p2bc, p3bc), ca = AddNode (p2ca, p3ca);
            AddTriangle (a, ab, ca); AddTriangle (b, bc, ab); AddTriangle (c, ca, bc);
            AddTriangle (ab, bc, ca);
         } else if (dab >= dbc && dab >= dca && dab > tolerance) {   // Try splitting ab
            int n = AddNode (p2ab, p3ab);
            AddTriangle (a, n, c); AddTriangle (n, b, c);
         } else if (dbc >= dab && dbc >= dca && dbc > tolerance) {    // Try splitting bc
            int n = AddNode (p2bc, p3bc);
            AddTriangle (a, b, n); AddTriangle (n, c, a);
         } else if (dca >= dab && dca >= dbc && dca > tolerance) {    // Try splitting ca
            int n = AddNode (p2ca, p3ca);
            AddTriangle (a, b, n); AddTriangle (n, b, c);
         } else {       // No splitting required, triangle is flat enough to add
            triangles.Add (a); triangles.Add (b); triangles.Add (c);
         }
      }

      int AddNode (Point2 uv, Point3 pt) {
         if (cache.TryGetValue (uv, out int n)) return n;
         nodes.Add (new (uv, (Vec3F)pt, (Vec3H)EvalNormal (uv)));
         cache.Add (uv, n = nodes.Count - 1);
         return n;
      }

      double Dist (Point3 pt, Vec3F a, Vec3F b)
         => pt.DistToLine ((Point3)a, (Point3)b);
   }

   struct Node {
      public Node (Point2 uv, Vec3F pos, Vec3H normal) { UV = uv; Pos = pos; Normal = normal; }
      public Point2 UV;
      public Vec3F Pos;
      public Vec3H Normal;
   }
}
#endregion

#region class E3CSSurface --------------------------------------------------------------------------
public abstract class E3CSSurface : E3ParaSurface {
   protected E3CSSurface () { }
   public E3CSSurface (int id, IEnumerable<Contour3> trims, CoordSystem cs) : base (id, trims) => mCS = cs;

   public CoordSystem CS => mCS;
   readonly CoordSystem mCS;

   // The matrix to go from the world coordinate system to the entity's private CS
   public Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;

   /// <summary>Matrix to go from the entity's private CS to the world Coordinate System</summary>
   public Matrix3 FromXfm => _fromXfm ??= Matrix3.From (mCS);
   Matrix3? _fromXfm;

   protected sealed override Point3 Evaluate (Point2 pt) => EvaluateCanonical (pt) * ToXfm;
   protected sealed override Vector3 EvalNormal (Point2 pt) => EvalNormalCanonical (pt) * ToXfm;
   protected sealed override Point2 Flatten (Point3 pt) => FlattenCanonical (pt * FromXfm);

   protected abstract Point3 EvaluateCanonical (Point2 pt);
   protected abstract Vector3 EvalNormalCanonical (Point2 pt);
   protected abstract Point2 FlattenCanonical (Point3 pt);
}
#endregion
