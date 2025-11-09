// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E3Cylinder ---------------------------------------------------------------------------
public sealed class E3Cylinder : E3CSSurface {
   // Constructors -------------------------------------------------------------
   E3Cylinder () { }
   public E3Cylinder (int id, IEnumerable<Contour3> trims, CoordSystem cs, double radius, bool infacing)
      : base (id, trims, cs) => (Radius, InFacing) = (radius, infacing);

   public static E3Cylinder Build (int id, IReadOnlyList<Contour3> trims, CoordSystem cs, double radius, bool infacing) {
      // We want to possibly rotate the given CoordSystem about its local Z axis so that the
      // cut line
      Bound1 angSpan = new ();
      var xfm = Matrix3.From (cs);
      foreach (var edge in trims.First ().Edges) {
         Adjust (edge.Start); Adjust (edge.GetPointAt (0.5));
      }
      if (angSpan.Length < Lib.TwoPI - Lib.Epsilon)
         cs *= Matrix3.Rotation (cs.Org, cs.Org + cs.VecZ, -(angSpan.Mid + Lib.PI));
      return new (id, trims, cs, radius, infacing);

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

   protected override Mesh3 BuildMesh (double tolerance)
      => BuildFullCylinderMesh (tolerance) ??
         BuildPartCylinderMesh (tolerance) ??
         base.BuildMesh (tolerance);

   Mesh3? BuildFullCylinderMesh (double tolerance) {
      if (mTrims.Length != 2 || mTrims.Any (a => a.Edges.Length != 1)) return null;
      var arcs = mTrims.Select (a => a.Edges[0]).OfType<Arc3> ().ToList ();
      if (arcs.Count != 2) return null;
      double cos = arcs[0].CS.VecZ.CosineToAlreadyNormalized (arcs[1].CS.VecZ);
      if (!Math.Abs (cos).EQ (1)) return null;
      Point3 cen0 = arcs[0].Center;
      Vector3 vecZ0 = arcs[1].Center - cen0, vecZ1 = arcs[1].Start - arcs[0].Start;
      if (!vecZ0.EQ (vecZ1)) return null;
      Point3 cenLift = cen0 + vecZ0.Normalized ();

      List<Point3> pts = [];
      List<Mesh3.Node> nodes = [];
      List<int> wires = [], tris = [];
      arcs[0].Discretize (pts, tolerance); int n = pts.Count;
      foreach (var pt in pts) {
         Vector3 vec = (pt.SnappedToUnitLine (cen0, cenLift) - pt).Normalized ();
         if (!InFacing) vec = -vec;
         nodes.Add (new (pt, vec));
      }
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         nodes.Add (new ((Vec3F)(pts[i] + vecZ0), nodes[i].Vec));
         wires.Add (i); wires.Add (j); wires.Add (i + n); wires.Add (j + n);
         tris.Add (i); tris.Add (i + n); tris.Add (j);
         tris.Add (j); tris.Add (i + n); tris.Add (j + n);
      }
      if (InFacing) tris.Reverse ();
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   Mesh3? BuildPartCylinderMesh (double tolerance) {
      if (mTrims.Length != 1 || mTrims[0].Edges.Length < 4) return null;
      var arcs = mTrims[0].Edges.OfType<Arc3> ().ToList (); if (arcs.Count != 2) return null;
      var lines = mTrims[0].Edges.OfType<Line3> ().ToList ();
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

      Point3 cen0 = arcs[0].Center;
      Vector3 vecZ0 = arcs[1].Center - cen0;
      Point3 cenLift = cen0 + vecZ0.Normalized ();
      List<Point3> pts = [];
      arcs[0].Discretize (pts, tolerance); pts.Add (arcs[0].End); int n = pts.Count;
      pts.Reverse ();
      arcs[1].Discretize (pts, tolerance); pts.Add (arcs[1].End);
      List<Mesh3.Node> nodes = [];
      foreach (var pt in pts) {
         Vector3 vec = (pt.SnappedToUnitLine (cen0, cenLift) - pt).Normalized ();
         if (!InFacing) vec = -vec;
         nodes.Add (new (pt, vec));
      }
      List<int> tris = [], wires = [0, n, n - 1, 2 * n - 1];
      for (int i = 0; i < n - 1; i++) {
         int j = i + 1;
         wires.Add (i); wires.Add (i + 1);
         wires.Add (i + n + 1); wires.Add (i + n);
         tris.Add (i); tris.Add (i + n); tris.Add (j);
         tris.Add (j); tris.Add (i + n); tris.Add (j + n);
      }
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Radius of the cylinder</summary>
   public readonly double Radius;

   /// <summary>If set, the normals are facing towards the center</summary>
   public readonly bool InFacing;

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

   protected override Mesh3 BuildMesh (double tolerance) {
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
