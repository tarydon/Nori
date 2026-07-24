// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXApi.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace UXDemo;

public static class UXApi {
   public static void Init () {
      UXSystem.Register (new RectClass ());
      UXSystem.Register (new PanelClass ());
      UXSystem.Register (new TextClass ());
      UXSystem.Register (new RootClass ());
      UXSystem.Register (new MTextClass ());
   }

   public static void END () {
      UXSystem.EndNode ();
   }

   public static ref Node PANEL (Size width, Size height, bool horizontal, Color4 bgrd) {
      ref Node node = ref UXSystem.BeginNode (EKind.Panel, width, height);
      node.BgrdColor = bgrd; node.IsHorizontal = horizontal;
      return ref node;
   }

   public static ref Node RECT (Size width, Size height, Color4 bgrd) {
      ref Node node = ref UXSystem.BeginNode (EKind.Rect, width, height);
      node.BgrdColor = bgrd;
      return ref node;
   }

   public static ref Node TEXT (string text, int fontId, Color4 textColor) 
      => ref TEXT (text, fontId, textColor, Color4.Transparent);

   public static ref Node TEXT (string text, int fontId, Color4 textColor, Color4 bgrdColor) {
      ref Node node = ref UXSystem.BeginNode (EKind.Text);
      node.FgrdColor = textColor; node.BgrdColor = bgrdColor; 
      node.FontId = (short)fontId; node.Text = text;
      return ref node; 
   }

   public static ref Node MTEXT (uint uid, string text, int fontId, Color4 textColor, Color4 bgrdColor) {
      ref Node node = ref UXSystem.BeginNode (EKind.MText);
      node.Data = uid; node.FgrdColor = textColor; node.BgrdColor = bgrdColor;
      node.FontId = (short)fontId; node.Text = text;
      return ref node;
   }
}
