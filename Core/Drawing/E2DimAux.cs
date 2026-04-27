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

public enum EDim { 
   Linear = 0, Aligned = 1, Angular = 2, Diameter = 3, Radius = 4, Angular3P = 5, Ordinate = 6, Generic = 32
};

