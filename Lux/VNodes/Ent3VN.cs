// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3VN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

abstract class Ent3VN (Ent3 mEnt) : VNode (mEnt) {
   public override void SetAttributes () {
      Lux.Color = mEnt.IsSelected ? new Color4 (128, 192, 255) : Color4.White;
   }
}

class E3SurfaceVN (E3Surface mSurface) : Ent3VN (mSurface) {
   public override void Draw () 
      => Lux.Mesh (mSurface.Mesh, mSurface.IsTranslucent ? EShadeMode.GlassNoStencil : EShadeMode.Phong);
}

class E3CurveVN (E3Curve mCurve) : Ent3VN (mCurve) {
   public override void Draw () {
      List<Point3> pts = [];
      mCurve.Edge.Discretize (pts, Lib.FineTess, 0.541);
      pts.Add (mCurve.Edge.End);

      List<Vec3F> vec = [(Vec3F)pts[0]];
      for (int i = 1; i < pts.Count; i++) { vec.Add ((Vec3F)pts[i]); vec.Add ((Vec3F)pts[i]); }
      vec.RemoveLast ();
      Lux.Lines (vec.AsSpan ());
   }
}
