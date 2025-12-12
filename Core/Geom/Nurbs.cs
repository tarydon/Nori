// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Nurbs.cs
// ║║║║╬║╔╣║ Implements NURBS curves and surfaces
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
namespace Nori;

#region class SplineImp-----------------------------------------------------------------------------
/// <summary>Helper that does basis-function evaluation for spline curves and surfaces</summary>
/// A NURBS curve contains an embedded SplineImp that holds onto the knot vector and can then 
/// evaluate the basis functions at a given value of t. A NURBS surface contains two SplineImp,
/// one along U and one along V - these are used to evalaute the point at a given value of u 
/// and v
public class SplineImp {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a SplineImp, given the number of control points and the knot vector</summary>
   public SplineImp (int cCtrl, ImmutableArray<double> knot) 
      => (Knot, mNodes) = (knot, cCtrl);
   SplineImp () => Knot = [];

   // Properties ---------------------------------------------------------------
   /// <summary>The degree of the NURBS curve</summary>
   public int Degree => Knot.Length - mNodes - 1;
   readonly int mNodes;

   /// <summary>The knot vector</summary>
   public readonly ImmutableArray<double> Knot;

   /// <summary>The order of the NURBS curve</summary>
   public int Order => Knot.Length - mNodes;

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
   public int ComputeBasis (double t, double[] result, int span = -1) {
      int order = Knot.Length - mNodes;
      if (result.Length < order)
         throw new Exception ($"Result buffer must have length >= {order}");
      while (mBuffer.Value!.Length < 2 * order) 
         mBuffer.Value = new double[mBuffer.Value!.Length * 2];
      Span<double> left = mBuffer.Value.AsSpan (0, order), right = mBuffer.Value.AsSpan (order, order);

      // First find the span of interest in which this knot lies
      if (span == -1) {
         int n = span = mNodes - 1; 
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
      }

      // Now, compute the basis functions for this span
      result[0] = 1.0;
      int p = Degree;
      for (int j = 1; j <= p; j++) {
         left[j] = t - Knot[span + 1 - j];
         right[j] = Knot[span + j] - t;
         double saved = 0.0;
         for (int r = 0; r < j; r++) {
            double temp = result[r] / (right[r + 1] + left[j - r]);
            result[r] = saved + right[r + 1] * temp;
            saved = left[j - r] * temp;
         }
         result[j] = saved;
      }
      return span;
   }
   // This buffer is used to maintain the 'left' and 'right' vectors used to compute
   // the basis functions. Both of these vectors are packed successively into this
   // mBuffer.
   // 1. This is a thread-static buffer so we don't allocate a new buffer for each
   //    evaluate, while retaining thread safety
   // 2. Placing both the left and right into the same array improves locality of
   //    reference, and also keeps the number of TLS slots we use as small as possible
   static readonly ThreadLocal<double[]> mBuffer = new (() => new double[8]);
}
#endregion

#region class Spline2 ------------------------------------------------------------------------------
/// <summary>Implements a 2 dimensional spline</summary>
/// This is a generalized spline - any order, rational or irrational
public class Spline2 {
   /// <summary>Construct a 2D spline given the control points, knot vector and the weights</summary>
   public Spline2 (ImmutableArray<Point2> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight) {
      Imp = new SplineImp (ctrl.Length, knot);
      Ctrl = ctrl; Weight = weight;
      Rational = !(weight.IsEmpty || weight.All (a => a.EQ (1)));
      if (!Rational) Weight = [];
   }
   Spline2 () => Imp = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>The set of control points for this Spline</summary>
   public readonly ImmutableArray<Point2> Ctrl;

   /// <summary>The basis-function computer for this Spline</summary>
   public readonly SplineImp Imp;

   /// <summary>Is this a rational spline (not all weights are set to 1)</summary>
   public readonly bool Rational;

   /// <summary>Weights attached to the control points</summary>
   /// If this array is empty, then this is a non-rational spline (all weights are 1)
   public readonly ImmutableArray<double> Weight;

   // Methods ------------------------------------------------------------------
   /// <summary>This discretizes the spline into a set of points (piecewise-linear approximation)</summary>
   /// The discetization is adaptive - it ensures that at no point does the error between
   /// the PWL approximation and the original spline curve exceed the given error threshold
   /// 'error'
   public void Discretize (List<Point2> pts, double error) {
      pts.Clear ();

      // Set up for adaptive evaluation. We create a rough linear approximation by evaluating
      // the spline at each of the unique knot values, and we push these values of t (along with
      // their evaluated points) into a stack of Nodes
      var (knots, errSq, eval) = (Imp.Knot, error * error, new Stack<Node> ());

      double done = -1;
      foreach (var knot in knots) {
         if (knot == done) continue;
         eval.Push (new Node { A = knot, Pt = Evaluate (knot), Level = 0 });
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
      if (t <= Imp.Knot[0]) return Ctrl[0];
      if (t >= Imp.Knot[^1]) return Ctrl[^1];
      while (mFactor.Value!.Length < Imp.Order) 
         mFactor.Value = new double[mFactor.Value.Length * 2];

      double[] factor = mFactor.Value;
      int span = Imp.ComputeBasis (t, factor), p = Imp.Degree;
      double x = 0, y = 0;
      if (Rational) {
         double wsum = 0;
         for (int j = 0; j <= p; j++) {
            int n = span - p + j;
            double weight = factor[j] * Weight[n];
            Point2 ctrl = Ctrl[n];
            x += ctrl.X * weight; y += ctrl.Y * weight;
            wsum += weight;
         }
         return new (x / wsum, y / wsum);
      } else {
         for (int j = 0; j <= p; j++) {
            double weight = factor[j];
            Point2 ctrl = Ctrl[span - p + j];
            x += ctrl.X * weight; y += ctrl.Y * weight;
         }
         return new (x, y);
      }
   }
   // This buffer is used to store the results of the SplineImp.ComputeBasis call.
   // To avoid allocating a buffer on each Evaluate call, we make this static. 
   // To then make it thread safe, we mark it as ThreadLocal (we grow this as the 
   // order of the Spline we're evalauting increases, and never shrink this buffer)
   static readonly ThreadLocal<double[]> mFactor = new (() => new double[8]);

   // Operators ----------------------------------------------------------------
   /// <summary>Creates a new Spline2 by applying the transformation matrix</summary>
   public static Spline2 operator * (Spline2 s, Matrix2 xfm)
      => new ([.. s.Ctrl.Select (a => a * xfm)], s.Imp.Knot, s.Weight);

   // Nested types -------------------------------------------------------------
   // Node is an internal struct used during discretization of a spline
   struct Node {
      public Point2 Pt;
      public double A;
      public int Level;
   }
}
#endregion
