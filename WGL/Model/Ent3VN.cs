// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3VN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

abstract class Ent3VN (Ent3 ent) : VNode (ent) {
   public override void SetAttributes () => Lux.Color = Color4.Yellow;
}

class E3PlaneVN : Ent3VN {
   public E3PlaneVN (E3Plane plane) : base (plane) => mPlane = plane;
   readonly E3Plane mPlane;

   public override void Draw () => Lux.Mesh (mPlane.Mesh, EShadeMode.Gourad);
}
