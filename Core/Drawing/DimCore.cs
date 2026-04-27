namespace Nori;
using static DimStyle2.EPos;

public abstract partial class E2Dim {
   public void BuildEnts () {
      var s = Style;
      var (asz, exo, gap, horz, pos) = (s.ArrowSize, s.ExtOffset, s.DimGap, s.TOHorz, s.TextPos);
      switch (this) {
         case E2DimRad e2r: MakeRadOrDia (asz, exo, gap, horz, false, pos, e2r.Radius); break;
         case E2DimDia e2d: MakeRadOrDia (asz, exo, gap, horz, true, pos, e2d.Radius); break;
         default: throw new NotImplementedException (); 
      }
   }

   /// <summary>
   /// Builds a radius or diameter dimension
   /// </summary>
   public void MakeRadOrDia (double asz, double exo, double gap, bool horz, bool isDia, DimStyle2.EPos pos, double mRadius) {
      var (cen, pick, thruCenter, iBreak) = (Pts[0], Pts[1], ForceDimLine, pos == Centered);
      var (angle, inside) = (cen.AngleTo (pick), cen.DistTo (pick) < mRadius - Lib.Delta);
      var tip = cen.Polar (mRadius, angle);
      if (inside) angle += Lib.PI;
      var textAngle = horz ? 0 : GetTextAngle (angle);

      // Compute and measure the text
      string text = mText ?? "";
      if (IsAutoText) text = isDia ? $"\u2205{(2 * mRadius).Round (mStyle.LinDecimal)}"
                                   : $"R{mRadius.Round (mStyle.LinDecimal)}";
      var bound = Measure (text);
      var (txtWidth, txtHeight) = (bound.Width, bound.Height);
      if (thruCenter && mRadius < txtWidth + 4 * asz) thruCenter = false;

      // Add arrowhead(s)
      AddArrow (tip, angle);
      if (isDia && thruCenter) AddArrow (cen + (cen - tip), angle + Lib.PI);

      // Compute the dimension line Poly
      Poly? poly = null;
      bool trimSeg = false;
      double outLine = asz + ((iBreak || horz) ? asz : txtWidth + gap);
      double horzLine = horz ? (iBreak ? asz : txtWidth) : 0;
      double textDx = ((horz || iBreak) ? 1 : -1) * txtWidth / 2;
      if (horz && iBreak) textDx += asz;
      double textDY = (pos switch { Above => 1, Below => -1, _ => 0 }) * txtHeight / 2;

      Point2 knee = tip.Polar (Math.Max (outLine, tip.DistTo (Pts[1])), angle);
      double dx = knee.X > tip.X ? 1 : -1;
      if (thruCenter) {
         double radius = inside ? mRadius : mRadius + 2 * asz;
         if (inside) {
            double shift1 = isDia ? radius : -exo;
            poly = Poly.Line (cen.Polar (shift1, angle), cen.Polar (-radius, angle));
            knee = isDia ? cen : poly[0].Midpoint;
            trimSeg = true; horzLine = textDx = textDY = 0;
         } else
            tip = cen.Polar (exo, angle);
         if (isDia) AddPoint (cen);
      }
      poly ??= horzLine.IsZero ()
         ? Poly.Line (tip, knee)
         : Poly.Lines ([tip, knee, knee.Moved (dx * horzLine, 0)], false);

      // Now, compute the text position
      double angle1 = horzLine.IsZero () ? angle : dx > 0 ? 0 : Lib.PI;
      Point2 textPt = knee.Polar (textDx, angle1);
      if (textDY != 0) textPt = textPt.Polar (textDY, textAngle + Lib.HalfPI);
      SetTextPoint (2, textPt);
      AddText (textPt, text, textAngle);

      if (trimSeg) {
         var textBox = Poly.Rectangle (bound) * Matrix2.Rotation (textAngle) * Matrix2.Translation (textPt);
         AddTrimmedSeg (poly[0], textBox);
      } else
         AddPoly (poly);
   }
}
