// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3Surface.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
namespace Nori;

#region class E3NurbsSurface -----------------------------------------------------------------------
/// <summary>Represents a NURBS surface (any order, rational or simple)</summary>
public sealed class E3NurbsSurface : E3Surface {
   // Constructors -------------------------------------------------------------
   public E3NurbsSurface (int id, ImmutableArray<Point3> ctrl, ImmutableArray<double> weight, int uCtl, ImmutableArray<double> uknots, ImmutableArray<double> vknots, ImmutableArray<Contour3> trims) : base (id, trims) {
      UCtl = uCtl; Ctrl = ctrl; Weight = weight;
      mUImp = new (uCtl, uknots); mVImp = new (VCtl, vknots);
      Rational = !(weight.IsEmpty || weight.All (a => a.EQ (1)));
      if (!Rational) Weight = [];
   }
   E3NurbsSurface () => mUImp = mVImp = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>The 2-dimensional grid of control points</summary>
   /// This is a 2D array, flattened. The total number of points here is UCtl x VCtl. 
   /// V is the index that varies fastest, so the linear index for (u, v) is (u * VCtl + v). 
   public readonly ImmutableArray<Point3> Ctrl;

   /// <summary>Is this a rational spline? (all weights set to 1)</summary>
   public readonly bool Rational;

   /// <summary>Number of 'columns' in the control point grid</summary>
   public readonly int UCtl;
   /// <summary>Number of 'rows' in the control point grid</summary>
   public int VCtl => Ctrl.Length / UCtl;

   /// <summary>The weights for the control points (if all are set to 1, this is a non-rational spline)</summary>
   public readonly ImmutableArray<double> Weight;

   // Overrides ----------------------------------------------------------------
   // Computes the domain of the NURBS surface (just the limits of the knots in U and V)
   protected override Bound2 ComputeDomain () 
      => new (mUImp.Knot[0], mVImp.Knot[0], mUImp.Knot[^1], mVImp.Knot[^1]);

   // Given a U,V computes a point on the NURBS surface. The SplineImps mUImp and mVImp
   // are the evaluators of the basis functions in U and V directions.
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

   /// <summary>Computes the U,V</summary>
   public override Point2 GetUV (Point3 pt) 
      => (_unlofter ??= new (this)).GetUV (pt);
   SurfaceUnlofter? _unlofter;

   // Returns a copy of the NURBsSurface transformed by the given matrix
   protected override E3NurbsSurface Xformed (Matrix3 xfm) {
      ImmutableArray<Point3> ctrl = [.. Ctrl.Select (a => a * xfm)];
      E3NurbsSurface nurb = new (Id, ctrl, Weight, UCtl, mUImp.Knot, mVImp.Knot, Contours * xfm);
      nurb.CopyMeshFrom (this, xfm);
      return nurb;
   }

   // Private data -------------------------------------------------------------
   readonly SplineImp mUImp, mVImp;
}
#endregion

#region class E3RuledSurface -----------------------------------------------------------------------
/// <summary>A RuledSurface is generaeted by connecting equi-parameter points on the bottom & top curves</summary>
/// 
/// Parametrization: 
/// - V is the parameter value (T) along the bottom and top curves
/// - U is the interpolation between these two points from 0 .. USpan , where
///   USpan is the average of [bottom.Start .. top.Start] and [bottom.End .. top.End]
public sealed class E3RuledSurface : E3Surface {
   // Constructors -------------------------------------------------------------
   public E3RuledSurface (int id, ImmutableArray<Contour3> trims, Curve3 bottom, Curve3 top) : base (id, trims) {
      (Bottom, Top) = (bottom, top);
      Lib.Check (bottom.Domain.EQ (top.Domain), "RuledSurface domains unequal");
      mUSpan = (Bottom.Start.DistTo (top.Start) + Bottom.End.DistTo (top.End)) / 2;
      mUSpan = Math.Max (mUSpan, 1);
   }
   E3RuledSurface () => Bottom = Top = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>Bottom generatrix curve</summary>
   public readonly Curve3 Bottom;
   /// <summary>Top generatrix curve</summary>
   public readonly Curve3 Top;

   // Overrides ----------------------------------------------------------------
   protected override Bound2 ComputeDomain ()
      => new (new (0, mUSpan), Bottom.Domain);

   public override Point3 GetPoint (double u, double v)
      => (u / mUSpan).Along (Bottom.GetPoint (v), Top.GetPoint (v));

   public override Point2 GetUV (Point3 pt3d)
      => (_unlofter = new (this)).GetUV (pt3d);
   SurfaceUnlofter? _unlofter;

   // Returns a copy of the RuledSurface, transformed by the given matrix
   protected override Ent3 Xformed (Matrix3 xfm) {
      E3RuledSurface ruled = new (Id, Contours * xfm, Bottom * xfm, Top * xfm);
      ruled.CopyMeshFrom (this, xfm);
      return ruled;
   }

   // Private data -------------------------------------------------------------
   double mUSpan;
}
#endregion
