// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3VN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

abstract class Ent3VN (Ent3 ent) : VNode (ent) {
   public override void SetAttributes () => Lux.Color = Color4.White;
}

class E3SurfaceVN : Ent3VN {
   public E3SurfaceVN (E3Surface surface) : base (surface) => mSurface = surface;
   readonly E3Surface mSurface;

   public override void Draw () => Lux.Mesh (mSurface.Mesh, EShadeMode.Phong);
}
