namespace Nori;

public abstract class Spline<T> {
   public Spline (ImmutableArray<T> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight, T zero, Func<T, double, T> mult, Func<T, T, T> add, Func<T, T, T, double> distLineSq) {
      (Ctrl, Knot, Weight) = (ctrl, knot, weight);
      Rational = weight.IsEmpty || weight.All (a => a.EQ (1));
      if (!Rational) Weight = [];
      _N = new double[Degree + 1];
      _left = new double[_N.Length]; _right = new double[_N.Length];
      (mZero, mAdd, mMult, mDistLineSq) = (zero, add, mult, distLineSq);
   }

   /// <summary>
   /// Returns the degree of this curve
   /// </summary>
   /// Note that the ORDER of the curve is DEGREE + 1
   public int Degree => Knot.Length - Ctrl.Length - 1;

   public readonly bool Rational;

   public readonly ImmutableArray<T> Ctrl;
   public readonly ImmutableArray<double> Knot;
   public readonly ImmutableArray<double> Weight;

   // Implementation -----------------------------------------------------------
   // Given a knot value (t), this computes the span of interest and computes the
   // basis functions. This is from Algorithm A2.2 in "The Nurbs Book"
   public int ComputeBasis (double t) {
      if (t == _tCache) return _nCache;

      // Find the span of interest using a binary search
      int n = Ctrl.Length - 1, mid;
      if (t >= Knot[n + 1]) {
         mid = n;
      } else {
         int low = Degree, high = n + 1; mid = (low + high) / 2;
         while (t < Knot[mid] || t >= Knot[mid + 1]) {
            if (t < Knot[mid]) high = mid;
            else low = mid;
            mid = (high + low) / 2;
            if (high == low) break;
         }
      }

      // Now, compute the basis functions for this span
      _N[0] = 1.0;
      int p = Degree, span = mid;
      for (int j = 1; j <= p; j++) {
         _left[j] = t - Knot[span + 1 - j];
         _right[j] = Knot[span + j] - t;
         double saved = 0.0;
         for (int r = 0; r < j; r++) {
            double temp = _N[r] / (_right[r + 1] + _left[j - r]);
            _N[r] = saved + _right[r + 1] * temp;
            saved = _left[j - r] * temp;
         }
         _N[j] = saved;
      }
      _tCache = t; _nCache = mid;
      return mid;
   }
   // The cached value of t (from the previous ComputeBasis call)
   double _tCache = double.NaN;
   // The cached value of the span from that previous call
   int _nCache;
   double[] _N, _left, _right;

   struct Eval {
      public Eval (double a, T pt) => (A, Pt) = (a, pt);
      public readonly double A;
      public readonly T Pt;
   }

   public void Discretize (List<T> pts, double error) {
      pts.Clear ();
      Stack<Eval> eval = [];
      double start = Knot[0], end = Knot[^1], errSq = error * error;
      for (int i = Ctrl.Length; i >= 0; i--) {
         double a = ((double)i / Ctrl.Length).Along (start, end);
         eval.Push (new Eval (a, Evaluate (a)));
      }

      double[] lies = [0.25, 0.5, 0.75];
      T[] ptLies = new T[lies.Length];
      int maxLevel = 6, level = maxLevel; // Max times a span can be divided
      while (eval.Count > 1) {
         Eval e1 = eval.Pop (), e2 = eval.Peek ();
         for (int i = 0; i < lies.Length; i++) ptLies[i] = Evaluate (lies[i]);
         if (level == 0 || ptLies.All (p => mDistLineSq (p, e1.Pt, e2.Pt) < errSq)) {
            pts.Add (e1.Pt);
            level = (level + 1).Clamp (0, maxLevel);
         } else {
            eval.Push (new Eval { A = 0.5.Along (e1.A, e2.A), Pt = ptLies[1] });
            eval.Push (e1);
            level--;
         }
      }
      pts.Add (eval.Pop ().Pt);
   }

   public T Evaluate (double t) {
      if (t <= Knot[0]) return Ctrl[0];
      if (t >= Knot[^1]) return Ctrl[^1];
      int span = ComputeBasis (t), p = Degree;

      T sum = mZero;
      if (Rational) {
         double wsum = 0;
         for (int j = 0; j <= p; j++) {
            int n = span - p + j;
            double weight = _N[j] * Weight[n];
            sum = mAdd (sum, mMult (Ctrl[n], weight));
            wsum += weight;
         }
         return mMult (sum, 1 / wsum);
      } else {
         for (int j = 0; j <= p; j++) {
            int n = span - p + j;
            double weight = _N[j];
            sum = mAdd (sum, mMult (Ctrl[n], weight));
         }
         return sum;
      }
   }

   // Private data -------------------------------------------------------------
   T mZero;                   // The zero value for T (Point2 or Point3)
   Func<T, T, T> mAdd;        // The function to add two T types (Point2+Point2 or Point3+Point3)
   Func<T, double, T> mMult;  // The function to multiply a T by a scalar
   Func<T, T, T, double> mDistLineSq;  // Function to compute square of point-to-line distance
}

public class Spline2 : Spline<Point2> {
   public Spline2 (ImmutableArray<Point2> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight)
      : base (ctrl, knot, weight, Point2.Zero, Mult, Add, DistLineSq) { }

   static Point2 Mult (Point2 a, double f) => new (a.X * f, a.Y * f);
   static Point2 Add (Point2 a, Point2 b) => new (a.X + b.X, a.Y + b.Y);
   static double DistLineSq (Point2 p, Point2 a, Point2 b) => p.DistToLineSq (a, b);
}
