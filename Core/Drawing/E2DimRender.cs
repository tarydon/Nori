namespace Nori;
using static DimStyle2.EPos;

// Implements E2Dim methods related to MakeEnts
public abstract partial class E2Dim {
   // Entity build helpers -----------------------------------------------------
   // Adds an arrowhead to the list of entities
   protected void AddArrow (Point2 pt, double angle) {
      double len = mStyle.ArrowSize, hwid = len / 6;
      Point2 pa = pt.Polar (len, angle);
      Point2 pb = pa.Polar (hwid, angle + Lib.HalfPI); pa = pa.Polar (hwid, angle - Lib.HalfPI);
      mEnts.Add (new E2Solid (Layer, [pa, pb, pt, pt]));
   }

   // Adds a Poly to the list of entities
   protected void AddPoly (Poly poly) => mEnts.Add (new E2Poly (Layer = Layer, poly));

   // Adds a Point to the list of entities      
   protected void AddPoint (Point2 pt) => mEnts.Add (new E2Point (Layer = Layer, pt) { IsDefPoint = true });

   // Adds text to the list of entities
   protected void AddText (Point2 pt, string text, double angle) {
      mEnts.Add (new E2Text (Layer, mStyle.Style, text, pt, mStyle.TextSize, angle, 0, 1, ETextAlign.MidCenter));
   }

   // This trims the given dimension segment around the text-box of the dimension and
   // adds the remnant segments to the mEnts list
   protected void AddTrimmedSeg (Seg seg, Poly box) {
      List<double> lies = [];
      Span<Point2> buffer = stackalloc Point2[2];
      foreach (var s in box.Segs)
         foreach (var pt in seg.Intersect (s, buffer, true))
            lies.Add (seg.GetLie (pt));
      lies.Sort ();
      if (lies.Count < 2) {
         AddPoly (Poly.Arc (seg.A, seg.Midpoint, seg.B));
         return;
      }

      // Get the two points adjacent the 'gap'
      double startLie = lies[0], endLie = lies[^1];
      Point2 ptStart = seg.GetPointAt (startLie), ptEnd = seg.GetPointAt (endLie);
      if (seg.IsArc) {
         AddPoly (Poly.Arc (seg.A, seg.GetPointAt (startLie / 2), ptStart));
         AddPoly (Poly.Arc (ptEnd, seg.GetPointAt ((endLie + 1.0) / 2), seg.B));
      } else {
         AddPoly (Poly.Line (seg.A, ptStart));
         AddPoly (Poly.Line (ptEnd, seg.B));
      }
   }

   protected void SetTextPoint (int index, Point2 pt) {
      if (mPts.Count <= index) mPts.Add (Point2.Zero);
      mPts[index] = pt;
   }

   // Checks if the arrows and text can be accomodated within the given space.
   // Normally, we try to draw arrow-heads within the two extension lines, and also draw the
   // text within the two extension lines. this checks if the space is large enough, and returns
   // EInside.Both if that can be done. Otherwise, it tries if either one of the text or arrows
   // can be drawn (and returns EInside.Arrow / EInside.Text in that case). Sometimes the space
   // is so small, we can't accommodate either (and this returns EInside.None). 
   // Anything that cannot be accomodated between the extension lines goes outside on one or
   // the other side. 
   protected EInside CheckSpace (double available, double txtMeasure) { // REMOVETHIS
      double asz = mStyle.ArrowSize * 4;
      if (asz + txtMeasure <= available) return EInside.Both;
      if (asz > txtMeasure && asz <= available) return EInside.Arrows;
      if (txtMeasure <= available) return EInside.Text;
      if (asz <= available) return EInside.Arrows;
      return EInside.None;
   }

   // Gets the lengths of the dimension line that overhang beyond the extension lines on
   // either side. Normally, if there is enough space between the extension lines to accomodate
   // arrowheads and text, these overhangs are both zero. Otherwise, the overhangs depend
   // on these:
   // - code : Which combination of arrowheads / text fit inside between the extension lines
   // - txtMeasure: The text measurement (width/height) that we should use for computation.
   // - nearStart: If the text is outside the extension lines, is it closer to the start point
   //              or the end point   
   protected (double a, double b) GetOverhangs (EInside code, double txtMeasure, bool nearStart) {
      double aExtend = 0, bExtend = 0;
      bool textOutside = (code & EInside.Text) == 0, arrowOutside = (code & EInside.Arrows) == 0;
      bool iBreak = mStyle.TextPos == Centered;
      if (textOutside) {
         // If the text is outside then we might need to extend the extension line,
         // if text is above/below the line
         if (mStyle.TOHorz) iBreak = true;
         aExtend += (nearStart && !iBreak) ? txtMeasure : 0;
         bExtend += (!nearStart && !iBreak) ? txtMeasure : 0;
      }
      if (arrowOutside) {
         // If the arrows are outside, then we need to extend the extension lines
         // by arrow-size + ext
         double ext = mStyle.ExtExtend;
         aExtend = bExtend = mStyle.ArrowSize;
         if (iBreak || !textOutside) { aExtend += ext; bExtend += ext; }
         if (textOutside && !iBreak) {
            if (nearStart) { aExtend += txtMeasure; bExtend += ext; } else { bExtend += txtMeasure; aExtend += ext; }
         }
      }
      return (aExtend, bExtend);
   }

   // Gets the normalized angle used for text
   protected double GetTextAngle (double textAngle) {
      textAngle = Lib.NormalizeAngle (textAngle);
      if (textAngle is < (-Lib.HalfPI + Lib.Epsilon) or > (Lib.HalfPI + Lib.Epsilon))
         textAngle += Lib.PI;
      return textAngle;
   }

   // This is overridden in derived types to make the entities 
   protected virtual void MakeEnts () {
      var s = Style;
      var (asz, gap, ohorz, ihorz) = (s.ArrowSize, s.DimGap, s.TOHorz, s.TIHorz);
      var (ext, exo, pos) = (s.ExtExtend, s.ExtOffset, s.TextPos);
      switch (this) {
         case E2DimRad e2r: MakeRadOrDia (asz, exo, gap, ohorz, false, pos, e2r.Radius); break;
         case E2DimDia e2d: MakeRadOrDia (asz, exo, gap, ohorz, true, pos, e2d.Radius); break;
         case E2Dim3PAngle e2p: Make3PAngle (asz, exo, ext, ohorz, ihorz, pos); break;
         default: throw new NotImplementedException ();
      }
   }

   // Helper used to measure the text (assumes text is horizontal, centered at 0,0)
   protected Bound2 Measure (string text) {
      var font = LineFont.Get (mStyle.Style.Font);
      var box = font.Measure (text, Point2.Zero, ETextAlign.MidCenter, 0, 1, mStyle.TextSize, 0);
      return box.InflatedL (mStyle.DimGap);
   }

   // Given a text box, computes the vector that would moves its center point
   // to a point on the edge of the box (moving along the given angle)
   protected Vector2 SlideTextBox (Poly rect, double angle) {
      Point2 cen = rect.Pts[0].Midpoint (rect.Pts[2]);
      Point2 outer = cen.Polar (1000, angle);
      foreach (var seg in rect.Segs) {
         Point2 pa = Geo.LineSegXLineSeg (seg.A, seg.B, cen, outer);
         if (!pa.IsNil) return pa - cen;
      }
      return Vector2.Zero;
   }

   // Given a text box, computes the vector that would move its center point
   // to one of the 8 corners on the edge of the box (closest to the angle)
   protected Vector2 SnapTextBox (Poly rect, double angle, bool arrowOutside) {
      Point2 cen = rect.Pts[0].Midpoint (rect.Pts[2]), outer = cen.Polar (1000, angle + Lib.PI);
      double minDist = 1e10; Point2 best = cen;
      foreach (var seg in rect.Segs) { Check (seg.A); Check (seg.Midpoint); }
      if (arrowOutside) cen = cen.Polar (mStyle.ArrowSize + mStyle.ExtExtend, angle);
      return cen - best;

      void Check (Point2 pt) {
         double dist = pt.DistToLineSeg (cen, outer);
         if (dist < minDist) { minDist = dist; best = pt; }
      }
   }

   // Entity build methods -------------------------------------------------------------------------
   // Creates the entities of the 3P Angular dimension.
   // Based on whether the TIHorz flag (text-inside horizontal) is set, and the text position
   // (Center/Above/Below), the text will need to be positioned in one of the ways as shown
   // in the image: file://N:/Doc/Img/Dim3PAngle2.png 
   // 
   // There are several helper routines in the E2Dim class that are used in this MakeEnts
   // routine (and those of other classes)
   // - Measure(text) computes the bounding box of the text (assuming the text is horizontal,
   //   and centered at (0,0)). 
   // - CheckSpace(available,txtSize) figures out if there is enough space between the
   //   extension lines to position the arrowheads and/or text (4 possibilities exist for
   //   this, as illustrated by file://N:/Doc/Img/Dim3PAngle3.png
   // - GetOverhangs returns the overhang distances used for the dimension line overhangs
   //   on either side of the extension lines. If both the arrowheads and text fit inside
   //   the extension lines, these overhangs are both 0. Otherwise, the overhangs are computed
   //   based on the arrowhead size / text measurement as shown in the images (the blue distances
   //   are the overhangs: file://N:/Doc/Img/Dim3PAngle4.png
   // - AddPoly, AddText, AddArrow are used to add various entities into the mEnts list.
   // - When the text is placed with 'centered' alignment, and fits between the extension 
   //   lines, the dimension line has to be trimmed to the bits on both sides of the text-box;
   //   this is done by AddTrimmedSeg
   void Make3PAngle (double asz, double exo, double ext, bool oHorz, bool iHorz, DimStyle2.EPos pos) {
      // Figure out if the CCW arc runs from b..c or from c..b (depends on the position of d)
      Point2 a = mPts[0], b = mPts[1], c = mPts[2], d = mPts[3];
      double angB = a.AngleTo (b), angC = a.AngleTo (c), rad = a.DistTo (d);
      Point2 arcStart = a.Polar (rad, angB), arcEnd = a.Polar (rad, angC);
      bool iBreak = pos == Centered;

      // Create the two extension lines - they run 'outward' or 'inward' from the
      // definition points b and c (depending on the position of d)
      double r1 = a.DistTo (b), r2 = a.DistTo (c);
      double ang = rad > r1 ? angB : angB + Lib.PI;
      AddPoly (Poly.Line (b.Polar (exo, ang), arcStart.Polar (ext, ang)));
      ang = rad > r2 ? angC : angC + Lib.PI;
      AddPoly (Poly.Line (c.Polar (exo, ang), arcEnd.Polar (ext, ang)));

      // Figure out if we need a major or minor arc, and compute the angle text
      string text = Text ?? "";
      var arc = Poly.Arc (arcStart, d, arcEnd); var arcSeg = arc[0];
      if (!arcSeg.IsCCW) { arc = arc.Reversed (); arcSeg = arc[0]; }
      double slope0 = arcSeg.GetSlopeAt (0), slope1 = arcSeg.GetSlopeAt (1);
      if (IsAutoText) {
         double span = Math.Abs (arcSeg.AngSpan).R2D ().Round (mStyle.AngDecimal);
         text = $"{span}\u00b0";
      }

      // Measure the text, and figure if we have to find space for the width or the
      // height of the text. 
      // - If the text is aligned (not horizontal), then this is the width
      // - Otherwise, it depends on the angle at which the dimension line goes through
      //   the text. If this is more vertical (not more than 30 degrees away from vertical)
      //   then we use the height of the text, else the width
      Bound2 bound = Measure (text);
      double txtMeasure = bound.Width;
      if (iHorz && Math.Abs (Math.Cos (arcSeg.GetSlopeAt (0.5))) < 0.5) txtMeasure = bound.Height;
      var inside = CheckSpace (arcSeg.Length, txtMeasure);
      bool textInside = (inside & EInside.Text) != 0, arrowInside = (inside & EInside.Arrows) != 0;

      // Figure out the rotation angle of the text box, and an initial anchor point for
      // the text box
      int txtLie = -1; // -1:Inside, 0:Outside Start, 1:Outside End
      double tangentAngle = 0; Point2 textPos = arcSeg.Midpoint;
      bool textHorz = textInside ? iHorz : oHorz;
      // If we are doing horizontal text, we always use 'centered' position, otherwise
      // we can compute the appropriate text angle
      if (textHorz) iBreak = true; else tangentAngle = arcSeg.GetSlopeAt (0.5);
      if (!textInside) {
         if (d.DistToSq (arcSeg.A) < d.DistToSq (arcSeg.B)) {
            textPos = arcSeg.A;
            tangentAngle = arcSeg.GetSlopeAt (0) + Lib.PI;
            txtLie = 0;
         } else {
            textPos = arcSeg.B;
            tangentAngle = arcSeg.GetSlopeAt (1);
            txtLie = 1;
         }
         double shift = txtMeasure / 2;
         if (!arrowInside) {
            shift += asz;
            if (iBreak) shift += ext;
         }
         if (!textHorz) textPos = textPos.Polar (shift, tangentAngle);
      }

      // Add the arrowheads, and the extension lines outside
      if (arrowInside) {
         double lie = asz / arcSeg.Length;
         AddArrow (arcSeg.A, arcSeg.A.AngleTo (arcSeg.GetPointAt (lie)));
         AddArrow (arcSeg.B, arcSeg.B.AngleTo (arcSeg.GetPointAt (1 - lie)));
      } else {
         AddArrow (arcSeg.A, slope0 + Lib.PI);
         AddArrow (arcSeg.B, slope1);
      }
      var (aExt, bExt) = GetOverhangs (inside, txtMeasure, txtLie == 0);
      if (aExt > 0) AddPoly (Poly.Line (arcSeg.A, arcSeg.A.Polar (aExt, slope0 + Lib.PI)));
      if (bExt > 0) AddPoly (Poly.Line (arcSeg.B, arcSeg.B.Polar (bExt, slope1)));

      // If the text is within the extensions, we have to trim the extension lines at the
      // box, otherwise we can add the complete arc
      double textAngle = textHorz ? 0 : tangentAngle;
      var textBox = Poly.Rectangle (bound) * Matrix2.Rotation (textAngle) * Matrix2.Translation (textPos);
      if (textInside && pos == Centered) AddTrimmedSeg (arcSeg, textBox);
      else AddPoly (arc);

      // If we are not doing 'centered' positioning, we have to shift the text box in a suitable
      // direction
      Vector2 vecShift = new (0, 0);
      if (!textHorz && pos != Centered) {
         double shiftLie = textInside ? 0.5 : txtLie, shiftAngle = a.AngleTo (arcSeg.GetPointAt (shiftLie));
         if (pos == Below) shiftAngle += Lib.PI;
         vecShift = SlideTextBox (textBox, shiftAngle);
      } else if (textHorz && !textInside)
         vecShift = SnapTextBox (textBox, tangentAngle, !arrowInside);
      textPos += vecShift;

      // Add the text itself
      textAngle = GetTextAngle (textAngle);
      SetTextPoint (4, textPos);
      AddText (textPos, text, textAngle);
      AddPoint (a); AddPoint (b); AddPoint (c);
   }

   // Builds a radius or diameter dimension
   void MakeRadOrDia (double asz, double exo, double gap, bool horz, bool isDia, DimStyle2.EPos pos, double mRadius) {
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
            tip = cen.Polar (isDia ? -radius : exo, angle);
         if (!isDia) AddPoint (cen);
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

   protected void BuildEnts (Seg seg, Point2 pick, string text, ReadOnlySpan<Point2> basis, bool twoArrows, bool discardSeg) {
      // Add the extension lines (these are required for most dimension types (except
      // radius & diameter), and are specified by the basis[] points array.
      // - If there are no basis points, no extension lines are drawn (Radius, Diameter dimensions)
      // - If there are two basis points, they form the start points of the first and second extension
      //   lines (Linear, Aligned, 3P-Angle dimensions)
      // - If there are four basis points, the first two are used for the first extension line,
      //   and the second two for the other one. We see where the seg.A position lies relative to 
      //   basis[0]..basis[1]. If it lies in between, we don't draw an extension line. If it lies
      //   _before_ basis[0], the extension line starts at basis[0], if it lies _beyond_ basis[1],
      //   the extension line starts form there (Angle dimensions)
      // The extension line is offset from the start point by ExtOffset, and extends beyond the 
      // dimension line by ExtExtend
      var s = mStyle;
      double ext = s.ExtExtend, asz = s.ArrowSize, gap = s.DimGap;
      if (basis.Length >= 4) {
         for (int i = 0; i < 2; i++) {
            Point2 end = seg.GetPointAt (i), a = basis[i * 2], b = basis[i * 2 + 1];
            double lie = a.EQ (b) ? 1.01 : end.GetLieOn (a, b);
            if (lie is >= 0 and <= 1) continue;
            Point2 start = basis[i * 2 + (lie < 0 ? 0 : 1)];
            double slope = start.AngleTo (end);
            AddPoly (Poly.Line (start.Polar (s.ExtOffset, slope), end.Polar (ext, slope)));
         }
      }

      // Next, measure the text and see if the dimension line cuts more through the horizontal or
      // vertical direction of the text-box (from this we figure out if we are going to use the 
      // width or height of the text box as txtMeasure). Of course, if the text is aligned to
      // the dimension (not horizontal), we always use the width. With this txtMeasure value, we 
      // use CheckSpace to figure out if there is enough space between the extension lines to place
      // the arrow, the text, both or neither. 
      Bound2 bound = Measure (text);
      double txtMeasure = bound.Width;
      if (s.TIHorz && Math.Abs (Math.Cos (seg.GetSlopeAt (0.5))) < 0.5) txtMeasure = bound.Height;
      double space = Math.Abs (seg.AngSpan) < Lib.PI ? seg.A.DistTo (seg.B) : seg.Length;
      var code = CheckSpace (space, txtMeasure);
      bool textInside = (code & EInside.Text) != 0, arrowInside = (code & EInside.Arrows) != 0;

      // Next, add the arrowheads
      // - Radius dimensions have one arrow head
      // - Diameter dimensions have one arrow head (unless the TOFL is set, in which case they extend
      //   to the opposite diameter of the circle and have two)
      // - Other dimension types have two
      // Note that the arrowhead direction is FROM the tip to the BASE of the arrow
      double delta = s.ArrowSize / seg.Length, ang0, ang1;
      if (arrowInside)
         (ang0, ang1) = (seg.A.AngleTo (seg.GetPointAt (delta)), seg.B.AngleTo (seg.GetPointAt (1 - delta)));
      else {
         (ang0, ang1) = (seg.GetSlopeAt (0) + Lib.PI, seg.GetSlopeAt (1));
      }
      AddArrow (seg.A, ang0); if (twoArrows) AddArrow (seg.B, ang1);
      // After this, tweak the ang0 and ang1 angles - we did not make them exactly tangential
      // to the start and end slopes to better 'center' the arrowhead along a curved dimension line
      // above, but now we want to clean tangent to position the text outside the dimension line
      // (so we do this only if arrowInside and textOutside). 
      if (arrowInside && !textInside) { ang0 = seg.GetSlopeAt (0); ang1 = seg.GetSlopeAt (1) + Lib.PI; }

      // Next, add the leader lines from the arrow to the text. First, figure out text position
      // relative to seg: 0=outside seg.A, -1:inside seg, 1:outside seg.B
      Point2 ptText = seg.GetPointAt (0.5);
      var pos = s.TextPos; var txtWidth = bound.Width;
      double txtSlideAngle = double.NaN; 
      bool iBreak = pos == Centered, iHorz = textInside ? s.TIHorz : s.TOHorz;
      int textLie = textInside ? -1 : (pick.DistToSq (seg.A) < pick.DistToSq (seg.B) ? 0 : 1);
      double textAngle = iHorz ? 0 : GetTextAngle (seg.GetSlopeAt (0.5));
      double yShift = pos switch { Above => 1, Below => -1, _ => 0 } * bound.Height / 2;

      double D () => 0;

      for (int i = 0; i < 2; i++) {
         // Each leader line consists of two legs:
         // - Leg 1 leading directly along/against the arrowhead of length leg1
         // - Leg 2 horizontal (only if txtHorz is set). 
         // Leader line length also depends on whether text is outside and on that side where
         // we are drawing the leader line
         bool textOutside = (i == textLie && !textInside), arrowOutside = !arrowInside;
         double leg1 = arrowOutside ? asz + ext : 0;
         leg1 += (arrowOutside, textOutside, iHorz, iBreak) switch {
            (true, true, false, false) => gap + txtWidth - asz,
            (false, true, false, false) => gap + txtWidth,
            (false, true, _, true) => asz,
            (false, true, true, false) => ext + D (),
            (true, true, true, false) => D (),
            _ => 0
         };
         double leg2 = (textOutside && iHorz) ? (iBreak ? ext : txtWidth) : 0;

         Point2 hip = seg.GetPointAt (i);
         Point2 knee = hip.Polar (leg1 * (arrowInside ? -1 : 1), i == 0 ? ang0 : ang1);
         double dx = knee.X.EQ (hip.X) ? Math.Sign (hip.X - seg.GetPointAt (1 - i).X) : Math.Sign (knee.X - hip.X);
         Point2 toe = knee.Moved (leg2 * dx, 0);
         Poly poly = leg2.IsZero () ? Poly.Line (hip, knee) : Poly.Lines ([hip, knee, toe], false);
         AddPoly (poly);

         // If the text has to be positioned near the leader line (outside the extension lines),
         // update the text pos
         if (i == textLie) {
            // Compute the text shift from the toe point
            double txtShift = txtWidth / 2 * (iBreak ? 1 : -1), slope = poly[^1].Slope;
            if (iHorz != iBreak) ptText = toe.Polar (txtShift, slope);
            else { ptText = toe; if (iHorz && iBreak) txtSlideAngle = slope; }
            if (!iHorz) textAngle = GetTextAngle (slope);

            if (!iHorz && iBreak) { textAngle = slope; ptText = toe.Polar (txtShift, slope); }
            if (!iHorz && !iBreak) { textAngle = slope; ptText = toe.Polar (txtShift, slope); }
            if (iHorz && iBreak) { ptText = toe; txtSlideAngle = slope; }
         }
      }
      if (iHorz && textInside) yShift = 0; 

      // Compute the text angle, and the text box
      Vector2 vecSlide = Vector2.Zero;
      textAngle = GetTextAngle (textAngle);
      ptText = ptText.Polar (yShift, textAngle + Lib.HalfPI);
      Poly textBox = Poly.Rectangle (bound) * Matrix2.Rotation (textAngle) * Matrix2.Translation (ptText);
      if (!txtSlideAngle.IsNan) {
         vecSlide = SlideTextBox (textBox, txtSlideAngle);
         ptText += vecSlide;
      }
      if ((iBreak || iHorz) && textInside) AddTrimmedSeg (seg, textBox);
      else AddPoly (seg.ToPoly ());

      // Position the text
      AddText (ptText, text, textAngle);
   }
}
