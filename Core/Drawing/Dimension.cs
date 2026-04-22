// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dimension.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DimStyle2 ----------------------------------------------------------------------------
/// <summary>Dimension style (based on AutoCAD)</summary>
public class DimStyle2 {
   // Constructor --------------------------------------------------------------
   DimStyle2 () => Name = "";
   public DimStyle2 (string name, float scale, float asz, float exo, float exe, float txt, float cen, float gap, int tih, int toh, int tofl, int tabove, int dec, int adec) {
      Name = name;
      ArrowSize = asz * scale; ExtOffset = exo * scale; ExtExtend = exe * scale; 
      TxtSize = txt * scale; DimCen = cen * scale; DimGap = gap * scale;
      TPos = tabove switch { 0 => EPos.Centered, 4 => EPos.Below, _ => EPos.Above };
      LinDecimal = dec; AngDecimal = adec;
      if (tih > 0) mFlags |= EFlags.TIHorz;
      if (toh > 0) mFlags |= EFlags.TOHorz;
      if (tofl > 0) mFlags |= EFlags.TOFL;
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

   /// <summary>
   /// Number of decimal places for linear dimensions
   /// </summary>
   public readonly int LinDecimal;
   /// <summary>
   /// Number of decimal places for angular dimensions
   /// </summary>
   public readonly int AngDecimal;

   /// <summary>Text inside dimension line horizontal (DXF Group 73)</summary>
   public bool TIHorz => (mFlags & EFlags.TIHorz) != 0;
   /// <summary>Text outside dimension line horizontal (DXF Group 74)</summary>
   public bool TOHorz => (mFlags & EFlags.TOHorz) != 0;
   /// <summary>Draw line between extension lines even when text is outside (DXF Group 172)</summary>
   public bool ForceDimLin => (mFlags & EFlags.TOFL) != 0;

   /// <summary>Text position (Centered / Above / Below)</summary>
   public readonly EPos TPos;

   // Nested types -------------------------------------------------------------
   [Flags]
   enum EFlags { TIHorz = 1 << 0, TOHorz = 1 << 1, TOFL = 1 << 2, }
   EFlags mFlags;

   public enum EPos { Centered = 0, Above = 1, Below = 4 }
}
#endregion

public enum EDim { 
   Linear = 0, Aligned = 1, Angular = 2, Diameter = 3, Radius = 4, Angular3P = 5, Ordinate = 6 
};

#region class E2Dim --------------------------------------------------------------------------------
public class E2Dim : Ent2 {
   // Constructors -------------------------------------------------------------
   protected E2Dim () => mStyle = null!;
   public E2Dim (Layer2 layer, DimStyle2 style, EDim kind, IList<Point2> pts) : base (layer) 
      => (mKind, mPts, mStyle) = (kind, [.. pts], style);

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the dimension</summary>
   public override Bound2 Bound
      => Bound2.Cached (ref mBound, () => new (mEnts.Select (a => a.Bound)));
   Bound2 mBound = new ();

   public DimStyle2 Style => mStyle;
   DimStyle2 mStyle;

   /// <summary>The entities making up the dimension</summary>
   /// In DXF, this is stored in a block, but since we never reuse that, we just store the
   /// entities here and create a block on the fly when saving the dimension
   public ReadOnlySpan<Ent2> Ents => mEnts;
   Ent2[] mEnts = [];

   /// <summary>
   /// Which kind of dimension is this?
   /// </summary>
   public EDim Kind => mKind;
   EDim mKind;

   // Set of points defining this dimension - interpretation depends on the dimension type
   public IReadOnlyList<Point2> Pts => mPts;
   protected Point2[] mPts = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Get the transformed bound of the dimension</summary>
   public override Bound2 GetBound (Matrix2 xfm) => new (mEnts.Select (a => a.GetBound (xfm)));

   /// <summary>
   /// Internal routine to load the entities from a block
   /// </summary>
   /// Since blocks are not used by any dimension other than this one, we can load the entities
   /// here and later discard that block
   internal Block2? LoadEnts (Dwg2 dwg, string name) {
      if (dwg.Blocks.FirstOrDefault (a => a.Name == name) is { } block) {
         mEnts = [.. block.Ents];
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
}
#endregion   