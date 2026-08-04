// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXApi.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using JetBrains.Annotations;
using Nori;
namespace UXDemo;
using static Size;
using static UXTheme;

public static class UXApi {
   public static void Init () {
      UXSystem.Register (new RectClass ());
      UXSystem.Register (new PanelClass ());
      UXSystem.Register (new TextClass ());
      UXSystem.Register (new RootClass ());
      UXSystem.Register (new MTextClass ());
      UXSystem.Register (new BlockClass ());
      UXSystem.Register (new VScrollClass ());
      UXSystem.Register (new PopupClass ());
      UXSystem.Register (new ListboxClass ());
      UXSystem.Register (new CListBoxClass ());
      UXSystem.Register (new WrapListClass ());
   }

   public static void END () {
      UXSystem.EndNode ();
   }

   public static ref Node BLOCK (uint uid, Size width, Size height, Color4 bgrd, Color4 over, Color4 hover) {
      ref Node node = ref UXSystem.BeginNode (EKind.Block, uid, width, height);
      node.BgrdColor = node.IsHovered (1000) ? hover : (node.IsMouseOver ? over : bgrd);
      return ref node;
   }

   public static ref Node LISTBOX (uint uid, Size width, Size height, IReadOnlyList<object> items, int selected) {
      ref Node node = ref PANEL (uid, width, height, true, LISTBOX_Bgrd);
      int a = LISTBOX_Margin, b = SCROLLBAR_Margin;
      node.SetPadding (a, a, b, a);
      VSCROLL (uid, Grow (), Grow (), Color4.Transparent);
      node = ref UXSystem.BeginNode (EKind.Listbox, uid, Grow (), Grow ()); END ();
      END ();
      node.GetMemo ().Data ??= new ListboxClass.Data (items, selected);
      return ref node;
   }

   public static ref Node WRAPLIST (uint uid, Size width, Size height, ICustomList list) {
      ref Node node = ref PANEL (uid++, width, height, true, LISTBOX_Bgrd);
      int a = LISTBOX_Margin, b = SCROLLBAR_Margin;
      node.SetPadding (a, a, b, a); 
      VSCROLL (uid++, Grow (), Grow (), Color4.Transparent);
      node = ref UXSystem.BeginNode (EKind.WrapList, uid++, Grow (), Grow ()); node.ChildGap = 6; END ();
      END ();  // VScroll
      node.GetMemo ().Data ??= new WrapListClass.Data (list);
      return ref node;
   }

   public static ref Node CLISTBOX (uint uid, Size width, Size height, ICustomList list) {
      ref Node node = ref PANEL (uid, width, height, false, LISTBOX_Bgrd);
      int a = LISTBOX_Margin, b = SCROLLBAR_Margin; node.SetPadding (a, a, b, a);
      VSCROLL (uid, Grow (), Grow (), Color4.Transparent);
      node = ref UXSystem.BeginNode (EKind.CListBox, uid, width.Min - a - b - VScrollClass.WIDTH, Grow ()); END ();
      END (); // VScroll
      node.GetMemo ().Data = new CListBoxClass.Data (list);
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

   public static ref Node POPUP (uint uid, Size width, Size height, Color4 bgrd, ECorner parentCorner, ECorner elemCorner, Vec2S offset, bool screenAlign = false) {
      ref Node node = ref UXSystem.BeginNode (EKind.Popup, uid, width, height);
      node.BgrdColor = bgrd;
      node.ElemCorner = elemCorner; node.ParentCorner = parentCorner; node.FloatOffset = offset;
      node.IsScreenRelative = screenAlign;
      return ref node;
   }

   public static ref Node RECT (uint uid, Size width, Size height, Color4 bgrd) {
      ref Node node = ref UXSystem.BeginNode (EKind.Rect, uid, width, height);
      node.BgrdColor = bgrd;
      return ref node;
   }

   public static ref Node VSCROLL (uint uid, Size width, Size height, Color4 color) {
      ref Node node = ref UXSystem.BeginNode (EKind.VScroll, uid, width, height);
      node.BgrdColor = Color4.Red; node.IsHorizontal = true; node.FgrdColor = color;
      node.X.PadEnd = VScrollClass.WIDTH;
      return ref node;
   }

   public static ref Node VSCROLL (uint uid, Color4 color) => ref VSCROLL (uid, Size.Grow (), Size.Grow (), color);

   public static ref Node TEXT (uint uid, string text, int fontId, Color4 textColor, Color4 bgrdColor) {
      ref Node node = ref UXSystem.BeginNode (EKind.Text, uid);
      node.FgrdColor = textColor; node.BgrdColor = bgrdColor; 
      node.FontId = (short)fontId; node.Text = text;
      return ref node; 
   }

   public static ref Node TEXT (uint uid, string text, int fontId, Color4 textColor)
      => ref TEXT (uid, text, fontId, textColor, Color4.Transparent);
}
