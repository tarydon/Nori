// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;

namespace Nori;

#region class E3Cylinder ---------------------------------------------------------------------------
/// <summary>
/// Represents a Cylinder in 3D space
/// </summary>
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

   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep)
      => BuildFullCylinderMesh (tolerance, maxAngStep) ??
         BuildPartCylinderMesh (tolerance, maxAngStep) ??
         base.BuildMesh (tolerance, maxAngStep);

   Mesh3? BuildFullCylinderMesh (double tolerance, double maxAngStep) {
      if (mContours.Length != 2 || mContours.Any (a => a.Edges.Length != 1)) return null;
      var arcs = mContours.Select (a => a.Edges[0]).OfType<Arc3> ().ToList ();
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
      arcs[0].Discretize (pts, tolerance, maxAngStep); int n = pts.Count;
      foreach (var pt in pts) {
         Vector3 vec = (pt.SnappedToUnitLine (cen0, cenLift) - pt).Normalized ();
         if (!InFacing) vec = -vec;
         nodes.Add (new (pt, vec));
      }
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         nodes.Add (new ((Point3f)(pts[i] + vecZ0), nodes[i].Vec));
         wires.Add (i); wires.Add (j); wires.Add (i + n); wires.Add (j + n);
         tris.Add (i); tris.Add (i + n); tris.Add (j);
         tris.Add (j); tris.Add (i + n); tris.Add (j + n);
      }
      if (InFacing) tris.Reverse ();
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   Mesh3? BuildPartCylinderMesh (double tolerance, double maxAngStep) {
      if (mContours.Length != 1 || mContours[0].Edges.Length < 4) return null;
      var arcs = mContours[0].Edges.OfType<Arc3> ().ToList (); if (arcs.Count != 2) return null;
      var lines = mContours[0].Edges.OfType<Line3> ().ToList ();
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
      arcs[0].Discretize (pts, tolerance, maxAngStep); pts.Add (arcs[0].End); int n = pts.Count;
      pts.Reverse ();
      arcs[1].Discretize (pts, tolerance, maxAngStep); pts.Add (arcs[1].End);
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
      Mesh3.Node n0 = nodes[tris[0]], n1 = nodes[tris[1]], n2 = nodes[tris[2]];
      Point3 p0 = (Point3)n0.Pos, p1 = (Point3)n1.Pos, p2 = (Point3)n2.Pos;
      Vector3 v0 = ToV (n0.Vec), v1 = ToV (n1.Vec), v2 = ToV (n2.Vec);
      Vector3 norma = (p1 - p0) * (p2 - p0), normb = v0 + v1 + v2;
      if (norma.Opposing (normb)) tris.Reverse ();
      return new ([.. nodes], [.. tris], [.. wires]);

      // Helpers ...........................................
      static Vector3 ToV (Vec3H v) => new ((double)v.X, (double)v.Y, (double)v.Z);
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

#region class NurbsSurface -------------------------------------------------------------------------
/// <summary>
/// Represents a NURBS surface (any order, rational or simple)
/// </summary>
public sealed class NurbsSurface : E3ParaSurface {
   NurbsSurface () => mUImp = mVImp = null!;
   public NurbsSurface (int id, ImmutableArray<Point3> ctrl, ImmutableArray<double> weight, int uCtl, ImmutableArray<double> uknots, ImmutableArray<double> vknots, IEnumerable<Contour3> trims) : base (id, trims) {
      UCtl = uCtl; Ctrl = ctrl; Weight = weight;
      mUImp = new (uCtl, uknots); mVImp = new (VCtl, vknots);
   }
   readonly SplineImp mUImp, mVImp;

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The 2-dimensional grid of control points
   /// </summary>
   /// This is a 2D array, flattened. The total number of points here is UCtl x VCtl. 
   /// V is the index that varies fastest, so the linear index for (u, v) is (u * VCtl + v). 
   public readonly ImmutableArray<Point3> Ctrl;

   /// <summary>
   /// The weights for the control points (if all are set to 1, this is a non-rational spline)
   /// </summary>
   public readonly ImmutableArray<double> Weight;

   /// <summary>
   /// Number of 'columns' in the control point grid
   /// </summary>
   public readonly int UCtl;
   /// <summary>
   /// Number of 'rows' in the control point grid
   /// </summary>
   public int VCtl => Ctrl.Length / UCtl;

   // Overrides ----------------------------------------------------------------
   protected override Vector3 EvalNormal (Point2 pt) => throw new NotImplementedException ();

   protected override Point3 Evaluate (Point2 pt) {
      double u = pt.X.Clamp (mUImp.Knot[0], mUImp.Knot[^1] - 1e-9);
      double[] ufactor = mUFactor.Value!;
      while (ufactor.Length < mUImp.Order)
         mUFactor.Value = ufactor = new double[ufactor.Length * 2];
      int uSpan = mUImp.ComputeBasis (u, ufactor), up = mUImp.Degree; 

      double v = pt.Y.Clamp (mVImp.Knot[0], mVImp.Knot[^1] - 1e-9);
      double[] vfactor = mVFactor.Value!;
      while (vfactor.Length < mVImp.Order)
         mVFactor.Value = vfactor = new double[vfactor.Length * 2];
      int vSpan = mVImp.ComputeBasis (v, vfactor), vp = mVImp.Degree;
      int vPts = mVImp.CPts;

      Point3 sum = Point3.Zero; double wsum = 0;
      for (int j = 0; j <= up; j++) {
         int jn = uSpan - up + j; double fBU = ufactor[j];
         // The insum and inwsum values contain the vector sum and the weight sum of just
         // this one row [j,*] of the entire control mesh. We use this to avoid having to 
         // do so many multiplications with fBU, since that does not change during the inner
         // loop. 
         Point3 insum = Point3.Zero; double inwsum = 0;
         for (int k = 0; k <= vp; k++) {
            int kn = vSpan - vp + k;
            double fBV = vfactor[k] * Weight[jn * vPts + kn];
            insum += mCtrl[jn * vPts + kn] * fBV;
            inwsum += fBV;
         }
         wsum += inwsum * fBU;
         sum += insum * fBU;
      }
      if (wsum.IsZero ()) wsum = 1;
      return sum * (1 / wsum);
   }

   // These buffers are used to store the results of SplineImp.ComputeBasis call in 
   // U and V. To avoid allocating a buffer on each Evaluate call, we make this static. 
   // To then make it thread safe, we mark it as ThreadLocal (we grow this as the 
   // order of the Spline we're evalauting increases, and never shrink this buffer)
   static readonly ThreadLocal<double[]> mUFactor = new (() => new double[8]);
   static readonly ThreadLocal<double[]> mVFactor = new (() => new double[8]);

   protected override Point2 Flatten (Point3 pt) => throw new NotImplementedException ();
}
#endregion

#region class E3Plane ------------------------------------------------------------------------------
/// <summary>
/// Represents a Planar surface
/// </summary>
public sealed class E3Plane : E3Surface {
   E3Plane () { }
   public E3Plane (int id, IEnumerable<Contour3> trims, CoordSystem cs) : base (id, trims) => mCS = cs;
   CoordSystem mCS;

   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep) {
      List<Point2> pts = [];

      List<int> splits = [0], wires = [];
      foreach (var poly in Contours.Select (a => a.Flatten (mCS, tolerance, maxAngStep))) {
         int a = pts.Count;
         poly.Discretize (pts, tolerance, maxAngStep);
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
         nodes.Add (new (new Point3f (pt3.X, pt3.Y, pt3.Z), normal));
      }
      return new ([.. nodes], [.. tries], [.. wires]);
   }

   Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;
}
#endregion
