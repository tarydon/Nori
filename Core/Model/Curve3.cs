// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Curve3.cs
// ║║║║╬║╔╣║ Implements Curve3 and various types of derived curve
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
namespace Nori;

#region class Curve3 -------------------------------------------------------------------------------
/// <summary>Base class for various types of Curve3</summary>
/// A Curve3 represents a parametric curve in 3D space. Most Curve3 are found embedded
/// in Contour3 objects, which form the boundaries of BREP surfaces. An important property
/// of a Curve3 is the PairId, through which it gets connected to a corresponding Curve3 
/// on the 'other side' of an edge of a model. In a closed manifold object, there will be
/// exactly two Curve3 with a particular PairId. 
public abstract class Curve3 {
   // Constructors -------------------------------------------------------------
   protected Curve3 (int pairId) => PairId = pairId;
   protected Curve3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the domain of the Curve</summary>
   public abstract Bound1 Domain { get; }

   /// <summary>Get the end point of the curve</summary>
   public abstract Point3 End { get; }

   /// <summary>Is this curve lying on the XY plane?</summary>
   public abstract bool IsOnXYPlane { get; }
   /// <summary>Is this curve lying on the XZ plane?</summary>
   public abstract bool IsOnXZPlane { get; }

   /// <summary>Get the start point of the curve</summary>
   public abstract Point3 Start { get; }

   /// <summary>Returns the point at the middle of the domain</summary>
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   public Point3 Midpoint => GetPoint (Domain.Mid);

   /// <summary>If non-zero, this is the pair-ID of this edge</summary>
   /// In each fully connected manifold model, there are exactly two edges with
   /// the same pair-ID. These are the two co-edges on two adjacent faces that are
   /// touching each other. If these two edges are E1 and E2, then 
   /// E1.Start==E2.End, and E1.End==E2.Start, and they run against each other. 
   public readonly int PairId;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns a PiecewiseLinear approximation of this curve</summary>
   /// 1. The curve is approximated with the given error threshold
   /// 2. In addition, the maxAngStep value is used to provide a maximum turn angle
   ///    that cannot be exceeded (this really works only for a smooth curve like an
   ///    arc or NURB with at least 2nd order continuity)
   /// Derived classes MUST implement this
   public abstract void Discretize (List<Point3> pts, double tolerance, double maxAngStep);

   /// <summary>Returns a new curve flipped in direction</summary>
   public abstract Curve3 Flipped ();

   /// <summary>Evaluates the point at a given parameter value t</summary>
   /// Derived classes MUST implement this 
   public abstract Point3 GetPoint (double t);

   /// <summary>Evaluates the tangent at a given parameter value t</summary>
   /// Derived classes CAN implement this for optimizing, otherwise this default
   /// implementation approximates the tangent using two evaluations 
   public virtual Vector3 GetTangent (double t) {
      var d = Domain;
      double dt = d.Length / 50;
      double t0 = d.Clamp (t - dt), t1 = d.Clamp (t + dt);
      return (GetPoint (t1) - GetPoint (t0)).Normalized ();
   }

   /// <summary>Returns the point at a given parameter value T along the curve</summary>
   /// Derived clases MUST implement this
   public abstract double GetT (Point3 pt);

   /// <summary>Trim the curve at the given parameters and return a new curve.</summary>
   /// <param name="t1">Starting point for the trim</param>
   /// <param name="t2">Ending point for the trim</param>
   /// <param name="reverseDir">For cyclical curves (circle, ellipse) indicates if the direction
   /// sense must be reversed. Ignore for non-cyclical curves.</param>
   /// <returns>A new trimmed curve.</returns>
   public abstract Curve3 Trimmed (double t1, double t2, bool reverseDir);

   /// <summary>Returns a copy of this curve transformed by the given transform</summary>
   public static Curve3 operator * (Curve3 curve, Matrix3 xfm) => curve.Xformed (xfm);

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"{GetType ().Name} ID={PairId}, {Start} to {End}";

   // Override this in each derived Curve3
   protected abstract Curve3 Xformed (Matrix3 xfm);
}
#endregion

#region class Arc3 ---------------------------------------------------------------------------------
/// <summary>Arc3 represents a section of a circular arc in space</summary>
/// The Arc3 is defined on the XY plane - always clockwise, with the center at the
/// origin and winding counter-clockwise about the Z axis. Then, it is lofted into space
/// using the specified coord-system CS.
public class Arc3 : Curve3 {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an Arc3</summary>
   /// The Arc3 always starts at the point (Radius,0,0) in the canonical space, before
   /// being transformed into the final CS. 
   public Arc3 (int pairId, CoordSystem cs, double radius, double angSpan) : base (pairId)
      => (CS, Radius, AngSpan) = (cs, radius, angSpan);
   Arc3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Angular span of the arc (in radians)</summary>
   /// The arc always starts at the point (Radius, 0, 0) in its local
   /// coordinate system, so just the angular span is enough to specify the extent
   [Radian]
   public readonly double AngSpan;

   /// <summary>The coordinate system in which the Arc3 is defined</summary>
   /// In this CS, the arc lies in the XY plane, and winds CCW about the Z
   /// axis. The start point of the arc is along the X axis and the angular span
   /// is in radians. So, with an angular span of +PI/2, the arc will finish
   /// at (0, Radius, 0), or exactly on the Y axis
   public readonly CoordSystem CS;

   /// <summary>Returns the center point of the arc</summary>
   public Point3 Center => CS.Org;

   /// <summary>The domain of an arc is always in radians</summary>
   public override Bound1 Domain => new (0, AngSpan);

   /// <summary>Get the endpoint of the Arc</summary>
   public override Point3 End => GetPoint (AngSpan);

   /// <summary>Radius of the arc</summary>
   public readonly double Radius;

   /// <summary>Check if the arc is on the XY plane</summary>
   public override bool IsOnXYPlane => CS.Org.Z.IsZero (Lib.Delta) && Math.Abs (CS.VecZ.Z).EQ (1, Lib.Delta);
   /// <summary>Check if the arc is on the XZ plane</summary>
   public override bool IsOnXZPlane => CS.Org.Y.IsZero (Lib.Delta) && Math.Abs (CS.VecZ.Y).EQ (1, Lib.Delta);

   /// <summary>Start point of the Arc3 (is always at (Radius,0,0) in the local coordinate system)</summary>
   public override Point3 Start => CS.Org + CS.VecX * Radius;

   // Methods ------------------------------------------------------------------
   /// <summary>Converts Arc3 to a piecewise-linear approximation</summary>
   /// <param name="pts">Points are added to this list</param>
   /// <param name="tolerance">Linear tolerance - the chords formed by the PWL approximation
   /// don't deviate from the original arc by more than this</param>
   /// <param name="maxAngStep">Angular tolerance - successive chords in the PWL approximation
   /// don't have a turn angle more than this between them</param>
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      int n = Lib.GetArcSteps (Radius, AngSpan, tolerance, maxAngStep);
      if (AngSpan.EQ (Lib.TwoPI) && n.IsOdd ()) n++;
      for (int i = 0; i <= n; i++) pts.Add (GetPoint ((double)i / n * AngSpan));
   }

   public override Curve3 Flipped () {
      Vector3 zaxis = CS.VecZ, xaxis = CS.VecX;
      xaxis = xaxis.Rotated (zaxis, true, AngSpan); // Alling X axis with the second point.
      return new Arc3 (PairId, new CoordSystem (CS.Org, xaxis, (-zaxis) * xaxis), Radius, AngSpan); // Flip the zaxis.
   }

   /// <summary>Returns the point at a given lie</summary>
   public override Point3 GetPoint (double t) {
      var (sin, cos) = Math.SinCos (t);
      return CS.Org + CS.VecX * (Radius * cos) + CS.VecY * (Radius * sin);
   }

   /// <summary>Returns the tangent a the given value of t</summary>
   public override Vector3 GetTangent (double t) {
      var (sin, cos) = Math.SinCos (t);
      return CS.VecX * -sin + CS.VecY * cos;
   }

   /// <summary>Computes the T value corresponding to the given point</summary>
   public override double GetT (Point3 pt) {
      pt *= (_from ??= Matrix3.From (CS));
      double ang = Math.Atan2 (pt.Y, pt.X);
      if (ang < 0) ang += Lib.TwoPI;
      return ang;
   }
   Matrix3? _from;

   public override Curve3 Trimmed (double t1, double t2, bool reverseDir) {
      t1 = Lib.NormalizeAngle (t1); t2 = Lib.NormalizeAngle (t2);
      if (t1 < 0) t1 += Lib.TwoPI; if (t2 < 0) t2 += Lib.TwoPI;
      Vector3 zaxis = CS.VecZ, xaxis = CS.VecX;
      if (reverseDir) {
         if (t1 < t2) {
            xaxis = xaxis.Rotated (zaxis, true, t1);
            return new Arc3 (PairId, new CoordSystem (CS.Org, xaxis, (-zaxis) * xaxis), Radius, Lib.TwoPI - (t2 - t1));
         } else {
            xaxis = xaxis.Rotated (zaxis, true, t2);
            return new Arc3 (PairId, new CoordSystem (CS.Org, xaxis, (-zaxis) * xaxis), Radius, t1 - t2);
         }
      } else {
         if (t1 < t2) {
            xaxis = xaxis.Rotated (zaxis, true, t1); // Allign X axis with the first point.
            return new Arc3 (PairId, new CoordSystem (CS.Org, xaxis, zaxis * xaxis), Radius, t2 - t1); // Update angle span.
         } else {
            xaxis = xaxis.Rotated (zaxis, true, t2);
            return new Arc3 (PairId, new CoordSystem (CS.Org, xaxis, zaxis * xaxis), Radius, Lib.TwoPI - (t1 - t2));
         }
      }
   }

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{base.ToString ()} R={Radius.Round (2)} Span={AngSpan.R2D ().Round (1)}\u00b0";

   // Transform the Arc3 by the given CS
   protected override Arc3 Xformed (Matrix3 xfm) => new (PairId, CS * xfm, Radius * xfm, AngSpan);
}
#endregion

#region class Ellipse3 -----------------------------------------------------------------------------
/// <summary>Ellipse3 implements a curve (canonical is on XY plane, projected into a space CS)</summary>
public sealed class Ellipse3 : Curve3 {
   // Constructors -------------------------------------------------------------
   /// <summary>Constructs an Ellipse3 given X & Y radii, start and end angles</summary>
   /// The ellipse is defined in the XY plane first and then lofted up into the final position
   /// using the given CS. The Ellipse always goes CCW from ang0 to ang1. To allow wrapping around
   /// the X axis (say 1.5Pi to 2.5Pi - negative Y to positive Y) ang1 can be greater than Lib.TwoPi.
   public Ellipse3 (int pairId, CoordSystem cs, double xRadius, double yRadius, double ang0, double ang1) : base (pairId) {
      CS = cs; XRadius = xRadius; YRadius = yRadius;
      Debug.Assert (ang1 >= ang0);
      while (ang0 < 0) ang0 += Lib.TwoPI;
      while (ang1 < ang0) ang1 += Lib.TwoPI;
      mDomain = new (ang0, ang1);
   }
   Ellipse3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>CoordSystem in which this Ellipse is defined</summary>
   public readonly CoordSystem CS;

   /// <summary>The domain of an Ellipse3 is in radians</summary>
   [Radian]
   public override Bound1 Domain => mDomain;
   Bound1 mDomain;

   /// <summary>End is the curve evaluated at the max limit of the domain</summary>
   public override Point3 End => GetPoint (mDomain.Max);

   /// <summary>Checks if the Ellipse3 is on the XY plane</summary>
   public override bool IsOnXYPlane => CS.Org.Z.IsZero () && Math.Abs (CS.VecZ.Z).EQ (1);
   /// <summary>Checks if the Ellipse3 is on the XZ plane</summary>
   public override bool IsOnXZPlane => CS.Org.Y.IsZero (Lib.Delta) && Math.Abs (CS.VecZ.Y).EQ (1, Lib.Delta);

   /// <summary>Start is the curve evaluated at the min limit of the domain</summary>
   public override Point3 Start => GetPoint (mDomain.Min);

   /// <summary>Radius of the ellipse along the X direction</summary>
   public readonly double XRadius;
   /// <summary>Radius of the ellipse along the Y direction</summary>
   public readonly double YRadius;

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize the arc with a given</summary>
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      double angSpan = mDomain.Length;
      int n = Lib.GetArcSteps ((XRadius + YRadius) / 2, angSpan, tolerance, maxAngStep);
      if (angSpan.EQ (Lib.TwoPI) && n.IsOdd ()) n++;
      for (int i = 0; i <= n; i++) pts.Add (GetPoint (((double)i / n).Along (mDomain)));
   }

   public override Curve3 Flipped () {
      var newCS = new CoordSystem (CS.Org, CS.VecX, -CS.VecY);
      return new Ellipse3 (PairId, newCS, XRadius, YRadius, Lib.TwoPI - mDomain.Max, Lib.TwoPI - mDomain.Min);
   }

   /// <summary>Returns the point at a given parameter value t (which is directly in domains)</summary>
   public override Point3 GetPoint (double t) {
      var (sin, cos) = Math.SinCos (t);
      return CS.Org + CS.VecX * (XRadius * cos) + CS.VecY * (YRadius * sin);
   }

   /// <summary>Returns the t value of a given point</summary>
   public override double GetT (Point3 pt) {
      pt *= (mFrom ??= Matrix3.From (CS));
      double ang = Math.Atan2 (pt.Y, pt.X);
      if (ang < 0) ang += Lib.TwoPI;
      return ang;
   }
   Matrix3? mFrom;

   public override Curve3 Trimmed (double t1, double t2, bool reverseDir) {
      t1 = Lib.NormalizeAngle (t1); t2 = Lib.NormalizeAngle (t2);
      if (t1 < 0) t1 += Lib.TwoPI; if (t2 < 0) t2 += Lib.TwoPI;
      if (reverseDir) return new Ellipse3 (PairId, CS, XRadius, YRadius, t2, t1).Flipped ();
      else return new Ellipse3 (PairId, CS, XRadius, YRadius, t1, t2);
   }

   /// <summary>Returns the Ellipse transformed by a given transform</summary>
   protected override Ellipse3 Xformed (Matrix3 xfm)
      => new (PairId, CS * xfm, XRadius * xfm, YRadius * xfm, mDomain.Min, mDomain.Max);
}
#endregion

#region class Line3 --------------------------------------------------------------------------------
/// <summary>Line3 implements a linear edge between two points</summary>
public sealed class Line3 : Curve3 {
   // Constructors -------------------------------------------------------------
   public Line3 (int pairId, Point3 start, Point3 end) : base (pairId) => (mStart, mEnd) = (start, end);
   Line3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Domain of a Line is 0..1</summary>
   public override Bound1 Domain => new (0, 1);

   /// <summary>End point of the line</summary>
   public override Point3 End => mEnd;
   readonly Point3 mEnd;

   /// <summary>Checks if the Line is on the XY plane</summary>
   public override bool IsOnXYPlane => mStart.Z.IsZero () && mEnd.Z.IsZero ();
   /// <summary>Checks if the line is on the XZ plane</summary>
   public override bool IsOnXZPlane => mStart.Y.IsZero (Lib.Delta) && mEnd.Y.IsZero (Lib.Delta);

   /// <summary>Length of the line</summary>
   public double Length => Start.DistTo (End);

   /// <summary>Start point of the line</summary>
   public override Point3 Start => mStart;
   readonly Point3 mStart;

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize just adds the start point of the Line3</summary>
   /// The convention is that the end point is never included (it is added as part of the next curve)
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      pts.Add (mStart); pts.Add (mEnd);
   }

   public override Curve3 Flipped () => new Line3 (PairId, mEnd, mStart);

   /// <summary>Gets the point at a given lie</summary>
   public override Point3 GetPoint (double lie) => lie.Along (mStart, mEnd);

   /// <summary>The tangent anywhere along a line is independent of the paramter value t</summary>
   public override Vector3 GetTangent (double t) => (mEnd - mStart).Normalized ();

   /// <summary>Returns the T value of a given point</summary>
   public override double GetT (Point3 pt) => pt.GetLieOn (mStart, mEnd);

   public override Curve3 Trimmed (double t1, double t2, bool _) =>
      new Line3 (PairId, GetPoint (t1), GetPoint (t2));

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"{base.ToString ()}, Len={Start.DistTo (End).Round (2)}";

   // Transformed copy of this line
   protected override Line3 Xformed (Matrix3 xfm) => new (PairId, mStart * xfm, mEnd * xfm);
}
#endregion

#region class NurbsCurve ---------------------------------------------------------------------------
/// <summary>Implements a 3 dimensional spline</summary>
/// This is a generalized spline - any order, rational or irrational
public class NurbsCurve3 : Curve3 {
   /// <summary>Construct a 3D spline given the control points, knot vector and the weights</summary>
   public NurbsCurve3 (int pairId, ImmutableArray<Point3> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight) : base (pairId) {
      mImp = new SplineImp (ctrl.Length, knot);
      Ctrl = ctrl; Weight = weight;
      Rational = !(weight.IsEmpty || weight.All (a => a.EQ (1)));
      if (!Rational) Weight = [];
   }
   NurbsCurve3 () => mImp = null!;
   readonly SplineImp mImp;

   // Properties ---------------------------------------------------------------
   /// <summary>The set of control points for this Spline</summary>
   public readonly ImmutableArray<Point3> Ctrl;

   /// <summary>The domain of the NurbsCurve is just from the first to last knot</summary>
   public override Bound1 Domain => new (mImp.Knot[0], mImp.Knot[^1]);

   /// <summary>Endpoint of the NurbsCurve</summary>
   public override Point3 End => Ctrl[^1];

   /// <summary>Check if the NurbsCurve is on the XY plane</summary>
   public override bool IsOnXYPlane => Ctrl.All (a => a.Z.EQ (0));
   /// <summary>Check if the NurbsCurve is on the XZ plane</summary>
   public override bool IsOnXZPlane => Ctrl.All (a => a.Y.IsZero (Lib.Delta));

   /// <summary>Is this a rational spline (not all weights are set to 1)</summary>
   public readonly bool Rational;

   /// <summary>Start point of the NurbsCurve</summary>
   public override Point3 Start => Ctrl[0];

   /// <summary>Returns the knot vector of the NurbsCurve</summary>
   public ImmutableArray<double> Knot => mImp.Knot;

   /// <summary>Weights attached to the control points</summary>
   /// If this array is empty, then this is a non-rational spline (all weights are 1)
   public readonly ImmutableArray<double> Weight;

   // Methods ------------------------------------------------------------------
   /// <summary>This discretizes the spline into a set of points (piecewise-linear approximation)</summary>
   /// The discetization is adaptive - it ensures that at no point does the error between
   /// the PWL approximation and the original spline curve exceed the given error threshold
   /// 'error'
   public override void Discretize (List<Point3> pts, double error, double maxAngStep) {
      // Set up for adaptive evaluation. We create a rough linear approximation by evaluating
      // the spline at each of the unique knot values, and we push these values of t (along with
      // their evaluated points) into a stack of Nodes
      var (knots, errSq, eval) = (mImp.Knot, error * error, new Stack<Node> ());

      double done = -1;
      foreach (var knot in knots.Reverse ()) {
         if (knot == done) continue;
         eval.Push (new Node { A = knot, Pt = GetPoint (knot), Level = 0 });
         done = knot;
      }

      // Now the recursive evaluation part - at each iteration of this loop, we pop off two
      // nodes from this stack to see if that linear span needs to be further subdivided.
      const int maxLevel = 5;
      while (eval.Count > 1) {
         Node e1 = eval.Pop (), e2 = eval.Peek ();

         // We want to see if the span between e1 and e2 needs to be further subdivided
         if (e1.Level < maxLevel) {
            double a = e1.A, b = e2.A, amid = (a + b) / 2;
            Point3 pmid = GetPoint (amid);
            // We evaluate points at 0.25, 0.5 and 0.75 of the knot values between e1 and e2.
            // If any of these evaluate points deviates from the straight line connecting e1 and e2
            // by more than the error threshold, then we need to further subdivide this segment into
            // two.
            bool subdivide = pmid.DistToLineSq (e1.Pt, e2.Pt) > errSq
               || GetPoint (0.25.Along (a, b)).DistToLineSq (e1.Pt, e2.Pt) > errSq
               || GetPoint (0.75.Along (a, b)).DistToLineSq (e1.Pt, e2.Pt) > errSq;
            if (subdivide) {
               // If we want to subdivide, we break down the span e1..e2 into two spans:
               // e1..emid and emid..e2 and push these on to the stack (note that we push emid first
               // since this is a 'stack'). Note that we are bumping up the level in these newly
               // pushed spans to avoid recursing too deep
               eval.Push (new Node { A = amid, Pt = pmid, Level = e1.Level + 1 });
               eval.Push (new Node { A = a, Pt = e1.Pt, Level = e1.Level + 1 });
               continue;
            }
         }

         // If we get to this point, we decided not to subdivide the segment e1..e2 so we
         // can just output the point p1
         pts.Add (e1.Pt);
      }
      // Finally, add the last point (endpoint) that still remains on the stack
      pts.Add (eval.Pop ().Pt);
   }

   public override Curve3 Flipped () {
      // We will have to adjust the knot values.
      double umin = Knot[0], umax = Knot[^1];
      var newKnots = ImmutableArray.CreateBuilder<double> (Knot.Length);
      for (int i = 0, m = Knot.Length; i < m; i++)
         newKnots.Add (umin + umax - Knot[m - i - 1]);

      return new NurbsCurve3 (PairId, [.. Ctrl.Reverse ()], newKnots.MoveToImmutable (), [.. Weight.Reverse ()]);
   }

   /// <summary>Evaluates the spline at a given knot value t</summary>
   public override Point3 GetPoint (double t) {
      if (t <= mImp.Knot[0]) return Ctrl[0];
      if (t >= mImp.Knot[^1]) return Ctrl[^1];
      while (mFactor.Value!.Length < mImp.Order)
         mFactor.Value = new double[mFactor.Value.Length * 2];

      double[] factor = mFactor.Value;
      int span = mImp.ComputeBasis (t, factor), p = mImp.Degree;
      double x = 0, y = 0, z = 0;
      if (Rational) {
         double wsum = 0;
         for (int j = 0; j <= p; j++) {
            int n = span - p + j;
            double weight = factor[j] * Weight[n];
            Point3 ctrl = Ctrl[n];
            x += ctrl.X * weight; y += ctrl.Y * weight; z += ctrl.Z * weight;
            wsum += weight;
         }
         return new (x / wsum, y / wsum, z / wsum);
      } else {
         for (int j = 0; j <= p; j++) {
            double weight = factor[j];
            Point3 ctrl = Ctrl[span - p + j];
            x += ctrl.X * weight; y += ctrl.Y * weight; z += ctrl.Z * weight;
         }
         return new (x, y, z);
      }
   }
   // This buffer is used to store the results of the SplineImp.ComputeBasis call.
   // To avoid allocating a buffer on each Evaluate call, we make this static. 
   // To then make it thread safe, we mark it as ThreadLocal (we grow this as the 
   // order of the Spline we're evalauting increases, and never shrink this buffer)
   static readonly ThreadLocal<double[]> mFactor = new (() => new double[8]);

   /// <summary>Return the T value corresponding to the given pt</summary>
   public override double GetT (Point3 pt) => (_unlofter = new CurveUnlofter (this)).GetT (pt);
   CurveUnlofter? _unlofter;

   // Nested types -------------------------------------------------------------
   // Node is an internal struct used during discretization of a spline
   struct Node {
      public Point3 Pt;
      public double A;
      public int Level;
   }

   public override Curve3 Trimmed (double t1, double t2, bool reverseDir) {
      throw new NotImplementedException ();
   }

   // Implementation -----------------------------------------------------------
   // Transforms the NurbsCurve3 by the given transform
   protected override NurbsCurve3 Xformed (Matrix3 xfm)
      => new (PairId, [.. Ctrl.Select (a => a * xfm)], mImp.Knot, Weight);
}
#endregion

#region class Polyline3 ----------------------------------------------------------------------------
/// <summary>Represents a PWL segment</summary>
public class Polyline3 : Curve3 {
   // Constructors -------------------------------------------------------------
   /// <summary>Create the Polyline3, given a set of pts</summary>
   public Polyline3 (int pairId, ImmutableArray<Point3> pts) : base (pairId) => Pts = pts;
   Polyline3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>The domain of the Polyline3 is 0 .. Pts.Length-1</summary>
   public override Bound1 Domain => new (0, Pts.Length - 1);

   /// <summary>The end of the Polyline3</summary>
   public override Point3 End => Pts[^1];

   /// <summary>Checks if the Polyline3 is on the XY plane</summary>
   public override bool IsOnXYPlane => Pts.All (a => a.Z.EQ (0));
   /// <summary>Checks if the Polyline3 is on the XZ plane</summary>
   public override bool IsOnXZPlane => Pts.All (a => a.Y.IsZero (Lib.Delta));

   /// <summary>The set of points making up the Polyline3</summary>
   public readonly ImmutableArray<Point3> Pts;

   /// <summary>The start of the Polyline3</summary>
   public override Point3 Start => Pts[0];

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize the Polyline3 just returns the set of points</summary>
   public override void Discretize (List<Point3> pts, double _, double __)
      => pts.AddRange (Pts);

   public override Curve3 Flipped () => new Polyline3 (PairId, [.. Pts.Reverse ()]);

   /// <summary>Gets the point at a given lie</summary>
   public override Point3 GetPoint (double lie) {
      if (lie < Lib.Epsilon) return Start;
      else if (lie >= Pts.Length - 1 - Lib.Epsilon) return End;
      int n = (int)lie;
      return (lie - n).Along (Pts[n], Pts[n + 1]);
   }

   /// <summary>Gets the T value for the given point</summary>
   public override double GetT (Point3 pt) {
      int iBest = 0;
      double minDist = double.MaxValue;
      for (int i = 0; i < Pts.Length - 1; i++) {
         double d = pt.DistToLineSeg (Pts[i], Pts[i + 1]);
         if (d < minDist) (minDist, iBest) = (d, i);
      }
      return pt.GetLieOn (Pts[iBest], Pts[iBest + 1]) + iBest;
   }

   public override Curve3 Trimmed (double t1, double t2, bool reverseDir) {
      throw new NotImplementedException ();
   }

   // Implementation -----------------------------------------------------------
   // Transform the Polyline3 by the given xfm
   protected override Polyline3 Xformed (Matrix3 xfm)
      => new (PairId, [.. Pts.Select (a => a * xfm)]);
}
#endregion

#region class Contour3 -----------------------------------------------------------------------------
/// <summary>Contour3 is a collection of Curve3 connected end-to-end</summary>
/// Typically surfaces are bounded by a set of Contour3
public class Contour3 {
   // Constructors -------------------------------------------------------------
   public Contour3 (ImmutableArray<Curve3> edges) => mCurves = edges;
   Contour3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>The list of Curve3  in this Contour3</summary>
   public ImmutableArray<Curve3> Curves => mCurves;
   readonly ImmutableArray<Curve3> mCurves;

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize this Contour3 into a piecewise-linear approximation</summary>
   public void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      for (int i = 0; i < mCurves.Length; i++) {
         mCurves[i].Discretize (pts, tolerance, maxAngStep);
         // The endpoint of curve N is also the start point of curve N + 1
         if (i < mCurves.Length - 1) pts.RemoveLast ();
      }
   }

   /// <summary>Project the Contour3 into a</summary>
   public Poly Flatten (CoordSystem cs) {
      var pb = PolyBuilder.It;
      var xfm = Matrix3.From (cs);
      foreach (var edge in mCurves) {
         switch (edge) {
            case Line3 line:
               pb.Line (Xfm (line.Start));
               break;
            case Arc3 arc:
               var (center, start, _) = (Xfm (arc.Center), Xfm (arc.Start), arc.Radius);
               var flags = (arc.CS.VecZ * xfm).Z > 0 ? Poly.EFlags.CCW : Poly.EFlags.CW;
               if (arc.AngSpan.EQ (Lib.TwoPI)) {
                  pb.Arc (start, center, flags);
                  pb.Arc (center + (center - start), center, flags);
               } else
                  pb.Arc (start, center, flags);
               break;
            case Polyline3 poly:
               foreach (var pt in poly.Pts) pb.Line (Xfm (pt));
               break;
            default:
               throw new BadCaseException (edge);
         }
      }
      return pb.Close ().Build ();

      // Helpers ...........................................
      Point2 Xfm (Point3 pt) {
         pt *= xfm; Lib.Check (pt.Z.IsZero (0.001), "Non-planar contour");
         return new (pt.X, pt.Y);
      }
   }

   // Operators ------------------------------------------------------------------------------------
   /// <summary>Transform the Contour3 by the given transform</summary>
   public static Contour3 operator * (Contour3 con, Matrix3 xfm)
      => new ([.. con.Curves.Select (a => a * xfm)]);
}
#endregion
