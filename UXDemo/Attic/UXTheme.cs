// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXTheme.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;

namespace UXDemo;

static class UXTheme {
   // Listbox 
   public static Color4 LISTBOX_Bgrd = new (0x1D1D1D);      // Listbox bgrd color
   public static Color4 LISTBOX_Bgrd_H = new (0x303030);    // Bgrd of hovered item
   public static Color4 LISTBOX_Bgrd_S = new (0x4772B3);    // Bgrd of selected item
   public static Color4 LISTBOX_Text = new (0xE6E6E6);      // Text color
   public static Color4 LISTBOX_Text_H = new (0xFFFFFF);    // Text color of hovered item
   public static Color4 LISTBOX_Text_S = new (0xFFFFFF);    // Text color of selected item

   public static int LISTBOX_Margin = 12;

   public static int SCROLLBAR_Margin = 4;
}
