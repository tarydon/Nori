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
   public override void Draw () => Lux.Mesh (mSurface.Mesh, EShadeMode.Phong);
}
