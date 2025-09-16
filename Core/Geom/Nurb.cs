// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Nurb.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Spline -------------------------------------------------------------------------------
/// <summary>Spline is the abstract base class for Spline2 (2-D) and Spline3 (3-D)</summary>
/// It implements the code common to 2D and 3D splines - evaluating the NURB basis
/// functions at a given value of the paramter t.
public abstract class Spline {
   public Spline (int cCtrl, ImmutableArray<double> knot, ImmutableArray<double> weight) {
      (mNodes, Knot, Weight) = (cCtrl, knot, weight);
      Rational = !(weight.IsEmpty || weight.All (a => a.EQ (1)));
      if (!Rational) Weight = [];
      int n = Degree + 1;
      _val = new double[n]; _left = new double[n]; _right = new double[n];
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The degree of teh spline</summary>
   /// Note that the ORDER of the spline is DEGREE + 1
   public int Degree => Knot.Length - mNodes - 1;

   /// <summary>The knot vector for this spline</summary>
   public readonly ImmutableArray<double> Knot;

   /// <summary>Is this a rational spline (not all weights are set to 1)</summary>
   public readonly bool Rational;

   /// <summary>Weights attached to the control points</summary>
   /// If this array is empty, then this is a non-rational spline (all weights are 1)
   public readonly ImmutableArray<double> Weight;

   // Methods ------------------------------------------------------------------
   // Computes the basis functions for a given knot value t.
   //
   // For a given value t, there is only a subset of control points that actually
   // contribute - this is known as the span of interest (see Algorithm A2.1 from
   // the "Nurbs Book'). That is the return value from this function.
   //
   // For the span of interest, this function computes the factors that must be used
   // to multiply each control point by and stores those in the _val array. (Since these
   // are basis functions, the sum of those weights adds up to 1.0). This computation
   // of the basis functions is detailed in Algorithm A2.2 from the "NURBS Book"
   protected int ComputeBasis (double t) {
      // First find the span of interest in which this knot lies
      int n = mNodes - 1, span = n;
      if (t < Knot[n + 1]) {
         int low = Degree, high = n + 1; span = (low + high) / 2;
         for (; ; ) {
            if (t < Knot[span]) high = span;
            else if (t >= Knot[span + 1]) low = span;
            else break;
            span = (high + low) / 2;
            if (high == low) break;
         }
      }

      // Now, compute the basis functions for this span
      _val[0] = 1.0;
      int p = Degree;
      for (int j = 1; j <= p; j++) {
         _left[j] = t - Knot[span + 1 - j];
         _right[j] = Knot[span + j] - t;
         double saved = 0.0;
         for (int r = 0; r < j; r++) {
            double temp = _val[r] / (_right[r + 1] + _left[j - r]);
            _val[r] = saved + _right[r + 1] * temp;
            saved = _left[j - r] * temp;
         }
         _val[j] = saved;
      }
      return span;
   }

   // Private data -------------------------------------------------------------
   double[] _left, _right;
   protected double[] _val;
   int mNodes;
}
#endregion

#region class Spline2
/// <summary>Implements a 2 dimensional spline</summary>
public class Spline2 : Spline {
   public Spline2 (ImmutableArray<Point2> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight) : base (ctrl.Length, knot, weight)
      => Ctrl = ctrl;

   // Properties ---------------------------------------------------------------
   /// <summary>The set of control points for this Spline</summary>
   public readonly ImmutableArray<Point2> Ctrl;

   // Methods ------------------------------------------------------------------
   /// <summary>This discretizes the spline into a set of points (piecewise-linear approximation)</summary>
   /// The discetization is adaptive - it ensures that at no point does the error between
   /// the PWL approximation and the original spline curve exceed the given error threshold
   /// 'error'
   public void Discretize (List<Point2> pts, double error) {
      pts.Clear ();

      // Set up for adaptive evaluation. We create a rough linear approximation by evaluating
      // the spline at an set of equidistant spaced t values (we use Ctrl.Length + 1 values),
      // and push these values of t (along with their evaluated points) into a stack of Nodes.
      Stack<Node> eval = [];
      double start = Knot[0], end = Knot[^1], errSq = error * error;

      double done = -1;
      for (int i = 0; i < Knot.Length; i++) {
         var knot = Knot[i]; if (knot == done) continue;
         int aa = ComputeBasis (knot);
         eval.Push (new Node { A = knot, Pt = Evaluate (knot), Level = 0 });
         done = knot;
      }

      //for (int i = Ctrl.Length; i >= 0; i--) { // REMOVETHIS
      //   double a = ((double)i / Ctrl.Length).Along (start, end);
      //   eval.Push (new Node { A = a, Pt = Evaluate (a), Level = 0 });
      //}

      // Now the recursive evaluation part - at each iteration of this loop, we pop off two
      // nodes from this stack to see if that linear span needs to be further subdivided.
      int maxLevel = 5;
      while (eval.Count > 1) {
         Node e1 = eval.Pop (), e2 = eval.Peek ();

         // We want to see if the span between e1 and e2 needs to be further subdivided
         if (e1.Level < maxLevel) {
            double a = e1.A, b = e2.A, amid = (a + b) / 2;
            Point2 pmid = Evaluate (amid);
            // We evaluate points at 0.25, 0.5 and 0.75 of the knot values between e1 and e2.
            // If any of these evaluate points deviates from the straight line connecting e1 and e2
            // by more than the error threshold, then we need to further subdivide this segment into
            // two.
            bool subdivide = pmid.DistToLineSq (e1.Pt, e2.Pt) > errSq
               || Evaluate (0.25.Along (a, b)).DistToLineSq (e1.Pt, e2.Pt) > errSq
               || Evaluate (0.75.Along (a, b)).DistToLineSq (e1.Pt, e2.Pt) > errSq;
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
   public Point2 Evaluate (double t) {
      if (t <= Knot[0]) return Ctrl[0];
      if (t >= Knot[^1]) return Ctrl[^1];
      int span = ComputeBasis (t), p = Degree;

      double x = 0, y = 0;
      if (Rational) {
         double wsum = 0;
         for (int j = 0; j <= p; j++) {
            int n = span - p + j;
            double weight = _val[j] * Weight[n];
            Point2 ctrl = Ctrl[n];
            x += ctrl.X * weight; y += ctrl.Y * weight;
            wsum += weight;
         }
         return new (x / wsum, y / wsum);
      } else {
         for (int j = 0; j <= p; j++) {
            double weight = _val[j];
            Point2 ctrl = Ctrl[span - p + j];
            x += ctrl.X * weight; y += ctrl.Y * weight;
         }
         return new (x, y);
      }
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Creates a new Spline2 by applying the transformation matrix</summary>
   public static Spline2 operator * (Spline2 s, Matrix2 xfm)
      => new ([.. s.Ctrl.Select (a => a * xfm)], s.Knot, s.Weight);

   // Nested types -------------------------------------------------------------
   // Node is an internal struct used during discretization of a spline
   struct Node {
      public double A;
      public Point2 Pt;
      public int Level;
   }
}
#endregion
