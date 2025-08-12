// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MechanismVN.cs
// ║║║║╬║╔╣║ MechanismVN is used to render mechanisms (one VN for each joint)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class MechanismVN : VNode {
   public MechanismVN (Mechanism mech) : base (mech) => mMech = mech;
   Mechanism mMech;

   public override VNode? GetChild (int n)
      => n < mMech.Children.Count ? new MechanismVN (mMech.Children[n]) : null;

   public override void SetAttributes () {
      Lux.Color = mMech.Color;
      Lux.Xfm = mMech.RelativeXfm;
   }

   public override void Draw () {
      if (mMech.Mesh != null) Lux.Mesh (mMech.Mesh, EShadeMode.Phong);
   }
}
