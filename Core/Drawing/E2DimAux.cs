// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dimension.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static DimStyle2.EPos;

#region class DimStyle2 ----------------------------------------------------------------------------
/// <summary>Dimension style (based on AutoCAD)</summary>
public class DimStyle2 {
   // Constructor --------------------------------------------------------------
   DimStyle2 () => (Name, Style) = ("", null!);
   public DimStyle2 (string name, float scale, float asz, float exo, float exe, float txt, float cen, float gap, bool tih, bool toh, bool tofl, int tabove, int dec, int adec, Style2 style) {
      Name = name; Style = style;
      ArrowSize = asz * scale; ExtOffset = exo * scale; ExtExtend = exe * scale; 
      TextSize = txt * scale; DimCen = cen * scale; DimGap = gap * scale;
      TextPos = tabove switch { 0 => Centered, 4 => Below, _ => Above };
      LinDecimal = dec; AngDecimal = adec;
      if (tih) mFlags |= EFlags.TIHorz;
      if (toh) mFlags |= EFlags.TOHorz;
      if (tofl) mFlags |= EFlags.TOFL;
   }

   public DimStyle2 (string name, Style2 style) {
      Name = name; Style = style;
      ArrowSize = TextSize = DimCen = 2.5f; ExtOffset = DimGap = 0.625f; ExtExtend = 1.25f;
      LinDecimal = 2; AngDecimal = 1;
      mFlags = EFlags.TOFL;
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Name of the style (DXF Group 2)</summary>
   public readonly string Name;

   /// <summary>Arrowhead size (DXF Group 41)</summary>
   public float ArrowSize;
   /// <summary>Extension line offset from dim. definition point (DXF Group 42)</summary>
   public float ExtOffset;
   /// <summary>Extension line extension beyond dimension line (DXF Group 44)</summary>
   public float ExtExtend;
   /// <summary>Text height (DXF Group 140)</summary>
   public float TextSize;
   /// <summary>Size of center mark / center lines (DXF Group 141)</summary>
   public float DimCen;
   /// <summary>Gap between dimension line and text (DXF Group 147)</summary>
   public float DimGap;

   /// <summary>Number of decimal places for linear dimensions</summary>
   public int LinDecimal;
   /// <summary>Number of decimal places for angular dimensions</summary>
   public int AngDecimal;

   /// <summary>Text inside dimension line horizontal (DXF Group 73)</summary>
   public bool TIHorz { get => Get (EFlags.TIHorz);  set => Set (EFlags.TIHorz, value); }
   /// <summary>Text outside dimension line horizontal (DXF Group 74)</summary>
   public bool TOHorz { get => Get (EFlags.TOHorz); set => Set (EFlags.TOHorz, value); }
   /// <summary>Draw line between extension lines even when text is outside (DXF Group 172)</summary>
   public bool TOFL => Get (EFlags.TOFL);

   /// <summary>Text position (Centered / Above / Below)</summary>
   public EPos TextPos;

   /// <summary>Text style used for dimensions</summary>
   public readonly Style2 Style;

   // Nested types -------------------------------------------------------------
   [Flags]
   enum EFlags { Nil = 0, TIHorz = 1 << 0, TOHorz = 1 << 1, TOFL = 1 << 2, }
   EFlags mFlags;

   public enum EPos { Centered = 0, Above = 1, Below = 4 }

   // Implementation -----------------------------------------------------------
   bool Get (EFlags bit) => (mFlags & bit) != 0;
   void Set (EFlags bit, bool value) { if (value) mFlags |= bit; else mFlags &= ~bit; }

   static DimStyle2? ByName (IReadOnlyList<object> stack, string name) {
      for (int i = stack.Count - 1; i >= 0; i--)
         if (stack[i] is Dwg2 dwg) return dwg.GetDimStyle (name);
      return null;
   }
}
#endregion

#region enum EDim ----------------------------------------------------------------------------------
/// <summary>Represents the different types of E2Dim</summary>
public enum EDim { 
   Linear = 0, Aligned = 1, Angular = 2, Diameter = 3, Radius = 4, Angular3P = 5, Ordinate = 6, Generic = 32
};
#endregion

#region E2Dim rendering ----------------------------------------------------------------------------
// Implements E2Dim methods related to rendering
public abstract partial class E2Dim {
   // Entity build helpers -----------------------------------------------------
   // Adds an arrowhead to the list of entities
   internal static void AddArrow (Layer2 layer, DimStyle2 style, List<Ent2> ents, Point2 pt, double angle) {
      double len = style.ArrowSize, hwid = len / 6;
      Point2 pa = pt.Polar (len, angle);
      Point2 pb = pa.Polar (hwid, angle + Lib.HalfPI); pa = pa.Polar (hwid, angle - Lib.HalfPI);
      ents.Add (new E2Solid (layer, [pa, pb, pt, pt]));
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
   protected EInside CheckSpace (double available, double txtMeasure, double textLie) {
      double asz = mStyle.ArrowSize * 4;
      if (textLie is < 0 or > 1) txtMeasure = 1e99;
      if (asz + txtMeasure <= available) return EInside.Both;
      if (asz > txtMeasure && asz <= available) return EInside.Arrows;
      if (txtMeasure <= available) return EInside.Text;
      if (asz <= available) return EInside.Arrows;
      return EInside.None;
   }

   // Gets the normalized angle used for text
   protected double GetTextAngle (double textAngle) {
      textAngle = Lib.NormalizeAngle (textAngle);
      if (textAngle is < (-Lib.HalfPI + Lib.Epsilon) or > (Lib.HalfPI + Lib.Epsilon))
         textAngle += Lib.PI;
      return textAngle;
   }

   // This is overridden in derived types to make the entities 
   protected abstract void MakeEnts ();

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

   // Basic build method called from various E2Dim objects
   protected Point2 BuildEnts (Seg seg, Point2 pick, string text, ReadOnlySpan<Point2> basis, bool twoArrows = true, bool discardSeg = false, bool markSegEnd = false) {
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
      if (basis.Length >= 2) {
         for (int i = 0; i < 2; i++) {
            Point2 end = seg.GetPointAt (i), start;
            if (basis.Length >= 4) {
               Point2 a = basis[i * 2], b = basis[i * 2 + 1];
               double lie = a.EQ (b) ? 1.01 : end.GetLieOn (a, b);
               if (lie is >= 0 and <= 1) continue;
               start = basis[i * 2 + (lie < 0 ? 0 : 1)];
            } else
               start = basis[i];
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
      double txtMeasure = bound.Width, snappedTextLie = seg.GetSnappedLie (pick);
      if (s.TIHorz && Math.Abs (Math.Cos (seg.GetSlopeAt (0.5))) < 0.5) txtMeasure = bound.Height;
      double space = Math.Abs (seg.AngSpan) < Lib.PI ? seg.A.DistTo (seg.B) : seg.Length;
      var code = CheckSpace (space, txtMeasure, snappedTextLie);
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
      AddArrow (Layer, Style, mEnts, seg.A, ang0);
      if (twoArrows) AddArrow (Layer, Style, mEnts, seg.B, ang1);
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
      bool textHorzAnyway = GetTextAngle (seg.GetSlopeAt (0.5)).IsZero ();
      double yShift = pos switch { Above => 1, Below => -1, _ => 0 } * bound.Height / 2;
      if (iHorz && textInside && !textHorzAnyway) yShift = 0;

      for (int i = 0; i < 2; i++) {
         // Each leader line consists of two legs:
         // - Leg 1 leading directly along/against the arrowhead of length leg1
         // - Leg 2 horizontal (only if txtHorz is set). 
         // Leader line length also depends on whether text is outside and on that side where
         // we are drawing the leader line
         bool textOutside = (i == textLie && !textInside), arrowOutside = !arrowInside;
         double leg1 = arrowOutside ? asz + ext : 0;
         leg1 += (arrowOutside, textOutside, iHorz, iBreak) switch {
            (true, true, false, false) => gap + txtWidth - ext,
            (false, true, false, false) => gap + txtWidth,
            (false, true, _, true) => asz,
            (false, true, true, false) => ext + DShift (i == 0 ? ang0 : ang1),
            (true, true, true, false) => DShift (i == 0 ? ang0 : ang1),
            _ => 0
         };
         double leg2 = (textOutside && iHorz) ? (iBreak ? ext : txtWidth) : 0;
         double kneeAngle = i == 0 ? ang0 : ang1;
         if (Math.Abs (Math.Sin (kneeAngle)) < 0.1 && textOutside && iHorz) leg1 -= ext;

         Point2 hip = seg.GetPointAt (i);
         if ((snappedTextLie < 0 && i == 0) || (snappedTextLie > 1 && i == 1))
            leg1 = Math.Max (leg1, hip.DistTo (pick));
         Point2 knee = hip.Polar (leg1 * (arrowInside ? -1 : 1), kneeAngle);
         double dx = knee.X.EQ (hip.X) ? Math.Sign (hip.X - seg.GetPointAt (1 - i).X) : Math.Sign (knee.X - hip.X);
         if (dx == 0) dx = 1;
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
         if (!twoArrows) break;
      }

      double DShift (double angle) {
         double sin = Math.Sin (angle); if (Math.Abs (sin) < 0.1) return 0;
         bool upward = sin > 0;
         if (arrowInside) return upward == (pos == Above) ? bound.Height : 0;
         return upward == (pos == Below) ? bound.Height - ext : 0;
      }

      // Compute the text angle, and the text box
      textAngle = GetTextAngle (textAngle);
      ptText = ptText.Polar (yShift, textAngle + Lib.HalfPI);
      Poly textBox = Poly.Rectangle (bound) * Matrix2.Rotation (textAngle) * Matrix2.Translation (ptText);
      if (!txtSlideAngle.IsNan)
         ptText += SlideTextBox (textBox, txtSlideAngle);
      if (!discardSeg) {
         if (markSegEnd) {
            double angle = seg.Slope;
            AddPoint (seg.B);
            seg = Poly.Line (seg.A, seg.B.Polar (-s.ExtOffset, angle))[0];
         }
         if ((iBreak || (iHorz && !textHorzAnyway)) && textInside) AddTrimmedSeg (seg, textBox);
         else AddPoly (seg.ToPoly ());
      }

      // Position the text
      AddText (ptText, text, textAngle);
      return ptText;
   }
}
#endregion
