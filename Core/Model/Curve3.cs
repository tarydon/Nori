// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Curve3.cs
// ║║║║╬║╔╣║ Implements Curve3 and various types of derived curve
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
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
   protected Curve3 () { }
   protected Curve3 (int pairId) => PairId = pairId;

   // Properties ---------------------------------------------------------------
   public abstract Bound1 Domain { get; }

   /// <summary>Get the start point of the curve</summary>
   public abstract Point3 Start { get; }
   /// <summary>Get the end point of the curve</summary>
   public abstract Point3 End { get; }

   /// <summary>Is this curve lying on the XY plane?</summary>
   public abstract bool IsOnXYPlane { get; }
   /// <summary>Is this curve lying on the XZ plane?</summary>
   public abstract bool IsOnXZPlane { get; }

   /// <summary>If non-zero, this is the pair-ID of this edge</summary>
   /// In each fully connected manifold model, there are exactly two edges with
   /// the same pair-ID. These are the two co-edges on two adjacent faces that are
   /// touching each other. If these two edges are E1 and E2, then 
   /// E1.Start==E2.End, and E1.End==E2.Start, and they run against each other. 
   public readonly int PairId;

   // Methods ------------------------------------------------------------------
   public abstract Point3 GetPoint (double t);

   public virtual Vector3 GetTangent (double t) {
      var d = Domain;
      double dt = d.Length / 50;
      double t0 = d.Clamp (t - dt), t1 = d.Clamp (t + dt);
      return (GetPoint (t1) - GetPoint (t0)).Normalized ();
   }

   /// <summary>Returns the point at a given parameter value T along the curve</summary>
   public abstract double GetT (Point3 pt);

   /// <summary>Returns a PiecewiseLinear approximation of this curve</summary>
   /// 1. The curve is approximated with the given error threshold
   /// 2. The End point of the curve is not included (it is effectively the start
   ///    point of the next Edge in the sequence
   public abstract void Discretize (List<Point3> pts, double tolerance, double maxAngStep);

   /// <summary>Returns a copy of this curve transformed by the given transform</summary>
   public abstract Curve3 Xformed (Matrix3 xfm);

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{GetType ().Name} PairID={PairId}";
}
#endregion

#region class Line3 --------------------------------------------------------------------------------
/// <summary>Line3 implements a linear edge between two points</summary>
public sealed class Line3 : Curve3 {
   // Constructors -------------------------------------------------------------
   Line3 () { }
   public Line3 (int pairId, Point3 start, Point3 end) : base (pairId) => (mStart, mEnd) = (start, end);

   // Properties ---------------------------------------------------------------
   public override Bound1 Domain => new (0, 1);

   /// <summary>Start point of the line</summary>
   public override Point3 Start => mStart;
   readonly Point3 mStart;

   /// <summary>End point of the line</summary>
   public override Point3 End => mEnd;
   readonly Point3 mEnd;

   public override bool IsOnXYPlane => mStart.Z.IsZero () && mEnd.Z.IsZero ();
   public override bool IsOnXZPlane => mStart.Y.IsZero (Lib.Delta) && mEnd.Y.IsZero (Lib.Delta);

   /// <summary>Length of the line</summary>
   public double Length => Start.DistTo (End);

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize just adds the start point of the Line3</summary>
   /// The convention is that the end point is never included (it is added as part of the next curve)
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) 
      => pts.Add (Start);

   /// <summary>Gets the point at a given lie</summary>
   public override Point3 GetPoint (double lie) => lie.Along (mStart, mEnd);

   public override Vector3 GetTangent (double t) => (mEnd - mStart).Normalized ();

   public override double GetT (Point3 pt) => pt.GetLieOn (mStart, mEnd);

   /// <summary>Transformed copy of this line</summary>
   public override Line3 Xformed (Matrix3 xfm) => new (PairId, mStart * xfm, mEnd * xfm);

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{base.ToString ()} Len={Start.DistTo (End).Round (2)}";
}
#endregion

#region class Ellipse3 -----------------------------------------------------------------------------
public sealed class Ellipse3 : Curve3 {
   Ellipse3 () { }
   public Ellipse3 (int pairId, CoordSystem cs, double xRadius, double yRadius, double ang0, double ang1) : base (pairId) {
      Debug.Assert (Ang1 >= Ang0);
   }

   public readonly CoordSystem CS;
   public readonly double XRadius;
   public readonly double YRadius;
   public readonly double Ang0;
   public readonly double Ang1;

   public override Bound1 Domain => new (Ang0, Ang1);

   public override Point3 Start => GetPoint (Ang0);
   public override Point3 End => GetPoint (Ang1);

   public override bool IsOnXYPlane 
      => CS.Org.Z.IsZero () && Math.Abs (CS.VecZ.Z).EQ (1);

   public override bool IsOnXZPlane
      => CS.Org.Y.IsZero (Lib.Delta) && Math.Abs (CS.VecZ.Y).EQ (1, Lib.Delta);

   public override Point3 GetPoint (double lie) {
      var (sin, cos) = Math.SinCos (lie.Along (Ang0, Ang1));
      return CS.Org + CS.VecX * (XRadius * cos) + CS.VecY * (YRadius * sin);
   }

   public override double GetT (Point3 pt) {      
      pt *= (mFrom ??= Matrix3.From (CS));
      double ang = Math.Atan2 (pt.Y, pt.X);
      while (ang < Ang0) ang += Lib.TwoPI;
      return ang.GetLieOn (Ang0, Ang1);
   }
   Matrix3? mFrom;

   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      double angSpan = Ang1 - Ang0;
      int n = Lib.GetArcSteps ((XRadius + YRadius) / 2, angSpan, tolerance, maxAngStep);
      if (angSpan.EQ (Lib.TwoPI) && n.IsOdd ()) n++;
      for (int i = 0; i < n; i++) pts.Add (GetPoint ((double)i / n));
   }

   public override Ellipse3 Xformed (Matrix3 xfm) 
      => new (PairId, CS * xfm, XRadius, YRadius, Ang0, Ang1);
}
#endregion

#region class Arc3 ---------------------------------------------------------------------------------
/// <summary>Arc3 represents a section of a circular arc in space</summary>
/// The Arc3 is defined on the XY plane - always clockwise, with the center at the
/// origin and winding counter-clockwise about the Z axis. Then, it is lofted into space
/// using the specified coord-system CS.
public class Arc3 : Curve3 {
   // Constructors -------------------------------------------------------------
   Arc3 () { }
   public Arc3 (int pairId, CoordSystem cs, double radius, double angSpan) : base (pairId)
      => (CS, Radius, AngSpan) = (cs, radius, angSpan);

   // Properties ---------------------------------------------------------------
   /// <summary>Angular span of the arc (in radians)</summary>
   /// The arc always starts at the point (Radius, 0, 0) in its local
   /// coordinate system, so just the angular span is enough to specify the extent
   [Radian]
   public readonly double AngSpan;

   public override Bound1 Domain => new (0, 1);

    /// <summary>The coordinate system in which the Arc3 is defined</summary>
   /// In this CS, the arc lies in the XY plane, and winds CCW about the Z
   /// axis. The start point of the arc is along the X axis and the angular span
   /// is in radians. So, with an angular span of +PI/2, the arc will finish
   /// at (0, Radius, 0), or exactly on the Y axis
   public readonly CoordSystem CS;

   /// <summary>Returns the center point of the arc</summary>
   public Point3 Center => CS.Org;

   /// <summary>Radius of the arc</summary>
   public readonly double Radius;

   public override bool IsOnXYPlane
      => CS.Org.Z.IsZero () && Math.Abs (CS.VecZ.Z).EQ (1);

   public override bool IsOnXZPlane
      => CS.Org.Y.IsZero (Lib.Delta) && Math.Abs (CS.VecZ.Y).EQ (1, Lib.Delta);

   public override Point3 End => GetPoint (1);

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
      for (int i = 0; i < n; i++) pts.Add (GetPoint ((double)i / n));
   }

   /// <summary>Returns the point at a given lie</summary>
   public override Point3 GetPoint (double lie) {
      var (sin, cos) = Math.SinCos (AngSpan * lie);
      return CS.Org + CS.VecX * (Radius * cos) + CS.VecY * (Radius * sin);
   }

   public override Vector3 GetTangent (double t) {
      var (sin, cos) = Math.SinCos (AngSpan * t);
      return CS.VecX * -sin + CS.VecY * cos;
   }

   public override double GetT (Point3 pt) {
      pt *= (mFrom ??= Matrix3.From (CS));
      double ang = Math.Atan2 (pt.Y, pt.X);
      if (ang < 0) ang += Lib.TwoPI;
      return ang / AngSpan;
   }
   Matrix3? mFrom;

   public override Arc3 Xformed (Matrix3 xfm)
      => new (PairId, CS * xfm, Radius, AngSpan);

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{base.ToString ()} R={Radius.Round (2)} Span={AngSpan.R2D ().Round (1)}\u00b0";
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

   public override Bound1 Domain => new (mImp.Knot[0], mImp.Knot[^1]);

   /// <summary>Endpoint of the NurbsCurve</summary>
   public override Point3 End => Ctrl[^1];

   /// <summary>Is this a rational spline (not all weights are set to 1)</summary>
   public readonly bool Rational;

   /// <summary>Start point of the NurbsCurve</summary>
   public override Point3 Start => Ctrl[0];

   public override bool IsOnXYPlane => Ctrl.All (a => a.Z.EQ (0));

   public override bool IsOnXZPlane => Ctrl.All (a => a.Y.IsZero (Lib.Delta));

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
      pts.Clear ();

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

   public override double GetT (Point3 pt) => (_unlofter = new CurveUnlofter (this)).GetT (pt);
   CurveUnlofter? _unlofter;

   public override NurbsCurve3 Xformed (Matrix3 xfm)
      => new (PairId, [.. Ctrl.Select (a => a * xfm)], mImp.Knot, Weight);

   // Nested types -------------------------------------------------------------
   // Node is an internal struct used during discretization of a spline
   struct Node {
      public Point3 Pt;
      public double A;
      public int Level;
   }
}
#endregion

#region class Polyline3 -------------------------------------------------------------------------------
/// <summary>Represents a PWL segment</summary>
public class Polyline3 : Curve3 {
   // Constructors -------------------------------------------------------------
   Polyline3 () { }
   public Polyline3 (int pairId, ImmutableArray<Point3> pts) : base (pairId) => Pts = pts;

   // Properties ---------------------------------------------------------------
   public readonly ImmutableArray<Point3> Pts;
   public double Length => _length < 0 ? (_length = ComputeLength ()) : _length;
   double _length = -1;

   // Curve3 Implementation ----------------------------------------------------
   public override Point3 Start => Pts[0];
   public override Point3 End => Pts[^1];

   public override Bound1 Domain => new (0, Pts.Length - 1);

   public override void Discretize (List<Point3> pts, double _, double __) 
      => pts.AddRange (Pts);

   public override Point3 GetPoint (double lie) {
      if (lie < Lib.Epsilon) return Start;
      else if (lie >= Pts.Length - 1 - Lib.Epsilon) return End;
      int n = (int)lie;
      return (lie - n).Along (Pts[n], Pts[n + 1]);
   }

   public override double GetT (Point3 pt) {
      int iBest = 0;
      double minDist = double.MaxValue;
      for (int i = 0; i < Pts.Length - 1; i++) {
         double d = pt.DistToLineSeg (Pts[i], Pts[i + 1]);
         if (d < minDist) (minDist, iBest) = (d, i);
      }
      return pt.GetLieOn (Pts[iBest], Pts[iBest + 1]) + iBest;
   }

   public override bool IsOnXYPlane => Pts.All (a => a.Z.EQ (0));

   public override bool IsOnXZPlane => Pts.All (a => a.Y.IsZero (Lib.Delta));

   public override Polyline3 Xformed (Matrix3 xfm)
      => new (PairId, [.. Pts.Select (a => a * xfm)]);

   // Implementation -----------------------------------------------------------
   double ComputeLength () {
      double totalLength = 0;
      for (int i = 1; i < Pts.Length; i++)
         totalLength += Pts[i - 1].DistTo (Pts[i]);
      return totalLength;
   }
}
#endregion

#region class Contour3 -----------------------------------------------------------------------------
/// <summary>Contour3 is a collection of Curve3 connected end-to-end</summary>
/// Typically surfaces are bounded by a set of Contour3
public class Contour3 {
   // Constructors -------------------------------------------------------------
   Contour3 () { }
   public Contour3 (ImmutableArray<Curve3> edges) => mCurves = edges;

   // Properties ---------------------------------------------------------------
   /// <summary>The list of Curve3  in this Contour3</summary>
   public ImmutableArray<Curve3> Curves => mCurves;
   readonly ImmutableArray<Curve3> mCurves;

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize this Contour3 into a piecewise-linear approximation</summary>
   public void Discretize (List<Point3> pts, double tolerance, double maxAngStep)
      => mCurves.ForEach (e => e.Discretize (pts, tolerance, maxAngStep));

   /// <summary>Project the Contour3 into a</summary>
   public Poly Flatten (CoordSystem cs, double tolerance, double maxAngStep) {
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
}
#endregion
