// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXTheme.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace Nori.UX;

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
   public static int MENUITEM_OpenDelay = 150;

   public static Color4 POPUPMENU_Bgrd = 0x181818;
   public static Color4 POPUPMENU_BorderC = 0x444444;
   public static short POPUPMENU_BorderW = 2;
   public static short POPUPMENU_Radius = 3;
   public static MarginS POPUPMENU_Padding = 10;
   public static short POPUPMENU_ChildGap = 3;

   public static Color4 DIALOG_Bgrd = 0x3D3D3D;
   public static Color4 DIALOG_BorderC = 0x303030;
   public static short DIALOG_BorderW = 6;
   public static short DIALOG_Radius = 12;
   public static MarginS DIALOG_TitlePadding = new (15, 10);
   public static Color4 DIALOG_TitleColor = 0x505050;
   public static MarginS DIALOG_Padding = 20;

   public static Color4 BUTTON_Bgrd = 0x545454;
   public static Color4 BUTTON_Bgrd_H = 0x656565;  // Button when mouse hovers over
   public static Color4 BUTTON_Bgrd_P = 0x4772B3;  // Button during pressing
   public static MarginS BUTTON_Padding = new (14, 7);
   public static short BUTTON_Radius = 2;
   public static Color4 BUTTON_Text = 0xDDDDDD;

   public static int TOOLTIP_OpenDelay = 500;
}
