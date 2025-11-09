namespace Nori;

#region class E3Cylinder ---------------------------------------------------------------------------
public sealed class E3Cylinder : E3CSSurface {
   // Constructors -------------------------------------------------------------
   E3Cylinder () { }
   public E3Cylinder (int id, IEnumerable<Contour3> trims, CoordSystem cs, double radius) 
      : base (id, trims, cs) => Radius = radius;

   public static E3Cylinder Build (int id, IEnumerable<Contour3> trims, CoordSystem cs, double radius) {
      // We want to possibly rotate the given CoordSystem about its local Z axis so that the
      // cut line 
      Bound1 angSpan = new ();
      var xfm = Matrix3.From (cs);
      foreach (var edge in trims.First ().Edges) {
         Adjust (edge.Start); Adjust (edge.GetPointAt (0.5));
      }
      if (angSpan.Length > Lib.TwoPI - Lib.Epsilon) {
         Lib.Trace ("Not working");
         return new (id, trims, cs, radius);
      } else {
         cs *= Matrix3.Rotation (EAxis.Z, -angSpan.Min);
      }
      return new E3Cylinder (id, trims, cs, radius);

      void Adjust (Point3 pt) {
         pt *= xfm;
         double ang = Math.Atan2 (pt.Y, pt.X);
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
   /// <summary>
   /// Radius of the cylinder
   /// </summary>
   public readonly double Radius;

   // Overrides ----------------------------------------------------------------
   // In the canonical definition, the cylinder is defined with the base center at
   // the origin, and the axis aligned with +Z. The parametrization is this:
   // - U is directly in radians, wraps around in the XY plane (0 = X axis)
   // - V is the height above XY plane, in linear units
   protected override Point3 EvaluateCanonical (Point2 pt) {
      var (sin, cos) = Math.SinCos (pt.X);
      return new (Radius * cos, Radius * sin, pt.Y);
   }

   // The normal in canonical space is always horizontal
   protected override Vector3 EvalNormalCanonical (Point2 pt) {
      var (sin, cos) = Math.SinCos (pt.X);
      return new (cos, sin, 0);
   }

   // See EvaluateCanonical for the definition of U and V
   protected override Point2 FlattenCanonical (Point3 pt) {
      double ang = Math.Atan2 (pt.Y, pt.X); if (ang < 0) ang += Lib.TwoPI;
      return new (ang, pt.Z);
   }
}
#endregion

#region class E3Plane ------------------------------------------------------------------------------
public sealed class E3Plane : E3Surface {
   E3Plane () { }
   public E3Plane (int id, IEnumerable<Contour3> trims, CoordSystem cs) : base (id, trims) => mCS = cs;
   CoordSystem mCS;

   protected override Mesh3 ComputeMesh (double tolerance) {
      List<Point2> pts = [];

      List<int> splits = [0], wires = [];
      foreach (var poly in Trims.Select (a => a.Flatten (mCS))) {
         int a = pts.Count;
         poly.Discretize (pts, tolerance);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }

      var xfm = ToXfm;
      var normal = (Vec3H)mCS.VecZ;
      var tries = Lib.Tessellate (pts, splits);
      List<Mesh3.Node> nodes = [];
      foreach (var pt in pts) {
         Point3 pt3 = (Point3)pt * xfm;
         nodes.Add (new (new Vec3F (pt3.X, pt3.Y, pt3.Z), normal));
      }
      return new ([.. nodes], [.. tries], [.. wires]);
   }

   Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;
}
#endregion
