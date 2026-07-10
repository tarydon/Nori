// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXFrameElem.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.UX;
using static SizeS;
using static UXNode.EOrientation;
using static UXTheme;

public static partial class UXApi {
   public static bool TOPMENU () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.TopMenu;
      a.BgrdColor = MENUBAR_Bgrd; a.Padding = MENUBAR_Padding;
      a.Width = Grow ();
      return true;
   }

   public static ref UXNode FILLER (int min = 0) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Width = Grow (min); a.Height = Grow ();
      UXFrame.EndNode ();
      return ref a;
   }

   public static bool MENUITEM_P (string text) {
      string? shortcut = UXFrame.N.Kind == UXNode.EKind.TopMenu ? null : "\u0034";
      return MENUITEM (text, shortcut, popup: true);
   }

   public static bool MENUITEM (string text, bool disable, bool popup = false)
      => MENUITEM (text, null, disable, popup);

   public static bool MENUITEM (string text, string? shortcut = null, bool disable = false, bool popup = false) {
      ref UXNode a = ref UXFrame.BeginNode ();
      // Set up bgrd color (different when the menu is hovered)
      a.BgrdColor = a.IsHovered || a.AnyPopupsOpen ? MENUITEM_Bgrd_H : MENUITEM_Bgrd;
      // Set up padding, corner radius, and if this a POPUP-MENU item (as opposed to a top level
      // MENU-BAR item), set it to Grow()
      a.Padding = MENUITEM_Padding; a.CornerRadius = MENUITEM_Radius; a.ChildAlignY = UXNode.EChildAlignY.Middle;
      if (a.GetParent ().Kind != UXNode.EKind.TopMenu) a.Width = Grow ();
      TEXT (text, (a.Disabled = disable) ? MENUITEM_Text_D : MENUITEM_Text);
      if (shortcut != null) {
         FILLER (80);
         TEXT (shortcut, MENUITEM_Shortcut, shortcut.Length == 1 ? 1 : 0);
      }
      if (a.Disabled) return false;
      if (popup)
         return a.IsHovered || a.AnyPopupsOpen;
      else
         return a.IsReleased;
   }

   public static bool POPUPMENU () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.PopupMenu;
      a.Floating = true; a.Orientation = TopToBottom;
      a.ElemCorner = UXNode.ECorner.LeftTop;
      if (a.GetGrandParent ().Kind == UXNode.EKind.TopMenu)
         a.ParentCorner = UXNode.ECorner.LeftBottom;
      else
         a.ParentCorner = UXNode.ECorner.RightTop;
      a.BgrdColor = POPUPMENU_Bgrd; a.BorderColor = POPUPMENU_BorderC;
      a.Border = POPUPMENU_BorderW; a.CornerRadius = POPUPMENU_Radius;
      a.Padding = POPUPMENU_Padding; a.ChildGap = POPUPMENU_ChildGap;
      return UXFrame.IsHovered (a.Id, 10);
   }

   public static void SEPARATOR () { }

   public static void TEXT (string text, Color4 color)
      => TEXT (text, color, 0);

   public static void TEXT (string text, Color4 color, int fontid) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Text = text; a.TextColor = color; a.FontId = (short)fontid;
      UXFrame.EndNode ();
   }

   public static void TIP (string text) { }
   public static void ICON (string s) { }
   public static bool DISABLED => false;

   public static void END () => UXFrame.EndNode ();
}
