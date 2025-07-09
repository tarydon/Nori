// ────── ╔╗
// ╔═╦╦═╦╦╬╣ EntVN.cs
// ║║║║╬║╔╣║ Implements VNodes for various types of entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Ent2VN -------------------------------------------------------------------------------
/// <summary>Ent2VN is the base class for all VNodes that render different types of Ent2</summary>
abstract class Ent2VN (Ent2 ent) : VNode (ent) {
   // Overrides ----------------------------------------------------------------
   public override void SetAttributes () {
      if (!ent.InBlock) {
         var layer = ent.Layer;
         Lux.Color = ent.IsSelected ? Color4.Blue : (ent.Color.IsNil ? layer.Color : ent.Color);
         if (ent.IsSelected) { Lux.LineWidth = 6f; Lux.PointSize = 11f; }
         if (ent is not E2Text) Lux.LineType = layer.Linetype;
      }
   }
}
#endregion

#region E2BendlineVN -------------------------------------------------------------------------------
/// <summary>VNode to render a bendline</summary>
/// A bendline is drawn as a green line using either the Dash2 linetype (+ve bends) or the
/// DashDotDot linetype (-ve bends)
class E2BendlineVN (E2Bendline e2b) : Ent2VN (e2b) {
   // Draw the actual lines
   public override void Draw () {
      var pts = mBend.Pts.Select (a => (Vec2F)a).ToList ();
      Lux.Lines (pts.AsSpan ());
   }
   readonly E2Bendline mBend = e2b;

   // The child of this node is used to draw the actual bend angle annotation
   public override VNode? GetChild (int n) {
      mChild ??= new SimpleVN (() => Lux.Color = Color4.Black, DrawText);
      return n == 0 ? mChild : null;
   }
   SimpleVN? mChild;

   // Set up the green color used for bendlines, and the appropriate linetype
   public override void SetAttributes () {
      Lux.Color = new Color4 (0, 192, 0);
      Lux.LineType = mBend.Angle > 0 ? ELineType.Dash2 : ELineType.DashDotDot;
   }

   // Helper used to draw the bend angle annotation
   void DrawText () {
      string text = mBend.Angle.R2D ().S6 ();
      text = mBend.Angle > 0 ? $"+{text}\u00b0" : $"{text}\u00b0";
      for (int i = 0; i < mBend.Pts.Length; i += 2) {
         Point2 pt = mBend.Pts[i].Midpoint (mBend.Pts[i + 1]);
         Lux.Text2D (text, (Vec2F)pt, ETextAlign.MidCenter);
      }
   }
}
#endregion

#region class E2InsertVN ---------------------------------------------------------------------------
/// <summary>VNode to render an E2Insert</summary>
/// An E2Insert references a block and provides a transformation for it, comprising of:
/// - Scaling (could be unequal in X and Y)
/// - Rotation
/// - Translation to insertion point
/// It is important to note that the translation will map the _reference point_ of the block
/// (which is not necessarily 0,0) to the insertion point of the E2Insert.
class E2InsertVN (E2Insert e2i) : Ent2VN (e2i) {
   // The only child for the E2Insert is the BlockVN. Since we call BlockVN.For that
   // constructs a shared BlockVN that is used by all the inserts (only the transform distinguishes
   // them from each other)
   public override VNode? GetChild (int n) => n == 0 ? Block2VN.Get (e2i.Block) : null;

   // The attribute we set pushes this Insert's Xfm on the draw stack.
   public override void SetAttributes () { base.SetAttributes (); Lux.Xfm = (Matrix3)e2i.Xfm; }
}
#endregion

#region class E2DimensionVN ------------------------------------------------------------------------
/// <summary>VNode to render all types of dimensions</summary>
/// Regardless of the type of dimension, it gets 'rendered' into the set of entities called
/// Ents (those entities will typically contain text, lines, arcs, arrow-heads etc). So all dimension
/// rendering can be done by this base class
class E2DimensionVN (E2Dimension e2d) : Ent2VN (e2d) {
   public override VNode? GetChild (int n) {
      Ent2? ent = e2d.Ents.SafeGet (n);
      return ent == null ? null : MakeFor (ent);
   }
}
#endregion

#region class E2PolyVN -----------------------------------------------------------------------------
/// <summary>VNode to render an E2Poly entity</summary>
class E2PolyVN (E2Poly e2p) : Ent2VN (e2p) {
   public override void Draw () => Lux.Poly (e2p.Poly);
}
#endregion

#region class E2TextVN -----------------------------------------------------------------------------
/// <summary>VNode to render an E2Text entity</summary>
class E2TextVN (E2Text e2t) : Ent2VN (e2t) {
   public override void Draw () => Lux.Polys (e2t.Polys.AsSpan ());
}
#endregion

#region class E2PointVN ----------------------------------------------------------------------------
/// <summary>VNode to render an E2Point entity</summary>
class E2PointVN (E2Point e2p) : Ent2VN (e2p) {
   public override void Draw () => Lux.Points ([e2p.Pt]);
}
#endregion

#region class E2SolidVN ----------------------------------------------------------------------------
/// <summary>VNode to render an E2Solid entity</summary>
class E2SolidVN (E2Solid e2p) : Ent2VN (e2p) {
   Vec2F[] mPoints = [.. e2p.Pts.Select (pt => (Vec2F)pt)];
   public override void Draw () => Lux.Quads (mPoints);
}
#endregion
