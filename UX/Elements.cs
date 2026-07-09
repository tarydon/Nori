namespace Nori.UX;
using static SizeS;
using static UXNode.EOrientation;
using static UXNode.EChildAlignY;
using static UXTheme;

public static class Elements {
   public static void BeginTopMenu () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.TopMenu;
      a.BgrdColor = MENUBAR_Bgrd; a.Padding = MENUBAR_Padding; a.Width = Grow ();
   }

   public static bool BeginMenuItem (string text, string? shortcut = null, string? icon = null, string? disable = null) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.BgrdColor = a.IsHovered ? MENUITEM_Bgrd_H : MENUITEM_Bgrd;
      a.Padding = MENUITEM_Padding; a.CornerRadius = MENUITEM_Radius; 
      if (a.GetParent ().Kind != UXNode.EKind.TopMenu) a.Width = Grow ();
      a.ChildAlignY = Middle; 
      Text (text, disable == null ? MENUITEM_Text : MENUITEM_Text_D);
      if (shortcut != null) {
         Filler (60);
         Text (shortcut, MENUITEM_Shortcut, shortcut.Length == 1 ? 1 : 0);
      } 
      return a.IsHovered;
   }

   public static bool BeginPopupMenu () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Floating = true; a.Orientation = TopToBottom;
      a.ElemCorner = UXNode.ECorner.LeftTop;
      if (a.GetGrandParent ().Kind == UXNode.EKind.TopMenu) 
         a.ParentCorner = UXNode.ECorner.LeftBottom;
      else
         a.ParentCorner = UXNode.ECorner.RightTop;
      a.BgrdColor = POPUPMENU_Bgrd; a.BorderColor = POPUPMENU_BorderC;
      a.Border = POPUPMENU_BorderW; a.CornerRadius = POPUPMENU_Radius;
      a.Padding = POPUPMENU_Padding; a.ChildGap = POPUPMENU_ChildGap;
      return UXFrame.IsHovered (a.Id, 5);
   }

   public static void End () {
      UXFrame.EndNode ();
   }

   public static void Text (string text, Color4 color)
      => Text (text, color, 0);

   public static void Text (string text, Color4 color, int fontid) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Text = text; a.TextColor = color; a.FontId = (short)fontid;
      UXFrame.EndNode ();
   }

   public static void MenuItem (string text, string? shortcut = null, string? icon = null, string? disable = null) {
      BeginMenuItem (text, shortcut, icon, disable);
      End ();
   }

   public static void Separator () { }

   public static ref UXNode Filler (int min = 0) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Width = Grow (min); a.Height = Grow ();
      UXFrame.EndNode ();
      return ref a;
   }
}
