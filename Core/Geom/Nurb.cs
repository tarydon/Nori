namespace Nori;

public class Spline {
   public Spline (int cPts, ImmutableArray<double> knot, ImmutableArray<double> weight) {
      (mPts, Knot, Weight) = (cPts, knot, weight);
      Rational = weight.IsEmpty || weight.All (a => a.EQ (1));
      if (!Rational) Weight = [];
   }

   /// <summary>
   /// Returns the degree of this curve
   /// </summary>
   /// Note that the ORDER of the curve is DEGREE + 1
   public int Degree => Knot.Length - mPts - 1;

   protected readonly int mPts;
   
   public readonly bool Rational;
   public readonly ImmutableArray<double> Knot;
   public readonly ImmutableArray<double> Weight;
}

public class Spline2 : Spline {
   public Spline2 (ImmutableArray<Point2> ctrl, ImmutableArray<double> knot, ImmutableArray<double> weight) : base (knot, weight) 
      => mCtrl = ctrl;

   readonly ImmutableArray<Point2> mCtrl;
}
