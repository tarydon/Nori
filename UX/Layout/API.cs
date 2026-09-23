// ────── ╔╗
// ╔═╦╦═╦╦╬╣ API.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static UXNode;
using static UXNode.Size;
using static UXTheme;

public static class UXApi {
   public static bool DISABLED => false;

   public static void END () {
      UXEngine.EndNode ();
   }

   public static void END (int a) {
      for (int i = 0; i < a; i++) END ();
   }

   public static ref UXNode FILLER (Size width, Size height) {
      ref UXNode a = ref UXEngine.BeginNode (EKind.Filler);
      a.X.Set (width); a.Y.Set (height);
      UXEngine.EndNode ();
      return ref a;
   }

   public static ref UXNode FILLER (int min = 0)
      => ref FILLER (Grow (min), Grow ());

   public static void ICON (string s) { }

   public static bool MENU (string key, string text, bool disable, bool hasChildren)
      => MENU (key, text, null, disable, hasChildren);

   public static bool MENU (string key, string text, string? shortcut = null, bool disable = false, bool hasChildren = false) {
      ref UXNode a = ref UXEngine.BeginNode (EKind.Menu, key);
      // Set up bgrd color (different when the menu is hovered)
      a.BgrdColor = a.IsMouseOver || a.AnyPopupsOpen ? MENUITEM_Bgrd_H : MENUITEM_Bgrd;
      // Set up padding, corner radius, and if this a POPUP-MENU item (as opposed to a top level
      // MENU-BAR item), set it to Grow()
      ref Axis x = ref a.X, y = ref a.Y;
      a.SetPadding (MENUITEM_Padding.L, MENUITEM_Padding.T, MENUITEM_Padding.R, MENUITEM_Padding.B);
      a.CornerRadius = MENUITEM_Radius; a.Y.ChildAlign = EAlign.Middle;
      if (a.GetParent ().Kind != EKind.TopMenu) {
         a.X.Set (Grow ());
         if (hasChildren) shortcut = "\u25B8";
      }
      TEXT (text, (a.Disabled = disable) ? MENUITEM_Text_D : MENUITEM_Text);
      if (shortcut != null) {
         FILLER (80);
         TEXT (shortcut, MENUITEM_Shortcut, shortcut.Length == 1 ? 1 : 0);
      }
      if (a.Disabled) return false;
      if (hasChildren)
         return a.IsHovered (MENUITEM_OpenDelay) || a.AnyPopupsOpen;
      else
         return a.IsReleased;
   }

   public static ref UXNode PANEL (Size width, Size height, bool horizontal, Color4 bgrd, string? key = null) {
      ref UXNode a = ref UXEngine.BeginNode (EKind.Panel, width, height, key);
      a.BgrdColor = bgrd; a.IsHorizontal = horizontal;
      return ref a;
   }

   public static bool POPUPMENU (string key) {
      ref UXNode a = ref UXEngine.BeginNode (EKind.Popup, key);
      a.ElemCorner = ECorner.TopLeft;
      if (a.GetGrandparent ().Kind == EKind.TopMenu)
         a.ParentCorner = ECorner.BotLeft;
      else
         a.ParentCorner = ECorner.TopRight;
      a.BgrdColor = POPUPMENU_Bgrd; a.BorderColor = POPUPMENU_BorderC;
      a.BorderWidth = POPUPMENU_BorderW; a.CornerRadius = POPUPMENU_Radius;
      a.SetPadding (POPUPMENU_Padding); a.ChildGap = POPUPMENU_ChildGap;
      return a.IsHovered (10);
   }

   public static void SEPARATOR () {
      ref UXNode a = ref PANEL (Grow (), 2, true, POPUPMENU_BorderC);
      a.FgrdColor = POPUPMENU_Bgrd;
      UXEngine.EndNode ();
   }

   public static void TEXT (string text, Color4 color)
      => TEXT (text, color, 0);

   public static void TEXT (string text, Color4 color, int fontid) {
      ref UXNode a = ref UXEngine.BeginNode (EKind.Text);
      a.Text = text; a.FgrdColor = color; a.FontId = (short)fontid;
      UXEngine.EndNode ();
   }

   public static void TIP (string s) { }

   public static bool TOPMENU () {
      ref UXNode a = ref UXEngine.BeginNode (EKind.TopMenu);
      a.BgrdColor = MENUBAR_Bgrd; a.SetPadding (MENUBAR_Padding.H, MENUBAR_Padding.V);
      a.X.Set (Grow ());
      return true;
   }
}
