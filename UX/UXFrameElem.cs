// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXFrameElem.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.UX;

using System.Transactions;
using static SizeS;
using static UXNode.EOrientation;
using static UXTheme;

public static partial class UXApi {
   public static bool BUTTON (string title, bool disable = false) {
      ref UXNode a = ref UXFrame.BeginNode ();
      // Set up bgrd color (different when the menu is hovered)
      a.BgrdColor = a.IsPressed ? BUTTON_Bgrd_P : (a.IsHovered ? BUTTON_Bgrd_H : BUTTON_Bgrd);
      a.Padding = BUTTON_Padding; a.CornerRadius = BUTTON_Radius; 
      a.ChildAlignX = UXNode.EChildAlignX.Center; a.ChildAlignY = UXNode.EChildAlignY.Middle;
      a.Disabled = disable;
      TEXT (title, BUTTON_Text);
      if (disable) return false;
      return a.IsReleased;
   }
   
   public static bool CHECKBOX (string title, ref bool val) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.ChildGap = 15; a.Padding = new MarginS (2, 0, 0, 0);
      a.ChildAlignY = UXNode.EChildAlignY.Middle;
      Color4 color = a.IsHovered ? DIALOG_TextC_H : DIALOG_TextC;
      TEXT (val ? "\u2611" : "\u2610", a.IsPressed ? CHECKBOX_Bgrd_P : color, 1);
      TEXT (title, color);
      if (a.IsReleased) { val = !val; return true; }
      return false;
   }

   public static bool RADIOBUTTON (string title, ref bool val) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.ChildGap = 15; a.Padding = new MarginS (2, 0, 0, 0);
      a.ChildAlignY = UXNode.EChildAlignY.Middle;
      Color4 color = a.IsHovered ? DIALOG_TextC_H : DIALOG_TextC;
      TEXT (val ? "\u25CF" : "\u25CB", a.IsPressed ? CHECKBOX_Bgrd_P : color, 1);
      TEXT (title, color);
      return a.IsReleased;
   }

   public static bool SLIDER (ref double val, double min, double max, double step = 0) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.Slider; a.ChildAlignY = UXNode.EChildAlignY.Middle;
      a.BgrdColor = a.IsHovered ? SLIDER_Bgrd_H : SLIDER_Bgrd;
      a.TextColor = a.IsPressed ? SLIDER_Fgrd_P : (a.IsHovered ? SLIDER_Fgrd_H : SLIDER_Fgrd); 
      a.Height = 36; a.Width = Grow ();
      a.DValue = val.GetLieOn (min, max).Clamp ();
      if (a.IsPressed) {
         var rect = UXFrame.GetRect (a.Id);
         double xpos = Hub.Mouse.Pos.X, margin = a.Height.Max / 8;
         double lie = xpos.GetLieOn (rect.Left + margin, rect.Right - margin);
         val = lie.Along (min, max).Clamp (min, max);
         if (step != 0) val = step * Math.Round (val / step);
      }
      return a.IsPressed;
   }

   public static bool DIALOG (string title) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.Dialog; a.Floating = true;
      a.ElemCorner = UXNode.ECorner.Center; a.ParentCorner = UXNode.ECorner.Center;
      a.BgrdColor = DIALOG_Bgrd; a.Padding = 0;
      a.Border = DIALOG_BorderW; a.BorderColor = DIALOG_BorderC; a.CornerRadius = DIALOG_Radius;
      a.Orientation = TopToBottom;
      a.Width = Fit (300); a.Height = Fit (200);

      // Add the title
      ref UXNode b = ref UXFrame.BeginNode ();
      b.Width = Grow (); b.BgrdColor = DIALOG_TitleColor; b.ChildAlignY = UXNode.EChildAlignY.Middle;
      b.CornerRadius = DIALOG_Radius;
      b.Padding = DIALOG_TitlePadding;
      TEXT (title, DIALOG_TextC, 0);
      UXFrame.EndNode ();

      // Add the content area
      ref UXNode c = ref UXFrame.BeginNode ();
      c.Width = Grow (); c.Height = Grow (); c.Padding = DIALOG_Padding; 
      c.ChildGap = DIALOG_Padding.Top;
      c.Orientation = TopToBottom;

      return true;
   }

   public static bool TOPMENU () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.TopMenu;
      a.BgrdColor = MENUBAR_Bgrd; a.Padding = MENUBAR_Padding;
      a.Width = Grow ();
      return true;
   }

   public static ref UXNode SCENEHOLDER (int slot) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.SceneHolder; a.BgrdColor = Color4.Red;
      a.Tag = slot.ToString (); a.Width = Grow (); a.Height = Grow ();
      UXFrame.EndNode ();
      return ref a;
   }

   public static ref UXNode FILLER (SizeS width, SizeS height) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Width = width; a.Height = height;
      UXFrame.EndNode ();
      return ref a;
   }

   public static ref UXNode FILLER (int min = 0) 
      => ref FILLER (Grow (min), Grow ());

   public static bool MENU (string text, bool disable, bool hasChildren)
      => MENU (text, null, disable, hasChildren);

   public static bool MENU (string text, string? shortcut = null, bool disable = false, bool hasChildren = false) {
      ref UXNode a = ref UXFrame.BeginNode ();
      // Set up bgrd color (different when the menu is hovered)
      a.BgrdColor = a.IsHovered || a.AnyPopupsOpen ? MENUITEM_Bgrd_H : MENUITEM_Bgrd;
      // Set up padding, corner radius, and if this a POPUP-MENU item (as opposed to a top level
      // MENU-BAR item), set it to Grow()
      a.Padding = MENUITEM_Padding; a.CornerRadius = MENUITEM_Radius;
      a.ChildAlignY = UXNode.EChildAlignY.Middle;
      if (a.GetParent ().Kind != UXNode.EKind.TopMenu) {
         a.Width = Grow ();
         if (hasChildren) shortcut = "\u25B8";
      }
      TEXT (text, (a.Disabled = disable) ? MENUITEM_Text_D : MENUITEM_Text);
      if (shortcut != null) {
         FILLER (80);
         TEXT (shortcut, MENUITEM_Shortcut, shortcut.Length == 1 ? 1 : 0);
      }
      if (a.Disabled) return false;
      if (hasChildren)
         return a.IsHovered || a.AnyPopupsOpen;
      else
         return a.IsReleased;
   }

   public static ref UXNode NODE () {
      return ref UXFrame.BeginNode ();
   }
   public static bool PANEL () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Orientation = TopToBottom;
      return true;
   }

   public static bool LABEL (object e) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Text = e.ToString ()!; a.TextColor = DIALOG_TextC; a.FontId = 0;
      return true;
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

   public static void SEPARATOR () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.Separator; a.Width = Grow (); a.Padding = new (0, 6);
      ref UXNode b = ref UXFrame.BeginNode ();
      b.BgrdColor = POPUPMENU_BorderC; b.Width = Grow (); b.Height = 2;
      UXFrame.EndNode ();
      UXFrame.EndNode ();
   }

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
   public static void WIDTH (string s) => UXFrame.N.Width = Grow ();

   public static void CHILDGAP (int n) => UXFrame.N.ChildGap = (short)n;

   public static void END () => UXFrame.EndNode ();

   public static void HORIZONTAL () => UXFrame.N.Orientation = LeftToRight;
   public static void VERTICAL () => UXFrame.N.Orientation = TopToBottom;
   public static void HGROW () => UXFrame.N.Width = Grow ();
   public static void VGROW () => UXFrame.N.Height = Grow ();
   public static void BGRD (string s) => UXFrame.N.BgrdColor = Color4.Parse (s);

   public static void END (int n) {
      for (int i = 0; i < n; i++) UXFrame.EndNode ();
   }
}
