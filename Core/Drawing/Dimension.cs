// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dimension.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DimStyle2 ----------------------------------------------------------------------------
/// <summary>Dimension style (based on AutoCAD)</summary>
public class DimStyle2 {
   public DimStyle2 (string name, double asz, double exo, double exe, double txt, double cen, double gap, int tih, int toh, int tofl, int tabove) {
      Name = name;
      ArrowSize = (float)asz; ExtOffset = (float)exo; ExtExtend = (float)exe;
      TxtSize = (float)txt; DimCen = (float)cen; Gap = (float)gap;
      TIHorz = tih > 0; TOHorz = toh > 0; TOfl = tofl > 0; TVertPos = tabove;
   }

   public readonly string Name;        // 2
   public readonly float ArrowSize;    // 41 - Length of arrow
   public readonly float ExtOffset;    // 42 - Extension line offset from dim definition point
   public readonly float ExtExtend;    // 44 - Extension line extend beyond dimension line
   public readonly float TxtSize;      // 140 - Text size
   public readonly float DimCen;       // 141 - Size of center mark/lines
   public readonly float Gap;          // 147 - Gap between dimension line & text

   public readonly bool TIHorz;        // 73 - Text inside dimension line horizontal?
   public readonly bool TOHorz;        // 74 - Text outside dimension line horizontal?
   public readonly bool TOfl;          // 172 - Draw line between extension lines even when text is placed outside

   public readonly int TVertPos;       // 77 - 0=Centered, 1=Above, 4=Below (default = above)
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
   
