// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Threading;
namespace Nori;
using static Math;

#region class E3Cone -------------------------------------------------------------------------------
public sealed class E3Cone : E3CSSurface {
   public E3Cone (int id, IEnumerable<Contour3> trims, CoordSystem cs, double halfAngle) : base (id, trims, cs) {
      HalfAngle = halfAngle;
      (mSin, mCos) = SinCos (halfAngle);
      mFlags |= E3Flags.VLinear;
   }
   E3Cone () { }

   public readonly double HalfAngle;
   readonly double mSin, mCos;

   protected override Point3 GetPointCanonical (double u, double v) {
      // The V value of the parametrization is linear distance from the tip of the cone. 
      // From this we can compute the radius at that point, and also the Z-height from the tip
      double r = v * mSin, z = v * mCos;
      // The U parameter is just the rotation of this point about the Z axis (starting with 
      // U=0 lying on the X axis itself
      return new Point3 (r, 0, z).Rotated (EAxis.Z, u);
   }

   protected override Vector3 GetNormalCanonical (Point2 uv) => new Vector3 (mCos, 0, -mSin).Rotated (EAxis.Z, uv.X);

   protected override Point2 GetUVCanonical (Point3 pt) => new (Atan2 (pt.Y, pt.X), pt.Z / mCos);
}
#endregion

#region class E3Cylinder ---------------------------------------------------------------------------
/// <summary>Represents a Cylinder in 3D space</summary>
public sealed class E3Cylinder : E3CSSurface {
   // Constructors -------------------------------------------------------------
   public E3Cylinder (int id, IEnumerable<Contour3> trims, CoordSystem cs, double radius, bool infacing) : base (id, trims, cs) {
      (Radius, InFacing) = (radius, infacing);
      mFlags |= E3Flags.VLinear;
   }
   E3Cylinder () { }

   public static E3Cylinder Build (int id, IReadOnlyList<Contour3> trims, CoordSystem cs, double radius, bool infacing) {
      // We want to possibly rotate the given CoordSystem about its local Z axis so that the
      // cut line
      Bound1 angSpan = new ();
      var xfm = Matrix3.From (cs);
      foreach (var edge in trims.First ().Edges) {
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

   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep)
      => BuildFullCylinderMesh (tolerance, maxAngStep) ??
         BuildPartCylinderMesh (tolerance, maxAngStep) ??
         base.BuildMesh (tolerance, maxAngStep);

   Mesh3? BuildFullCylinderMesh (double tolerance, double maxAngStep) {
      if (mContours.Length != 2 || mContours.Any (a => a.Edges.Length != 1)) return null;
      var arcs = mContours.Select (a => a.Edges[0]).OfType<Arc3> ().ToList ();
      if (arcs.Count != 2) return null;
      double cos = arcs[0].CS.VecZ.CosineToAlreadyNormalized (arcs[1].CS.VecZ);
      if (!Abs (cos).EQ (1)) return null;
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
   protected override Point3 GetPointCanonical (double u, double v) {
      var (sin, cos) = SinCos (u);
      return new (Radius * cos, Radius * sin, v);
   }

   // The normal in canonical space is always horizontal
   protected override Vector3 GetNormalCanonical (Point2 pt) {
      var (sin, cos) = SinCos (pt.X);
      return new (cos, sin, 0);
   }

   // See EvaluateCanonical for the definition of U and V
   protected override Point2 GetUVCanonical (Point3 pt) {
      double ang = Atan2 (pt.Y, pt.X); if (ang < 0) ang += Lib.TwoPI;
      return new (ang, pt.Z);
   }
}
#endregion

#region class NurbsSurface -------------------------------------------------------------------------
/// <summary>Represents a NURBS surface (any order, rational or simple)</summary>
public sealed class E3NurbsSurface : E3Surface {
   E3NurbsSurface () => mUImp = mVImp = null!;
   public E3NurbsSurface (int id, ImmutableArray<Point3> ctrl, ImmutableArray<double> weight, int uCtl, ImmutableArray<double> uknots, ImmutableArray<double> vknots, IEnumerable<Contour3> trims) : base (id, trims) {
      UCtl = uCtl; Ctrl = ctrl; Weight = weight;
      mUImp = new (uCtl, uknots); mVImp = new (VCtl, vknots);
      Rational = !(weight.IsEmpty || weight.All (a => a.EQ (1)));
      if (!Rational) Weight = [];
   }
   readonly SplineImp mUImp, mVImp;

   // Properties ---------------------------------------------------------------
   /// <summary>The 2-dimensional grid of control points</summary>
   /// This is a 2D array, flattened. The total number of points here is UCtl x VCtl. 
   /// V is the index that varies fastest, so the linear index for (u, v) is (u * VCtl + v). 
   public readonly ImmutableArray<Point3> Ctrl;

   /// <summary>Is this a rational spline? (all weights set to 1)</summary>
   public readonly bool Rational;

   /// <summary>The weights for the control points (if all are set to 1, this is a non-rational spline)</summary>
   public readonly ImmutableArray<double> Weight;

   /// <summary>Number of 'columns' in the control point grid</summary>
   public readonly int UCtl;
   /// <summary>Number of 'rows' in the control point grid</summary>
   public int VCtl => Ctrl.Length / UCtl;

   // Overrides ----------------------------------------------------------------
   public override Point3 GetPoint (double u, double v) {
      u = u.Clamp (mUImp.Knot[0], mUImp.Knot[^1] - 1e-9);
      double[] ufactor = mUFactor.Value!;
      while (ufactor.Length < mUImp.Order)
         mUFactor.Value = ufactor = new double[ufactor.Length * 2];
      int uSpan = mUImp.ComputeBasis (u, ufactor), up = mUImp.Degree; 

      v = v.Clamp (mVImp.Knot[0], mVImp.Knot[^1] - 1e-9);
      double[] vfactor = mVFactor.Value!;
      while (vfactor.Length < mVImp.Order)
         mVFactor.Value = vfactor = new double[vfactor.Length * 2];
      int vSpan = mVImp.ComputeBasis (v, vfactor), vp = mVImp.Degree;
      int vPts = VCtl;

      Point3 sum = Point3.Zero;
      if (Rational) {
         // We compute a weighted sum of some subset of the control points
         // around a zone of interest
         double wsum = 0;
         for (int j = 0; j <= up; j++) {
            int jn = uSpan - up + j; double fBU = ufactor[j];
            // The insum and inwsum values contain the vector sum and the weight sum of just
            // this one row [j,*] of the entire control mesh. We use this to avoid having to 
            // do so many multiplications with fBU, since that does not change during the inner
            // loop. 
            Point3 insum = Point3.Zero; double inwsum = 0;
            for (int k = 0; k <= vp; k++) {
               int kn = vSpan - vp + k;
               int idx = jn * vPts + kn;
               double fBV = vfactor[k] * Weight[idx];
               insum += Ctrl[idx] * fBV;
               inwsum += fBV;
            }
            wsum += inwsum * fBU;
            sum += insum * fBU;
         }
         if (wsum.IsZero ()) wsum = 1;
         return sum * (1 / wsum);
      } else {
         // This is a slightly optimized implementation of the same 
         // weighted-average loop as above, tuned for the case where all the
         // weights are 1
         for (int j = 0; j <= up; j++) {
            int jn = uSpan - up + j; double fBU = ufactor[j];
            Point3 insum = Point3.Zero; 
            for (int k = 0; k <= vp; k++) {
               int kn = vSpan - vp + k;
               insum += Ctrl[jn * vPts + kn] * vfactor[k];
            }
            sum += insum * fBU;
         }
         return sum;
      }
   }
   // These buffers are used to store the results of SplineImp.ComputeBasis call in 
   // U and V. To avoid allocating a buffer on each Evaluate call, we make this static. 
   // To then make it thread safe, we mark it as ThreadLocal (we grow this as the 
   // order of the Spline we're evalauting increases, and never shrink this buffer)
   static readonly ThreadLocal<double[]> mUFactor = new (() => new double[8]);
   static readonly ThreadLocal<double[]> mVFactor = new (() => new double[8]);

   public override Bound2 ComputeDomain () => new (mUImp.Knot[0], mVImp.Knot[0], mUImp.Knot[^1], mVImp.Knot[^1]);

   public override Point2 GetUV (Point3 pt) => (_unlofter ??= new (this)).GetUV (pt);
   SurfaceUnlofter? _unlofter;
}
#endregion

#region class E3Plane ------------------------------------------------------------------------------
/// <summary>Represents a Planar surface</summary>
public sealed class E3Plane : E3CSSurface {
   E3Plane () { }
   public E3Plane (int id, IEnumerable<Contour3> trims, CoordSystem cs) : base (id, trims, cs) 
      => mFlags |= (E3Flags.ULinear | E3Flags.VLinear);

   protected override Mesh3 BuildMesh (double tolerance, double maxAngStep) {
      List<Point2> pts = [];

      List<int> splits = [0], wires = [];
      foreach (var poly in Contours.Select (a => a.Flatten (CS, tolerance, maxAngStep))) {
         int a = pts.Count;
         poly.Discretize (pts, tolerance, maxAngStep);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }

      var xfm = ToXfm;
      var normal = (Vec3H)CS.VecZ;
      var tries = Lib.Tessellate (pts, splits);
      List<Mesh3.Node> nodes = [];
      foreach (var pt in pts) {
         Point3 pt3 = (Point3)pt * xfm;
         nodes.Add (new (new Point3f (pt3.X, pt3.Y, pt3.Z), normal));
      }
      return new ([.. nodes], [.. tries], [.. wires]);
   }

   protected override Point3 GetPointCanonical (double u, double v) => new (u, v, 0);
   protected override Vector3 GetNormalCanonical (Point2 pt) => new (0, 0, 1);
   protected override Point2 GetUVCanonical (Point3 pt) => new (pt.X, pt.Y);
}
#endregion

#region class E3Sphere -----------------------------------------------------------------------------
/// <summary>A sphere is defined by a center point and radius</summary>
/// The parametrization is as follows:
/// - U goes from 0 at the south pole to 1 at the north pole
/// - V goes from 0 to 1 as we rotate about the polar axis
public sealed class E3Sphere : E3Surface {
   public E3Sphere (int id, List<Contour3> trims, Point3 center, double radius) : base (id, trims) {
      Center = center; Radius = radius; 
   }
   public readonly Point3 Center;
   public readonly double Radius;

   public override Point3 GetPoint (double u, double v) => throw new NotImplementedException ();
   public override Point2 GetUV (Point3 pt) => throw new NotImplementedException ();
}
#endregion

#region class E3SpunSurface ------------------------------------------------------------------------
/// <summary>A SpunSurface is generated by rotating a generatrix curve around an axis</summary>
/// Canonically, the axis is the Z axis, and the resulting surface is lofted into space
/// using the provided CS transform
public sealed class E3SpunSurface : E3CSSurface {
   public E3SpunSurface (int id, IEnumerable<Contour3> trims, CoordSystem cs, Edge3 genetrix) : base (id, trims, cs) {
      Genetrix = genetrix; 
   }
   E3SpunSurface () => Genetrix = null!;
   public readonly Edge3 Genetrix;

   protected override Point3 GetPointCanonical (double u, double v) => throw new NotImplementedException ();
   protected override Vector3 GetNormalCanonical (Point2 pt) => throw new NotImplementedException ();
   protected override Point2 GetUVCanonical (Point3 pt) => throw new NotImplementedException ();
}
#endregion

#region class E3SweptSurface -----------------------------------------------------------------------
/// <summary>A SweptSurface is generated by sweeping a generatrix curve about a vector</summary>
public sealed class E3SweptSurface : E3CSSurface {
   public E3SweptSurface (int id, IEnumerable<Contour3> trims, CoordSystem cs, Edge3 genetrix) : base (id, trims, cs) {
      Genetrix = genetrix;
      if (Genetrix.IsOnXYPlane) mFlags |= E3Flags.FlatGenetrix;
      mFlags |= E3Flags.VLinear;
   }
   E3SweptSurface () => Genetrix = null!;

   public readonly Edge3 Genetrix;

   protected override Point3 GetPointCanonical (double u, double v) => Genetrix.GetPoint (u).Moved (0, 0, v);

   protected override Vector3 GetNormalCanonical (Point2 pt) => throw new NotImplementedException ();
   protected override Point2 GetUVCanonical (Point3 pt) => Point2.Zero; // This is never used since we override GetUV

   public override Bound2 ComputeDomain () => new Bound2 (Genetrix.Domain, new Bound1 (0, 100));

   public override Point2 GetUV (Point3 pt) => (_unlofter ??= new (this)).GetUV (pt);
   Unlofter? _unlofter;
}
#endregion

#region class E3Torus ------------------------------------------------------------------------------
/// <summary>A Torus is defined in the XY plane and lofted into a given coordinate system</summary>
public sealed class E3Torus : E3CSSurface {
   public E3Torus (int id, IEnumerable<Contour3> trims, CoordSystem cs, double rmajor, double rminor) : base (id, trims, cs) {
      RMajor = rmajor; RMinor = rminor;
   }
   E3Torus () { }

   public readonly double RMajor;
   public readonly double RMinor;

   /// <summary>Converts the given U,V coordinate into a 3D point on the surface of the Torus</summary>
   /// The Torus is generated by rotating a circle initially centered at (RMajor,0,0)
   /// and aligned in the XZ plane, about the Z axis. 
   /// - U is the position along the initial minor circle, with U=0 corresponding to
   ///   the point at (RMajor+RMinor, 0, 0) and moving CCW as viewed from +Y
   /// - V is the rotation of this generating minor circle about Z axis
   protected override Point3 GetPointCanonical (double u, double v) {
      var (sin, cos) = SinCos (u);     
      Point3 pos = new (RMajor + RMinor * cos, 0, RMinor * sin);
      return pos.Rotated (EAxis.Z, v);
   }

   /// <summary>See EvaluateCanonical for an explanation of the parametrization</summary>
   /// The normal computation follows from the same
   protected override Vector3 GetNormalCanonical (Point2 pt) {
      var (sin, cos) = SinCos (pt.X);
      return new Vector3 (cos, 0, sin).Rotated (EAxis.Z, pt.Y);
   }
   
   /// <summary>See EvaluateCanonical above for a description of the parametrization</summary>
   protected override Point2 GetUVCanonical (Point3 pt) {
      // First, look from above to figure out the V value (the rotation of the
      // generating circle about Z axis)
      double v = Atan2 (pt.Y, pt.X);
      pt = pt.Rotated (EAxis.Z, -v);      // Now, rotate the generating circle to its canonical orientation (v = 0)
      double u = Atan2 (pt.Z, pt.X - RMajor);
      return new (u, v);
   }
}
#endregion
