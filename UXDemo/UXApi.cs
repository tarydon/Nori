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
      UXSystem.Register (new BlockClass ());
      UXSystem.Register (new VScrollClass ());
   }

   public static void END () {
      UXSystem.EndNode ();
   }

   public static ref Node BLOCK (uint uid, Size width, Size height, Color4 bgrd, Color4 over, Color4 hover) {
      ref Node node = ref UXSystem.BeginNode (EKind.Block, uid, width, height);
      node.BgrdColor = node.IsHovered (1000) ? hover : (node.IsMouseOver ? over : bgrd);
      return ref node;
   }

   public static ref Node MTEXT (uint uid, string text, int fontId, Color4 textColor, Color4 bgrdColor) {
      ref Node node = ref UXSystem.BeginNode (EKind.MText, uid);
      node.FgrdColor = textColor; node.BgrdColor = bgrdColor;
      node.FontId = (short)fontId; node.Text = text;
      return ref node;
   }

   public static ref Node PANEL (uint uid, Size width, Size height, bool horizontal, Color4 bgrd) {
      ref Node node = ref UXSystem.BeginNode (EKind.Panel, uid, width, height);
      node.BgrdColor = bgrd; node.IsHorizontal = horizontal;
      return ref node;
   }

   public static ref Node RECT (uint uid, Size width, Size height, Color4 bgrd) {
      ref Node node = ref UXSystem.BeginNode (EKind.Rect, uid, width, height);
      node.BgrdColor = bgrd;
      return ref node;
   }

   public static ref Node VSCROLL (uint uid, Size width, Size height) {
      ref Node node = ref UXSystem.BeginNode (EKind.VScroll, uid, width, height);
      node.BgrdColor = Color4.Red; node.IsHorizontal = true;
      node.X.PadEnd = 40;  // SCROLL-WIDTH
      return ref node;
   }

   public static ref Node VSCROLL (uint uid) => ref VSCROLL (uid, Size.Grow (), Size.Grow ());

   public static ref Node TEXT (uint uid, string text, int fontId, Color4 textColor, Color4 bgrdColor) {
      ref Node node = ref UXSystem.BeginNode (EKind.Text, uid);
      node.FgrdColor = textColor; node.BgrdColor = bgrdColor; 
      node.FontId = (short)fontId; node.Text = text;
      return ref node; 
   }

   public static ref Node TEXT (uint uid, string text, int fontId, Color4 textColor)
      => ref TEXT (uid, text, fontId, textColor, Color4.Transparent);
}
