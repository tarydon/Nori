namespace Nori;

public partial class Poly {
   // Operations ---------------------------------------------------------------
   public Poly? Chamfer (int n, double d1, double d2, Point2 hint) {
      // Handle the special case where we are chamfering at node 0 of a 
      // closed Poly (by rolling the poly and making it a chamfer at N-1)
      if (IsClosed && n == 0 || n == Count)
         return Roll (1).Chamfer (Count - 1, d1, d2, hint);
      if (n <= 0 || n >= Count) return null;

      Seg s1 = this[n - 1], s2 = this[n];
      if (s1.IsArc || s2.IsArc || s1.Length <= d1 || s2.Length <= d2) return null;

      if (!hint.IsNil && s2.GetDist (hint) < s1.GetDist (hint)) (d1, d2) = (d2, d1);

      PolyBuilder pb = new ();
      const EFlags mask = EFlags.CW | EFlags.CCW;
      for (int i = 0; i < mPts.Length; i++) {
         Point2 pt = mPts[i];
         if (i == n) pt = pt.Polar (d2, s2.Slope);
         if (HasArcs && i < mExtra.Length) {
            var extra = mExtra[i];
            if ((extra.Flags & mask) != 0) pb.Arc (pt, extra.Center, extra.Flags);
            else pb.Line (pt);
         } else 
            pb.Line (pt);
         if (i == n - 1) 
            pb.Line (s2.A.Polar (-d1, s1.Slope));
      }
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   public Poly Roll (int n) {
      if (!IsClosed) throw new Exception ("Pline.Roll() works only with closed plines");
      if (!HasArcs) return new ([.. mPts.Roll (n)], [], mFlags);
      var knots = mExtra.ToList ();
      while (knots.Count < mPts.Length) knots.Add (new (Point2.Nil, 0));
      return new ([.. mPts.Roll (n)], [.. knots.Roll (n)], mFlags);
   }
}