// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dimension.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DimStyle2 ----------------------------------------------------------------------------
/// <summary>Dimension style (based on AutoCAD)</summary>
public class DimStyle2 {
   // Constructor --------------------------------------------------------------
   DimStyle2 () => (Name, Style) = ("", null!);
   public DimStyle2 (string name, float scale, float asz, float exo, float exe, float txt, float cen, float gap, bool tih, bool toh, bool tofl, int tabove, int dec, int adec, Style2 style) {
      Name = name; Style = style;
      ArrowSize = asz * scale; ExtOffset = exo * scale; ExtExtend = exe * scale; 
      TextSize = txt * scale; DimCen = cen * scale; DimGap = gap * scale;
      TextPos = tabove switch { 0 => EPos.Centered, 4 => EPos.Below, _ => EPos.Above };
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
   public readonly float ArrowSize;
   /// <summary>Extension line offset from dim. definition point (DXF Group 42)</summary>
   public readonly float ExtOffset;
   /// <summary>Extension line extension beyond dimension line (DXF Group 44)</summary>
   public readonly float ExtExtend;
   /// <summary>Text height (DXF Group 140)</summary>
   public readonly float TextSize;
   /// <summary>Size of center mark / center lines (DXF Group 141)</summary>
   public readonly float DimCen;
   /// <summary>Gap between dimension line and text (DXF Group 147)</summary>
   public readonly float DimGap;

   /// <summary>Number of decimal places for linear dimensions</summary>
   public readonly int LinDecimal;
   /// <summary>Number of decimal places for angular dimensions</summary>
   public readonly int AngDecimal;

   /// <summary>Text inside dimension line horizontal (DXF Group 73)</summary>
   public bool TIHorz { get => Get (EFlags.TIHorz);  set => Set (EFlags.TIHorz, value); }
   /// <summary>Text outside dimension line horizontal (DXF Group 74)</summary>
   public bool TOHorz { get => Get (EFlags.TOHorz); set => Set (EFlags.TOHorz, value); }
   /// <summary>Draw line between extension lines even when text is outside (DXF Group 172)</summary>
   public bool TOFL => Get (EFlags.TOFL);

   /// <summary>Text position (Centered / Above / Below)</summary>
   public EPos TextPos { get => mTextPos; set => mTextPos = value; }
   EPos mTextPos;

   /// <summary>Text style used for dimensions</summary>
   public readonly Style2 Style;

   // Nested types -------------------------------------------------------------
   [Flags]
   enum EFlags { TIHorz = 1 << 0, TOHorz = 1 << 1, TOFL = 1 << 2, }
   EFlags mFlags;

   public enum EPos { Centered = 0, Above = 1, Below = 4 }

   // Implementation -----------------------------------------------------------
   bool Get (EFlags bit) => (mFlags & bit) != 0;
   void Set (EFlags bit, bool value) { if (value) mFlags |= bit; else mFlags &= ~bit; }
}
#endregion

public enum EDim { 
   Linear = 0, Aligned = 1, Angular = 2, Diameter = 3, Radius = 4, Angular3P = 5, Ordinate = 6, Generic = 32
};

#region class E2Dim --------------------------------------------------------------------------------
/// <summary>E2Dim is the base class for all 2D dimensions</summary>
public abstract class E2Dim : Ent2 {
   // Constructors -------------------------------------------------------------
   // Default constructor used during streaming
   protected E2Dim () => (mStyle, mText) = (null!, null);
   /// <summary>Called by derived classes when an E2Dim is built</summary>
   /// <param name="layer">Layer this entity lives in</param>
   /// <param name="kind">Which kind of dimension is this?</param>
   /// <param name="style">The DimStyle2 that provides dimension settings</param>
   /// <param name="pts">Definition points </param>
   /// <param name="text">Dimension text</param>
   /// The interpretation of definition points varies based on the actual kind of dimension
   /// this is. Typically, the set of points is in the same order in which one would click
   /// to input them when creating the dimension dynamically.
   public E2Dim (Layer2 layer, EDim kind, DimStyle2 style, IList<Point2> pts, string? text) : base (layer) {
      (mKind, mStyle, mText) = (kind, style, text is null or "<>" or "" ? null : text);
      if (mText == null) mFlags |= E2Flags.AutoText;
      mPts.AddRange (pts);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the dimension</summary>
   public override Bound2 Bound => Bound2.Cached (ref mBound, () => new (Ents.Select (a => a.Bound)));
   Bound2 mBound = new ();

   /// <summary>The entities making up the dimension</summary>
   /// When loading a Dimension from a DXF file, these are stored in Block - we explode
   /// that block and gather the Ents here. When a dimension is created dynamically in the UI,
   /// we call the MakeEnts routine that is overridden for each type of dimension to create the
   /// actual dimension
   public IReadOnlyList<Ent2> Ents {
      get {
         if (mEnts.Count == 0) MakeEnts ();
         return mEnts;
      }
   }
   protected List<Ent2> mEnts = [];

   /// <summary>
   /// Has the text been auto-generated (based on measurement)
   /// </summary>
   public bool IsAutoText => Get (E2Flags.AutoText);

   /// <summary>Which kind of dimension is this?</summary>
   public EDim Kind => mKind;
   EDim mKind;

   /// <summary>Set of points defining the dimension (interpretation depends on the kind)</summary>
   public IReadOnlyList<Point2> Pts => mPts;
   protected List<Point2> mPts = [];

   /// <summary>The DimStyle used by this dimension entity</summary>
   /// This is stored primarily in the DimStyles list of the drawing
   public DimStyle2 Style => mStyle;
   protected DimStyle2 mStyle;

   /// <summary>The text of the dimension, if not blank</summary>
   /// If this is "<>" or "" or null, the default text is used (based on the actual 
   /// measurement). If this is " ", then the text is suppressed.
   public string? Text => mText;
   protected string? mText;

   // Methods ------------------------------------------------------------------
   /// <summary>Get the transformed bound of the dimension</summary>
   public override Bound2 GetBound (Matrix2 xfm) 
      => new (Ents.Select (a => a.GetBound (xfm)));

   /// <summary>
   /// Gets the 'definition points' of this dimension (for saving to DXF)
   /// </summary>
   /// Each tuple in the list is a DXF group code (10,11,12 etc) for the X
   /// coordinate (the Y coordinates are stored at that value + 10)
   public abstract void GetDefPoints (List<(int, Point2)> defPoints);

   /// <summary>Internal routine to load the entities from a block</summary>
   /// Since blocks are not used by any dimension other than this one, we can load the entities
   /// here and later discard that block
   internal Block2? LoadEnts (Dwg2 dwg, string name) {
      if (dwg.Blocks.FirstOrDefault (a => a.Name == name) is { } block) {
         mEnts.AddRange (block.Ents);
         return block;
      }
      return null;
   }

   /// <summary>Check if the Dimension is close to the given point (closer than the given threshold)</summary>
   public override bool IsCloser (Point2 pt, ref double threshold) {
      if (!Bound.Contains (pt, threshold)) return false;
      foreach (var ent in Ents)
         if (ent.IsCloser (pt, ref threshold)) return true;
      return false;
   }

   // Implementation -----------------------------------------------------------
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

   // Checks if the arrows and text can be accomodated within the given space.
   // Normally, we try to draw arrow-heads within the two extension lines, and also draw the
   // text within the two extension lines. this checks if the space is large enough, and returns
   // EInside.Both if that can be done. Otherwise, it tries if either one of the text or arrows
   // can be drawn (and returns EInside.Arrow / EInside.Text in that case). Sometimes the space
   // is so small, we can't accommodate either (and this returns EInside.None). 
   // Anything that cannot be accomodated between the extension lines goes outside on one or
   // the other side. 
   protected EInside CheckSpace (double available, double txtMeasure) {
      double asz = mStyle.ArrowSize * 3;
      txtMeasure += 2 * mStyle.ExtExtend;
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
      bool iBreak = mStyle.TextPos == DimStyle2.EPos.Centered;
      if (textOutside) {
         // If the text is outside then we might need to extend the extension line,
         // if text is above/below the line
         aExtend += (nearStart && !iBreak) ? txtMeasure : 0;
         bExtend += (!nearStart && !iBreak) ? txtMeasure : 0;
      }
      if (arrowOutside) {
         // If the arrows are outside, then we need to extend the extension lines
         // by arrow-size + ext
         double ext = mStyle.ExtExtend;
         aExtend = bExtend = mStyle.ArrowSize;
         if (mStyle.TextPos == DimStyle2.EPos.Centered || !textOutside) { aExtend += ext; bExtend += ext; } 
         if (textOutside && !iBreak) {
            if (nearStart) { aExtend += txtMeasure; bExtend += ext; }
            else { bExtend += txtMeasure; aExtend += ext; }
         }
      }
      return (aExtend, bExtend);
   }

   // This is overridden in derived types to make the entities 
   protected abstract void MakeEnts ();
  
   // Helper used to measure the text (assumes text is horizontal, centered at 0,0)
   protected Bound2 Measure (string text) {
      var font = LineFont.Get (mStyle.Style.Font);
      return font.Measure (text, Point2.Zero, ETextAlign.MidCenter, 0, 1, mStyle.TextSize, 0);      
   }

   // Nested types -------------------------------------------------------------
   // What lies on the 'inside' of the dimension (between the two extension lines)
   // The arrows and/or text can lie on the inside or the outside
   [Flags]
   protected enum EInside { None = 0, Text = 1, Arrows = 2, Both = 3 };
}
#endregion   

#region class E2DimGeneric -------------------------------------------------------------------------
/// <summary>E2DimGeneric is a 'fallback' dimension used when we don't have a suitable type</summary>
/// An E2DimGeneric loads and displays all the entities (looks fine visually), but cannot be 
/// edited nor exported to DXF
class E2DimGeneric : E2Dim {
   public E2DimGeneric (Layer2 layer, DimStyle2 style, IList<Point2> pts, string? text)
      : base (layer, EDim.Generic, style, pts, text) { }

   protected override void MakeEnts () => throw new NotImplementedException ();
   public override void GetDefPoints (List<(int, Point2)> defPoints) => throw new NotImplementedException ();

   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2DimGeneric (Layer, mStyle, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim; 
   }
}
#endregion

#region class E2Dim3PAngular -----------------------------------------------------------------------
/// <summary>E2Dim3PAngular is a '3-point angular' dimension</summary>
/// This image file://N:/Doc/Img/Dim3PAngle1.png shows the definition of this type of dimension.
/// The points a, b, c, d are enough to define the dimension (with point e being the optional
/// text positioning point). The values in parentheses are the group codes from which these values
/// are loaded from the DXF file. 
public class E2Dim3PAngular : E2Dim {
   // Constructors -------------------------------------------------------------
   E2Dim3PAngular () { }
   /// <summary>Creates a 3P-Angular dimension given the definition points as shown in the image above</summary>
   public E2Dim3PAngular (Layer2 layer, DimStyle2 style, IList<Point2> pts, string? text = null)
      : base (layer, EDim.Angular3P, style, pts, text) { }

   // Overrides ----------------------------------------------------------------
   public override void GetDefPoints (List<(int, Point2)> pts) {
      var _ = Ents;
      pts.Add ((10, mPts[3])); pts.Add ((11, mPts[4]));
      pts.Add ((13, mPts[1])); pts.Add ((14, mPts[2])); pts.Add ((15, mPts[0]));
   }

   // Creates a transformed version of the 3P Angular dimension
   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2Dim3PAngular (Layer, mStyle, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim; 
   }

   /// <summary>Creates the entities of the 3P Angular dimension</summary>
   /// Based on whether the TIHorz flag (text-inside horizontal) is set, and the text position
   /// (Center/Above/Below), the text will need to be positioned in one of the ways as shown
   /// in the image: file://N:/Doc/Img/Dim3PAngle2.png 
   /// 
   /// There are several helper routines in the E2Dim class that are used in this MakeEnts
   /// routine (and those of other classes)
   /// - Measure(text) computes the bounding box of the text (assuming the text is horizontal,
   ///   and centered at (0,0)). 
   /// - CheckSpace(available,txtSize) figures out if there is enough space between the
   ///   extension lines to position the arrowheads and/or text (4 possibilities exist for
   ///   this, as illustrated by file://N:/Doc/Img/Dim3PAngle3.png
   /// - GetOverhangs returns the overhang distances used for the dimension line overhangs
   ///   on either side of the extension lines. If both the arrowheads and text fit inside
   ///   the extension lines, these overhangs are both 0. Otherwise, the overhangs are computed
   ///   based on the arrowhead size / text measurement as shown in the images (the blue distances
   ///   are the overhangs: file://N:/Doc/Img/Dim3PAngle4.png
   /// - AddPoly, AddText, AddArrow are used to add various entities into the mEnts list.
   /// - When the text is placed with 'centered' alignment, and fits between the extension 
   ///   lines, the dimension line has to be trimmed to the bits on both sides of the text-box;
   ///   this is done by AddTrimmedSeg
   protected override void MakeEnts () {
      // Figure out if the CCW arc runs from b..c or from c..b (depends on the position of d)
      Point2 a = mPts[0], b = mPts[1], c = mPts[2], d = mPts[3];
      double angB = a.AngleTo (b), angC = a.AngleTo (c), rad = a.DistTo (d);
      Point2 arcStart = a.Polar (rad, angB), arcEnd = a.Polar (rad, angC);
      bool iBreak = mStyle.TextPos == DimStyle2.EPos.Centered;

      // Create the two extension lines - they run 'outward' or 'inward' from the
      // definition points b and c (depending on the position of d)
      double exo = mStyle.ExtOffset, ext = mStyle.ExtExtend, asz = mStyle.ArrowSize;
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
      Bound2 bound = Measure (text).InflatedL (mStyle.DimGap);
      double txtMeasure = bound.Width;
      if (mStyle.TIHorz && Math.Abs (Math.Cos (arcSeg.GetSlopeAt (0.5))) < 0.5) txtMeasure = bound.Height;
      var inside = CheckSpace (arcSeg.Length, txtMeasure);
      bool textInside = (inside & EInside.Text) != 0, arrowInside = (inside & EInside.Arrows) != 0;

      // Figure out the rotation angle of the text box, and an initial anchor point for
      // the text box
      int txtLie = -1; // -1:Inside, 0:Outside Start, 1:Outside End
      double tangentAngle = 0; Point2 textPos = arcSeg.Midpoint;
      bool textHorz = textInside ? mStyle.TIHorz : mStyle.TOHorz;
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
            shift += mStyle.ArrowSize;
            if (iBreak) shift += mStyle.ExtExtend;
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
      if (textInside && mStyle.TextPos == DimStyle2.EPos.Centered) AddTrimmedSeg (arcSeg, textBox);
      else AddPoly (arc);

      // If we are not doing 'centered' positioning, we have to shift the text box in a suitable
      // direction
      Vector2 vecShift = new (0, 0);
      if (!textHorz && mStyle.TextPos != DimStyle2.EPos.Centered) {
         double shiftLie = textInside ? 0.5 : txtLie, shiftAngle = a.AngleTo (arcSeg.GetPointAt (shiftLie));
         if (mStyle.TextPos == DimStyle2.EPos.Below) shiftAngle += Lib.PI;
         vecShift = GetShift (textBox, shiftAngle);
      } else if (textHorz && !textInside) 
         vecShift = GetShift2 (textBox, tangentAngle, !arrowInside);
      textPos += vecShift;
      // AddPoly (textBox * Matrix2.Translation (vecShift));

      // Add the text itself
      textAngle = Lib.NormalizeAngle (textAngle);
      if (textAngle is < (-Lib.HalfPI + Lib.Epsilon) or > (Lib.HalfPI + Lib.Epsilon))
         textAngle += Lib.PI;
      if (mPts.Count < 5) mPts.Add (Point2.Zero);
      mPts[4] = textPos;
      AddText (textPos, text, textAngle);
      AddPoint (a); AddPoint (b); AddPoint (c);
   }

   Vector2 GetShift (Poly rect, double angle) {
      Point2 cen = rect.Pts[0].Midpoint (rect.Pts[2]);
      Point2 outer = cen.Polar (1000, angle);
      foreach (var seg in rect.Segs) {
         Point2 pa = Geo.LineSegXLineSeg (seg.A, seg.B, cen, outer);
         if (!pa.IsNil) return pa - cen;
      }
      return Vector2.Zero;
   }

   Vector2 GetShift2 (Poly rect, double angle, bool arrowOutside) {
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
}
#endregion
