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
   public DimStyle2 (string name, float scale, float asz, float exo, float exe, float txt, float cen, float gap, int tih, int toh, int tofl, int tabove, int dec, int adec, Style2 style) {
      Name = name; Style = style;
      ArrowSize = asz * scale; ExtOffset = exo * scale; ExtExtend = exe * scale; 
      TxtSize = txt * scale; DimCen = cen * scale; DimGap = gap * scale;
      TPos = tabove switch { 0 => EPos.Centered, 4 => EPos.Below, _ => EPos.Above };
      LinDecimal = dec; AngDecimal = adec;
      if (tih > 0) mFlags |= EFlags.TIHorz;
      if (toh > 0) mFlags |= EFlags.TOHorz;
      if (tofl > 0) mFlags |= EFlags.TOFL;
   }

   public DimStyle2 (string name, Style2 style) {
      Name = name; Style = style;
      ArrowSize = TxtSize = DimCen = 2.5f; 
      ExtOffset = DimGap = 0.625f;
      ExtExtend = 1.25f;
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
   public readonly float TxtSize;
   /// <summary>Size of center mark / center lines (DXF Group 141)</summary>
   public readonly float DimCen;
   /// <summary>Gap between dimension line and text (DXF Group 147)</summary>
   public readonly float DimGap;

   /// <summary>Number of decimal places for linear dimensions</summary>
   public readonly int LinDecimal;
   /// <summary>Number of decimal places for angular dimensions</summary>
   public readonly int AngDecimal;

   /// <summary>Text inside dimension line horizontal (DXF Group 73)</summary>
   public bool TIHorz => (mFlags & EFlags.TIHorz) != 0;
   /// <summary>Text outside dimension line horizontal (DXF Group 74)</summary>
   public bool TOHorz => (mFlags & EFlags.TOHorz) != 0;
   /// <summary>Draw line between extension lines even when text is outside (DXF Group 172)</summary>
   public bool ForceDimLin => (mFlags & EFlags.TOFL) != 0;

   /// <summary>Text position (Centered / Above / Below)</summary>
   public readonly EPos TPos;

   /// <summary>
   /// Text style used for dimensions
   /// </summary>
   public readonly Style2 Style;

   // Nested types -------------------------------------------------------------
   [Flags]
   enum EFlags { TIHorz = 1 << 0, TOHorz = 1 << 1, TOFL = 1 << 2, }
   EFlags mFlags;

   public enum EPos { Centered = 0, Above = 1, Below = 4 }
}
#endregion

public enum EDim { 
   Linear = 0, Aligned = 1, Angular = 2, Diameter = 3, Radius = 4, Angular3P = 5, Ordinate = 6, Generic = 32
};

#region class E2Dim --------------------------------------------------------------------------------
public abstract class E2Dim : Ent2 {
   // Constructors -------------------------------------------------------------
   protected E2Dim () => mStyle = null!;
   public E2Dim (Layer2 layer, EDim kind, DimStyle2 style, IList<Point2> pts, string text) : base (layer) {
      (mKind, mPts, mStyle) = (kind, [.. pts], style);
      if (text != "") mText = text;
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the dimension</summary>
   public override Bound2 Bound
      => Bound2.Cached (ref mBound, () => new (Ents.Select (a => a.Bound)));
   Bound2 mBound = new ();

   /// <summary>The DimStyle used by this dimension entity</summary>
   public DimStyle2 Style => mStyle;
   protected DimStyle2 mStyle;

   /// <summary>The entities making up the dimension</summary>
   /// In DXF, this is stored in a block, but since we never reuse that, we just store the
   /// entities here and create a block on the fly when saving the dimension
   public IReadOnlyList<Ent2> Ents {
      get {
         if (mEnts.Count == 0) MakeEnts ();
         return mEnts;
      }
   }
   List<Ent2> mEnts = [];

   /// <summary>Which kind of dimension is this?</summary>
   public EDim Kind => mKind;
   EDim mKind;

   /// <summary>Set of points defining the dimension (interpretation depends on the kind)</summary>
   public IReadOnlyList<Point2> Pts => mPts;
   protected Point2[] mPts = [];

   /// <summary>The text of the dimension, if not blank</summary>
   /// If this is "<>" or "" or null, the default text is used (based on the actual 
   /// measurement). If this is " ", then the text is suppressed.
   public string? Text => mText;
   string? mText;

   // Methods ------------------------------------------------------------------
   /// <summary>Get the transformed bound of the dimension</summary>
   public override Bound2 GetBound (Matrix2 xfm) 
      => new (Ents.Select (a => a.GetBound (xfm)));

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

   /// <summary>Creates a transformed version of this dimension</summary>
   protected override E2Dim Xformed (Matrix2 m)
      => throw new NotImplementedException ();

   // Implementation -----------------------------------------------------------
   protected abstract void MakeEnts ();

   protected void AddPoly (Poly poly) => mEnts.Add (new E2Poly (Layer = Layer, poly));

   protected void AddArrow (Point2 pt, double angle) {
      double len = mStyle.ArrowSize, hwid = len / 6;
      Point2 pa = pt.Polar (len, angle);
      Point2 pb = pa.Polar (hwid, angle + Lib.HalfPI); pa = pa.Polar (hwid, angle - Lib.HalfPI);
      mEnts.Add (new E2Solid (Layer, [pa, pb, pt, pt]));
   }

   protected void AddText (Point2 pt, string text, double angle) {
      mEnts.Add (new E2Text (Layer, mStyle.Style, text, pt, mStyle.TxtSize, angle, 0, 1, ETextAlign.MidCenter)); 
   }
}
#endregion   

class E2DimGeneric : E2Dim {
   public E2DimGeneric (Layer2 layer, DimStyle2 style, IList<Point2> pts, string text)
      : base (layer, EDim.Generic, style, pts, text) { }

   protected override void MakeEnts () => throw new NotImplementedException ();
}

class E2Dim3PAngular : E2Dim {
   E2Dim3PAngular () { }
   public E2Dim3PAngular (Layer2 layer, DimStyle2 style, IList<Point2> pts, string text)
      : base (layer, EDim.Angular3P, style, pts, text) { }

   protected override void MakeEnts () {
      double exo = mStyle.ExtOffset, ext = mStyle.ExtExtend;
      Point2 cen = mPts[4], p3 = mPts[2], p4 = mPts[3], ptxt = mPts[1];
      double ang3 = cen.AngleTo (p3), ang4 = cen.AngleTo (p4), rad = cen.DistTo (mPts[0]);
      p3 = cen.Polar (cen.DistTo (p3) + exo, ang3); 
      p4 = cen.Polar (cen.DistTo (p4) + exo, ang4);
      Point2 p3a = cen.Polar (rad + ext, ang3), p4a = cen.Polar (rad + ext, ang4);

      AddPoly (Poly.Line (p3, p3a)); AddPoly (Poly.Line (p4, p4a));
      Vector3 v3 = (Vector3)(p3 - cen), v4 = (Vector3)(p4 - cen);
      if ((v3 * v4).Z < 0) (ang3, ang4) = (ang4, ang3);
      Poly arc = Poly.Arc (cen, rad, ang3, ang4, true); AddPoly (arc);
      AddArrow (arc.A, ang3 + Lib.HalfPI); AddArrow (arc.B, ang4 - Lib.HalfPI);
      string? text = Text;
      if (text == null) {
         double span = Math.Abs (arc[0].AngSpan).R2D ().Round (mStyle.AngDecimal);
         text = span.ToString () + "\u00b0";
      }
      double tAng = cen.AngleTo (ptxt) - Lib.HalfPI;
      AddText (ptxt, text, tAng);
   }
}
