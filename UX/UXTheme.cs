using Nori;
using Nori.UX;
namespace UXDemo;

public static class UXTheme {
   public static Color4 MENUBAR_Bgrd = 0x181818;
   public static MarginS MENUBAR_Padding = new (8, 6);

   public static Color4 MENUITEM_Bgrd = 0x181818;
   public static Color4 MENUITEM_Bgrd_H = 0x363636;
   public static Color4 MENUITEM_Text = 0xDDDDDD;
   public static Color4 MENUITEM_Text_D = 0x777777;
   public static MarginS MENUITEM_Padding = new (20, 7, 20, 6);
   public static short MENUITEM_Radius = 3;
   public static Color4 MENUITEM_Shortcut = 0x888888;

   public static Color4 POPUPMENU_Bgrd = 0x181818;
   public static Color4 POPUPMENU_BorderC = 0x444444;
   public static short POPUPMENU_BorderW = 2;
   public static short POPUPMENU_Radius = 3;
   public static MarginS POPUPMENU_Padding = 10;
   public static short POPUPMENU_ChildGap = 3;
}