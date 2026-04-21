// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dimension.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DimStyle2 ----------------------------------------------------------------------------
/// <summary>Dimension style (based on AutoCAD)</summary>
public class DimStyle2 {
   public DimStyle2 (string name, float asz, float exo, float exe, float txt, float cen, float gap, int tih, int toh, int tofl, int tabove) {
      Name = name;
      ArrowSize = asz; ExtOffset = exo; ExtExtend = exe; TxtSize = txt; DimCen = cen; Gap = gap;
      TPos = tabove switch { 0 => EPos.Centered, 4 => EPos.Below, _ => EPos.Above };
      if (tih > 0) mFlags |= EFlags.TIHorz;
      if (toh > 0) mFlags |= EFlags.TOHorz;
      if (tofl > 0) mFlags |= EFlags.TOfl;
   }

   public readonly string Name;        // 2
   public readonly float ArrowSize;    // 41 - Length of arrow
   public readonly float ExtOffset;    // 42 - Extension line offset from dim definition point
   public readonly float ExtExtend;    // 44 - Extension line extend beyond dimension line
   public readonly float TxtSize;      // 140 - Text size
   public readonly float DimCen;       // 141 - Size of center mark/lines
   public readonly float Gap;          // 147 - Gap between dimension line & text

   public bool TIHorz => (mFlags & EFlags.TIHorz) != 0;
   public bool TOHorz => (mFlags & EFlags.TOHorz) != 0;
   public bool TOfl => (mFlags & EFlags.TOfl) != 0;

   [Flags]
   enum EFlags {
      TIHorz = 1 << 0,                 // 73 - Text iside dimension line horizontal
      TOHorz = 1 << 1,                 // 74 - Text outside dimension line horizontal
      TOfl = 1 << 2,                   // 172 - Draw line between extension lines even when text is outside
   }
   EFlags mFlags;

   public enum EPos { Centered = 0, Above = 1, Below = 4 }
   public readonly EPos TPos;          // 77 - 0=Centered, 1=Above, 4=Below
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
/// <summary>
/// Implements a 'diameter' dimension
/// </summary>
public class E2DimDia : E2Dim {
   // Constructors -------------------------------------------------------------
   E2DimDia () { }
   public E2DimDia (Layer2 layer) : base (layer) { }
}   
#endregion
   
