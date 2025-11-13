// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Model3VN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class Model3VN : VNode {
   public Model3VN (Model3 model) : base (model) { mModel = model; ChildSource = mModel.Ents; }
   readonly Model3 mModel;
}
