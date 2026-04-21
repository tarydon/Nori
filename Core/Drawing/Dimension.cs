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
   public DimStyle2 (string name, float scale, float asz, float exo, float exe, float txt, float cen, float gap, int tih, int toh, int tofl, int tabove) {
      Name = name;
      ArrowSize = asz * scale; ExtOffset = exo * scale; ExtExtend = exe * scale; 
      TxtSize = txt * scale; DimCen = cen * scale; DimGap = gap * scale;
      TPos = tabove switch { 0 => EPos.Centered, 4 => EPos.Below, _ => EPos.Above };
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

#region class E2Dim --------------------------------------------------------------------------------
public class E2Dim : Ent2 {
   // Constructors -------------------------------------------------------------
   protected E2Dim () { }
   public E2Dim (Layer2 layer) : base (layer) { }

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the dimension</summary>
   public override Bound2 Bound
      => Bound2.Cached (ref mBound, () => new (mEnts.Select (a => a.Bound)));
   Bound2 mBound = new ();

   /// <summary>The entities making up the dimension</summary>
   /// In DXF, this is stored in a block, but since we never reuse that, we just store the
   /// entities here and create a block on the fly when saving the dimension
   public ReadOnlySpan<Ent2> Ents => mEnts;
   Ent2[] mEnts = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Get the transformed bound of the dimension</summary>
   public override Bound2 GetBound (Matrix2 xfm) => new (mEnts.Select (a => a.GetBound (xfm)));

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

#region class E2DimDia -----------------------------------------------------------------------------
/// <summary>Implements a 'diameter' dimension</summary>
public class E2DimDia : E2Dim {
   // Constructors -------------------------------------------------------------
   E2DimDia () { }
   public E2DimDia (Layer2 layer) : base (layer) { }
}   
#endregion
   
