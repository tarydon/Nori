// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3CSSurface.cs
// ║║║║╬║╔╣║ Implements surfaces derived from ECSSurface
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Math;

#region class E3Cone -------------------------------------------------------------------------------
/// <summary>Defines a Cone surface</summary>
/// The surface is defined in canonical space with tip at the origin, and expanding outward
/// along +Z direction. 
/// 
/// Parametrization:
/// Given a canonical point ptCanon lying on the canonical surface of the cone:
/// - U is in radians, and is the heading of this ptCanon from the origin, as projected on
///   the XY plane (viewing from above). A U value of 0 means the point is lying on the
///   XY plane and will project to a point on the X axis in 2D
/// - V is the distance of this point from the tip of the cone (which is basically the origin)
public sealed class E3Cone : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Cone (int id, ImmutableArray<Contour3> trims, CoordSystem cs, double halfAngle) : base (id, trims, cs) {
      HalfAngle = halfAngle;
      mFlags |= E3Flags.VLinear;
      PostLoad ();
   }
   E3Cone () { }

   // Properties ---------------------------------------------------------------
   /// <summary>The half-angle inscribed by the cone at the tip (between 0 .. PI/2)</summary>
   public readonly double HalfAngle;

   // Overrides ----------------------------------------------------------------
   // Computes the UV domain of the cone
   // U domain is always 0..2*PI, V domain is computed based on the contours
   protected override Bound2 ComputeDomain () {
      List<Point3> pts = [];
      Bound1 v = new (); var xfm = FromXfm;
      foreach (var c in Contours.SelectMany (a => a.Curves)) {
         pts.Clear ();
         c.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         foreach (var pt in pts) v += (pt * xfm).Z / _cos;
      }
      return new (new (0, Lib.TwoPI), v);
   }

   // Computes the Point3, given the u v coordinates
   protected override Point3 GetPointCanonical (double u, double v) {
      // The V value of the parametrization is linear distance from the tip of the cone. 
      // From this we can compute the radius at that point, and also the Z-height from the tip
      double radius = v * _sin, z = v * _cos;
      // The U parameter is just the rotation of this point about the Z axis (starting with 
      // U=0 lying on the X axis itself
      return new Point3 (radius, 0, z).Rotated (EAxis.Z, u);
   }

   // The normal does not depend on the v value at all (at a particular u, it is constant
   // across the height of the cone)
   protected override Vector3 GetNormalCanonical (double u, double v) 
      => new Vector3 (_cos, 0, -_sin).Rotated (EAxis.Z, u);

   // V is the distance from the tip, but since we know that the point lies on this cone,
   // we can compute it more efficiently as just pt.Z / _cos. U we get by looking at above
   // (consider X and Y) and computing the heading. U ranges from 0 .. 2*PI
   protected override Point2 GetUVCanonical (Point3 pt) {
      double u = GetUAxis (ref pt, false); 
      return new (u, pt.Z / _cos);
   }

   // Transform the cone by the given Matrix
   protected override E3Cone Xformed (Matrix3 xfm) {
      E3Cone cone = new (Id, Contours * xfm, CS * xfm, HalfAngle);
      cone.CopyMeshFrom (this, xfm);
      return cone; 
   }

   // Implementation -----------------------------------------------------------
   void PostLoad () => (_sin, _cos) = SinCos (HalfAngle);
   double _sin, _cos;
}
#endregion

#region class E3Cylinder ---------------------------------------------------------------------------
/// <summary>Represents a Cylinder in 3D space</summary>
/// The surface is defined in the canonical space and lofted into final position with 
/// a CoordSystem. 
/// 
/// Parametrization:
/// Given a canonical point ptCanon lying on the canonical surface of the cone:
/// - U is in radians, and is the heading of this ptCanon from the origin, as projected on
///   the XY plane (viewing from above). A U value of 0 means the point is lying on the
///   XY plane and will project to a point on the X axis in 2D
/// - V is the distance of this point from the XY plane (along height of the cylinder)
public sealed class E3Cylinder : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Cylinder (int id, ImmutableArray<Contour3> trims, CoordSystem cs, double radius, bool infacing = false) : base (id, trims, cs) {
      Radius = radius;
      mFlags |= E3Flags.VLinear;
      if (infacing) mFlags |= E3Flags.FlipNormal;
   }
   E3Cylinder () { }

   /// <summary>Optimized Cylinder build - rotates the CS if needed about Z to align the trim line correctly</summary>
   public static E3Cylinder Build (int id, ImmutableArray<Contour3> trims, CoordSystem cs, double radius, bool infacing) {
      // We want to possibly rotate the given CoordSystem about its local Z axis so that the
      // cut line
      Bound1 angSpan = new ();
      var xfm = Matrix3.From (cs);
      foreach (var edge in trims.First ().Curves) {
         Adjust (edge.Start); Adjust (edge.GetPoint (0.5));
      }
      if (angSpan.Length < Lib.TwoPI - Lib.Epsilon)
         cs *= Matrix3.Rotation (cs.Org, cs.Org + cs.VecZ, -(angSpan.Mid + Lib.PI));
      return new (id, trims, cs, radius, infacing);

      void Adjust (Point3 pt) {
         pt *= xfm;
         double ang = Atan2 (pt.Y, pt.X);
         if (angSpan.IsEmpty) angSpan += ang;
         else if (!angSpan.Contains (ang)) {
            if (ang < angSpan.Min) {
               double altAng = ang + Lib.TwoPI;
               if (altAng - angSpan.Max < angSpan.Min - ang) ang = altAng;
            } else {
               double altAng = ang - Lib.TwoPI;
               if (ang - angSpan.Max > angSpan.Min - altAng) ang = altAng;
            }
            angSpan += ang;
         }
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Radius of the cylinder</summary>
   public readonly double Radius;

   // Overrides ----------------------------------------------------------------
   // BuildMesh is overridden to handle a few common cases:
   // - A full cylinder (a through-hole through a sheet metal thick plane, for example
   // - A partial cylinder - like the inner/outer surface of a bend line in a sheet metal
   //   model, where the trimming curve is a perfect rectangle
   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep)
      => BuildFullCylinderMesh (tolerance, maxAngStep) ??
         BuildPartCylinderMesh (tolerance, maxAngStep) ??
         base.BuildMesh (tolerance, maxAngStep);

   // Computes the domain of the cylinder 
   protected override Bound2 ComputeDomain () {
      List<Point3> pts = [];
      Bound1 v = new (); var xfm = FromXfm;
      foreach (var c in Contours.SelectMany (a => a.Curves)) {
         pts.Clear ();
         c.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         foreach (var pt in pts) v += (pt * xfm).Z;
      }
      return new (new (0, Lib.TwoPI), v);
   }

   // In the canonical definition, the cylinder is defined with the base center at
   // the origin, and the axis aligned with +Z. The parametrization is this:
   // - U is directly in radians, wraps around in the XY plane (0 = X axis)
   // - V is the height above XY plane, in linear units
   protected override Point3 GetPointCanonical (double u, double v) {
      var (sin, cos) = SinCos (u);
      return new (Radius * cos, Radius * sin, v);
   }

   // The normal in canonical space is always horizontal
   protected override Vector3 GetNormalCanonical (double u, double v) {
      var (sin, cos) = SinCos (u);
      return new (cos, sin, 0);
   }

   // See EvaluateCanonical for the definition of U and V
   protected override Point2 GetUVCanonical (Point3 pt) {
      double u = GetUAxis (ref pt, false);
      return new (u, pt.Z);
   }

   // Transform the Cylinder by the given matrix
   protected override E3Cylinder Xformed (Matrix3 xfm) {
      E3Cylinder cyl = new (Id, Contours * xfm, CS * xfm, Radius * xfm, IsNormalFlipped);
      cyl.CopyMeshFrom (this, xfm);
      return cyl;
   }

   // Implementation -----------------------------------------------------------
   // Computes the mesh for a full cylinder, like a through hole in a sheet metal model.
   Mesh3? BuildFullCylinderMesh (double tolerance, double maxAngStep) {
      // There should be two trimming contours - top hole and bottom hole.
      // Both should be circles, with their normal axes aligned ot the axis of the cylinder
      if (Contours.Length != 2 || Contours.Any (a => a.Curves.Length != 1)) return null;
      var arcs = Contours.Select (a => a.Curves[0]).OfType<Arc3> ().ToList ();
      if (arcs.Count != 2) return null;
      double cos = arcs[0].CS.VecZ.CosineToAlreadyNormalized (arcs[1].CS.VecZ);
      if (!Abs (cos).EQ (1)) return null;
      Point3 cen0 = arcs[0].Center;
      Vector3 vecZ0 = arcs[1].Center - cen0, vecZ1 = arcs[1].Start - arcs[0].Start;
      if (!vecZ0.EQ (vecZ1)) return null;
      Point3 cenLift = cen0 + vecZ0.Normalized ();

      // Create the bottom list of points (we can just add a constant vector to these
      // to create the top list of points). We also compute here the vectors facing in
      // or out of the cylinder (based on the IsNormalFlipped bit).
      List<Point3> pts = [];
      List<Mesh3.Node> nodes = [];
      List<int> wires = [], tris = [];
      arcs[0].Discretize (pts, tolerance, maxAngStep); int n = pts.Count;
      foreach (var pt in pts) {
         Vector3 vec = (pt.SnappedToUnitLine (cen0, cenLift) - pt).Normalized ();
         if (!IsNormalFlipped) vec = -vec;
         nodes.Add (new (pt, vec));
      }
      // Now we can make the Mesh3
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         nodes.Add (new ((Point3f)(pts[i] + vecZ0), nodes[i].Vec));
         wires.Add (i); wires.Add (j); wires.Add (i + n); wires.Add (j + n);
         tris.Add (i); tris.Add (i + n); tris.Add (j);
         tris.Add (j); tris.Add (i + n); tris.Add (j + n);
      }
      if (IsNormalFlipped) tris.Reverse ();
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   // Computes the mesh for a partial cylinder (like the inner or outer surface of 
   // a sheet-metal bend). We handle here only the common case where the trimming curve
   // in 2D would be a full rectangle
   Mesh3? BuildPartCylinderMesh (double tolerance, double maxAngStep) {
      // There should be a single trimming curve - should be made of Line-Arc-Line-Arc,
      // where both the lines should be parallel to the cylinder axes, and the arcs have
      // normals aligned to the cylinder axis.
      if (Contours.Length != 1 || Contours[0].Curves.Length < 4) return null;
      var arcs = Contours[0].Curves.OfType<Arc3> ().ToList (); if (arcs.Count != 2) return null;
      var lines = Contours[0].Curves.OfType<Line3> ().ToList ();
      for (int i = lines.Count - 1; i >= 1; i--) {
         Line3 line0 = lines[i - 1], line1 = lines[i];
         Vector3 vec0 = (line0.End - line0.Start).Normalized ();
         Vector3 vec1 = (line1.End - line1.Start).Normalized ();
         if (vec0.EQ (vec1) && line0.End.EQ (line1.Start)) {
            lines[i - 1] = new (line0.PairId, line0.Start, line1.End);
            lines.RemoveAt (i);
         }
      }
      if (lines.Count != 2) return null;
      if (!arcs[0].AngSpan.EQ (arcs[1].AngSpan, 0.001)) return null;
      if (!lines[0].Length.EQ (lines[1].Length, 0.01)) return null;

      // Compute the nodes of the mesh, going around the bottom curve here, and
      // the top curve next
      Point3 cen0 = arcs[0].Center;
      Vector3 vecZ0 = arcs[1].Center - cen0;
      Point3 cenLift = cen0 + vecZ0.Normalized ();
      List<Point3> pts = [];
      arcs[0].Discretize (pts, tolerance, maxAngStep); pts.Add (arcs[0].End); int n = pts.Count;
      pts.Reverse ();
      arcs[1].Discretize (pts, tolerance, maxAngStep); pts.Add (arcs[1].End);
      List<Mesh3.Node> nodes = [];
      foreach (var pt in pts) {
         Vector3 vec = (pt.SnappedToUnitLine (cen0, cenLift) - pt).Normalized ();
         if (!IsNormalFlipped) vec = -vec;
         nodes.Add (new (pt, vec));
      }

      // Combine the nodes into triangles and wires
      List<int> tris = [], wires = [0, n, n - 1, 2 * n - 1];
      for (int i = 0; i < n - 1; i++) {
         int j = i + 1;
         wires.Add (i); wires.Add (i + 1);
         wires.Add (i + n + 1); wires.Add (i + n);
         tris.Add (i); tris.Add (i + n); tris.Add (j);
         tris.Add (j); tris.Add (i + n); tris.Add (j + n);
      }
      Mesh3.Node n0 = nodes[tris[0]], n1 = nodes[tris[1]], n2 = nodes[tris[2]];
      Point3 p0 = (Point3)n0.Pos, p1 = (Point3)n1.Pos, p2 = (Point3)n2.Pos;
      Vector3 v0 = ToV (n0.Vec), v1 = ToV (n1.Vec), v2 = ToV (n2.Vec);
      Vector3 norma = (p1 - p0) * (p2 - p0), normb = v0 + v1 + v2;
      if (norma.Opposing (normb)) tris.Reverse ();
      return new ([.. nodes], [.. tris], [.. wires]);

      // Helpers ...........................................
      static Vector3 ToV (Vec3H v) => new ((double)v.X, (double)v.Y, (double)v.Z);
   }
}
#endregion

#region class E3Plane ------------------------------------------------------------------------------
/// <summary>Represents a Planar surface</summary>
/// 
/// Parametrization: 
/// Each U,V point is converted into a 3D point (U,V,0) and lofted up into the local
/// coordinate system using the To transform
public sealed class E3Plane : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Plane (int id, ImmutableArray<Contour3> trims, CoordSystem cs) : base (id, trims, cs) 
      => mFlags |= (E3Flags.ULinear | E3Flags.VLinear);
   E3Plane () { }

   // Overrides ----------------------------------------------------------------
   // The mesh for the E3Plane can be built with just a simple 2D tessellation,
   // lofted up into the final space of the plane
   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep) {
      List<Point2> pts = [];
      List<int> splits = [0], wires = [];
      foreach (var poly in Contours.Select (a => a.Flatten (CS))) {
         int a = pts.Count;
         poly.Discretize (pts, tolerance, maxAngStep);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }

      var xfm = ToXfm;
      var normal = (Vec3H)(IsNormalFlipped ? -CS.VecZ : CS.VecZ);
      var tries = Lib.Tessellate (pts, splits);
      List<Mesh3.Node> nodes = [];
      foreach (var pt in pts) {
         Point3 pt3 = (Point3)pt * xfm;
         nodes.Add (new (new Point3f (pt3.X, pt3.Y, pt3.Z), normal));
      }
      return new ([.. nodes], [.. tries], [.. wires]);
   }

   // The domain of the plane can be computed using only Contours[0], since that
   // is always the outer contour of the plane
   protected override Bound2 ComputeDomain () {
      List<Point3> pts = [];
      Bound2 dom = new (); var xfm = FromXfm;
      foreach (var c in Contours[0].Curves) {
         pts.Clear ();
         c.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         foreach (var pt in pts) dom += (Point2)(pt * xfm);
      }
      return dom;
   }

   // Since the UV coordinates of the point are just the X,Y values of the point
   // in canonical space, this is trivial
   protected override Point3 GetPointCanonical (double u, double v) => new (u, v, 0);

   // The normal in canonical space is always +Z axis
   protected override Vector3 GetNormalCanonical (double u, double v) => new (0, 0, 1);

   // The UV coordinate of a point is just the X,Y coordinate of the point 
   protected override Point2 GetUVCanonical (Point3 pt)  => new (pt.X, pt.Y);

   // Transform the Plane by the given transform
   protected override E3Plane Xformed (Matrix3 xfm) {
      E3Plane plane = new (Id, Contours * xfm, CS * xfm);
      plane.CopyMeshFrom (this, xfm);
      return plane; 
   }
}
#endregion

#region class E3Sphere -----------------------------------------------------------------------------
/// <summary>A sphere is defined by a center point and radius</summary>
/// 
/// Parametrization:
/// - U goes from 0 to 2*PI as we rotate about the polar axis
/// - V goes from -PI/2 at the south pole to +PI/2 at the north pole
public sealed class E3Sphere : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Sphere (int id, ImmutableArray<Contour3> trims, CoordSystem cs, double radius) : base (id, trims, cs) => Radius = radius;
   E3Sphere () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Radius of the sphere</summary>
   public readonly double Radius;

   // Overrides ----------------------------------------------------------------
   // Domain is 0..360 in U and -90..90 in V
   protected override Bound2 ComputeDomain () => new (0, -Lib.HalfPI, Lib.TwoPI, Lib.HalfPI);

   // Compute the point in canonical space by first computing a point along the 0 longitude,
   // and then rotating it about the pole
   protected override Point3 GetPointCanonical (double u, double v) {
      var (sin, cos) = SinCos (v);       
      Point3 pt = new (cos * Radius, 0, sin * Radius);  // Point on the XZ plane, along longitude 0
      return pt.Rotated (EAxis.Z, u);
   }

   // The normal computation is very similar to the point computation, since the center of the
   // sphere is at the origin
   protected override Vector3 GetNormalCanonical (double u, double v) {
      var (sin, cos) = SinCos (v);
      Vector3 vec = new (cos, 0, sin);                   // Normal in XZ plane, along longitude 0
      return vec.Rotated (EAxis.Z, u);
   }

   // U is computed by first computing the longitude
   protected override Point2 GetUVCanonical (Point3 pt) {
      double u = GetUAxis (ref pt);       // First, bring pt into 0 longitude (and compute U)
      return new (u, Atan2 (pt.Z, pt.X));
   }

   // Transform the sphere by the given transform
   protected override E3Sphere Xformed (Matrix3 xfm) {
      E3Sphere sphere = new (Id, Contours * xfm, CS * xfm, Radius * xfm);
      sphere.CopyMeshFrom (this, xfm);
      return sphere;
   }
}
#endregion

#region class E3SpunSurface ------------------------------------------------------------------------
/// <summary>A SpunSurface is generated by rotating a generatrix curve around an axis</summary>
/// Canonically, the axis is the Z axis, and the resulting surface is lofted into space
/// using the provided CS transform
/// 
/// Parametrization:
/// - V is the parameter value (T) along the genetrix
/// - U is the rotation of the generated point about the Z axis
public sealed class E3SpunSurface : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3SpunSurface (int id, ImmutableArray<Contour3> trims, CoordSystem cs, Curve3 generatrix) : base (id, trims, cs) {
      Generatrix = generatrix;
      if (Generatrix.IsOnXZPlane) mFlags |= E3Flags.GeneratrixFlat;
      else throw new ArgumentException ("Invalid generatrix for SpunSurface");
   }
   E3SpunSurface () => Generatrix = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>The generatrix curve in canonical space</summary>
   /// The generatrix is always aligned in the XZ plane, and is rotated about
   /// the +Z axis to create the canonical surface
   public readonly Curve3 Generatrix;

   // Overrides ----------------------------------------------------------------
   // First compute a point on the generatrix, and then spin it around the Z axis
   protected override Point3 GetPointCanonical (double u, double v)
      => Generatrix.GetPoint (v).Rotated (EAxis.Z, u);

   // The generatrix is in the XZ plane, so we start by computing this cross
   // product between YAxis and the tangent of the generatrix (at the given v). 
   // This gives us the normal at a u value of 0, so we can just rotate the normal
   // by u about the ZAxis to get the final normal
   protected override Vector3 GetNormalCanonical (double u, double v)
      => (Vector3.YAxis * Generatrix.GetTangent (v)).Rotated (EAxis.Z, u);

   // First, slew the point around in Z to get the 
   protected override Point2 GetUVCanonical (Point3 pt) {
      double u = GetUAxis (ref pt);
      return new (u, Generatrix.GetT (pt));
   }

   // Domain in u is 0..360 degrees, and in V is just the domain of the Generatrix curve
   protected override Bound2 ComputeDomain () 
      => new (new Bound1 (0, Lib.TwoPI), Generatrix.Domain);

   // Returns a transformed copy of this SpunSurface
   protected override E3SpunSurface Xformed (Matrix3 xfm) {
      E3SpunSurface spun = new (Id, Contours * xfm, CS * xfm, Generatrix * xfm);
      spun.CopyMeshFrom (this, xfm);
      return spun;
   }
}
#endregion

#region class E3SweptSurface -----------------------------------------------------------------------
/// <summary>A SweptSurface is generated by sweeping a generatrix curve about a vector</summary>
/// Parametrization:
/// - V is the parameter value (T) along the genetrix
/// - U is the sweep of the generated point along the sweep direction (+Z in canonical)
public sealed class E3SweptSurface : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3SweptSurface (int id, ImmutableArray<Contour3> trims, CoordSystem cs, Curve3 genetrix) : base (id, trims, cs) {
      Generatrix = genetrix;
      if (Generatrix.IsOnXYPlane) mFlags |= E3Flags.GeneratrixFlat;
      mFlags |= E3Flags.VLinear;
   }
   E3SweptSurface () => Generatrix = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>The Generatrix curve in canonical space</summary>
   /// The curve is typically in XY plane (but is not necessarily so). Then, this
   /// curve is swept up in Z to generate the canonical curve
   public readonly Curve3 Generatrix;

   // Overrides ----------------------------------------------------------------
   // Computes the domain (special casing for the common case that the generatrix
   // is on the XY plane)
   protected override Bound2 ComputeDomain () {
      List<Point3> pts = [];
      Bound1 u = new (); var xfm = FromXfm;
      foreach (var c in Contours.SelectMany (a => a.Curves)) {
         pts.Clear ();
         c.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         if (IsGeneratrixFlat) {
            foreach (var pt in pts) u += pt.Z;
         } else {
            foreach (var pt in pts) u += GetUV (pt).X;
         }
      }
      return new (u, Generatrix.Domain);
   }

   // Computing a point on the surface by taking the point on the Generatrix,
   // and moving it up by U
   protected override Point3 GetPointCanonical (double u, double v) 
      => Generatrix.GetPoint (v).Moved (0, 0, u);

   // To compute the normal:
   // We take the cross product of the Z axis and the tangent of the generatrix.
   // In the common case where the generatrix is on the XY plane, these vectors are
   // already perpendicular to each other, so we don't have to normalize. Otherwise,
   // we normalize
   protected override Vector3 GetNormalCanonical (double u, double v) {
      Vector3 normal = Vector3.ZAxis * Generatrix.GetTangent (v);
      if (!IsGeneratrixFlat) normal = normal.Normalized ();
      return normal;
   }

   // Computing the UV handles the common special case where the generatrix is
   // flat on the XY plane
   protected override Point2 GetUVCanonical (Point3 pt) {
      if (IsGeneratrixFlat) {
         // If generatrix is flat on XY plane, then the Z coordinate is directly
         // the V value.
         double v = (_unlofter ??= new CurveUnlofter (Generatrix)).GetT (new (pt.X, pt.Y, 0));
         return new (pt.Z, v);
      } else {
         // Otherwise, we create a 'projected generatrix' that is the projection of the
         // generatrix curve in XY plane, and 
         if (Generatrix is NurbsCurve3 nc) {
            var ctrl = nc.Ctrl.Select (a => new Point3 (a.X, a.Y, 0));
            _flatGenetrix = new NurbsCurve3 (0, [.. ctrl], nc.Knot, nc.Weight);
         } else
            throw new NotImplementedException ();

         double v = (_unlofter ??= new CurveUnlofter (_flatGenetrix)).GetT (new (pt.X, pt.Y, 0));
         return new (pt.Z - Generatrix.GetPoint (v).Z, v);
      }
   }
   CurveUnlofter? _unlofter;
   Curve3? _flatGenetrix;

   // Returns a copy of this SweptSurface transformed by the given matrix
   protected override E3SweptSurface Xformed (Matrix3 xfm) {
      E3SweptSurface swept = new (Id, Contours * xfm, CS * xfm, Generatrix * xfm);
      swept.CopyMeshFrom (this, xfm);
      return swept;
   }
}
#endregion

#region class E3Torus ------------------------------------------------------------------------------
/// <summary>A Torus is defined in the XY plane and lofted into a given coordinate system</summary>
/// 
/// Parametrization:
/// The Torus is generated by rotating a circle initially centered at (RMajor,0,0)
/// and aligned in the XZ plane, about the Z axis. 
/// - V is the position along the initial minor circle, with V=0 corresponding to
///   the point at (RMajor+RMinor, 0, 0) and moving CCW as viewed from +Y
/// - U is the rotation of this generating minor circle about Z axis
public sealed class E3Torus : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Torus (int id, ImmutableArray<Contour3> trims, CoordSystem cs, double rmajor, double rminor) : base (id, trims, cs) {
      RMajor = rmajor; RMinor = rminor;
   }
   E3Torus () { }

   // Properties ---------------------------------------------------------------
   /// <summary>The radius of the generating circle</summary>
   public readonly double RMinor;
   /// <summary>Radius of the major circle along which this minor circle's center orbits</summary>
   public readonly double RMajor;

   // Overrides ----------------------------------------------------------------
   // The domain of a torus extends from 0..360 in both U and V
   protected override Bound2 ComputeDomain () 
      => new (0, 0, Lib.TwoPI, Lib.TwoPI);

   /// <summary>Converts the given U,V coordinate into a 3D point on the surface of the Torus</summary>
   protected override Point3 GetPointCanonical (double u, double v) {
      var (sin, cos) = SinCos (v);     
      Point3 pos = new (RMajor + RMinor * cos, 0, RMinor * sin);
      return pos.Rotated (EAxis.Z, u);
   }

   /// <summary>See EvaluateCanonical for an explanation of the parametrization</summary>
   /// The normal computation follows from the same
   protected override Vector3 GetNormalCanonical (double u, double v) {
      var (sin, cos) = SinCos (v);
      return new Vector3 (cos, 0, sin).Rotated (EAxis.Z, u);
   }
   
   /// <summary>See EvaluateCanonical above for a description of the parametrization</summary>
   protected override Point2 GetUVCanonical (Point3 pt) {
      double u = GetUAxis (ref pt);
      double v = Atan2 (pt.Z, pt.X - RMajor); if (v < 0) v += Lib.TwoPI;
      return new (u, v);
   }

   // Transform the entity by the given matrix
   protected override E3Torus Xformed (Matrix3 xfm) {
      E3Torus torus = new (Id, Contours * xfm, CS * xfm, RMajor * xfm, RMinor * xfm);
      torus.CopyMeshFrom (this, xfm);
      return torus; 
   }
}
#endregion
