// ────── ╔╗
// ╔═╦╦═╦╦╬╣ API.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static UXNode;

public static class UXApi {
   public static void END () {
      UXEngine.EndNode ();
   }

   public static ref UXNode PANEL (uint uid, Size width, Size height, bool horizontal, Color4 bgrd) {
      ref UXNode node = ref UXEngine.BeginNode (EKind.Panel, uid, width, height);
      node.BgrdColor = bgrd; node.IsHorizontal = horizontal;
      return ref node;
   }
}
