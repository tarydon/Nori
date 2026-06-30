namespace Nori.UX;
using static SizeS;
using static UXNode.EOrientation;
using static UXNode.EChildAlignY;
using static UXNode.EChildAlignX;

static class Elements {
   public static void BeginMenuBar () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Kind = UXNode.EKind.TopMenu;
      a.BgrdColor = 0x181818; a.Width = Grow (); a.Padding = new (8, 8, 6, 6);
   } 

   public static bool BeginMenu (string text) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Padding = new (20, 20, 6, 6); a.CornerRadius = 3;
      a.BgrdColor = (uint)(a.IsHovered ? 0x363636 : 0x181818);
      a.ChildAlignX = Center; a.ChildAlignY = Middle;
      Text (text, 0xD9D9D9);
      return a.IsPressed;
   }

   public static void BeginPopupMenu () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Floating = true; a.ElemCorner = UXNode.ECorner.LeftTop;
      if (a.GetGrandParent (UXFrame.All).Kind == UXNode.EKind.TopMenu) 
         a.ParentCorner = UXNode.ECorner.LeftBottom;
      else
         a.ParentCorner = UXNode.ECorner.RightTop;
      a.BgrdColor = 0x405060; a.Padding = 10; a.ChildGap = 1; a.Floating = true;
      a.Orientation = TopToBottom;
   }

   public static void End () {
      UXFrame.EndNode ();
   }

   public static void Text (string text, Color4 color) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Text = text; a.TextColor = color;
      UXFrame.EndNode ();
   }

   public static void MenuItem (string text, string? shortcut = null) {
      BeginMenuItem (text, shortcut);
      End ();
   }

   public static bool BeginMenuItem (string text, string? shortcut = null) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Padding = new (20, 20, 6, 6); a.CornerRadius = 3; a.Width = Grow ();
      a.BgrdColor = (uint)(a.IsHovered ? 0x363636 : 0x181818);
      a.ChildAlignX = Center; a.ChildAlignY = Middle;
      Text (text, 0xD9D9D9);
      if (shortcut != null) {
         Filler (60);
         Text (shortcut, 0xD9D9D9);
      } else
         Filler ();
      return a.IsPressed;
   }

   public static void Separator () { }

   public static void MENU (params IEnumerable<string> items) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.BgrdColor = 0x181818; a.Border = 2; a.BorderColor = 0x363636; a.CornerRadius = 8;
      a.Orientation = TopToBottom;
      foreach (var item in items) {
         ref UXNode b = ref UXFrame.BeginNode ();
         b.TextColor = 0xD9D9D9; a.Padding = 8; a.ChildGap = 8; a.Width = Fit (200);
         b.Text = item;
         UXFrame.EndNode ();
      }
      UXFrame.EndNode ();
   }

   public static ref UXNode Filler (int min = 0) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Width = Grow (min); a.Height = Grow ();
      UXFrame.EndNode ();
      return ref a;
   }
}
