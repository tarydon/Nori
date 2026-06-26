using System.Diagnostics;
using System.Reactive.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;
using static SizeS;

class UXVNode : VNode {
   public UXVNode () {
      Streaming = true;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);
   }

   void OnMouseMove (Vec2S vec) { mPos = vec; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      if (mFirst) {
         mFirst = false;
         UXFrame.BeginLayout (Lux.PanelSize);
         UXFrame.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

         UXFrame.BeginNode ();
         ref UXNode a = ref UXFrame.N;
         a.Width = Grow (); a.Height = Grow ();
         a.Padding = 10;
         a.Tag = "Root";

         // DropDownMenu ();
         Demo1 ();

         UXFrame.EndNode ();
         UXFrame.EndLayout ();
         UXFrame.DumpAll ();
      }
      UXFrame.Render ();
   }
   bool mFirst = true;

   // Testing --------------------------------------------------------------------
   static void Demo1 () {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Tag = "Rect";
      a.Width = 1600; a.Height = 600;
      a.BgrdColor = new (0xFF0A6E89);
      a.Padding = 30; a.ChildGap = 30;

      ref UXNode b = ref UXFrame.BeginNode ();
      b.Width = 300; b.Height = 300; b.Tag = "Child1";
      b.BgrdColor = new (0xFFFB938F);
      UXFrame.EndNode ();   // b

      ref UXNode c = ref UXFrame.BeginNode ();
      c.Width = Grow (); c.Height = Grow (); c.Tag = "Child2";
      c.BgrdColor = new (0xFFFED84D);
      UXFrame.EndNode ();

      ref UXNode d = ref UXFrame.BeginNode ();
      d.Width = Grow (350); d.Height = 400; d.Tag = "Child3";
      d.BgrdColor = new (0xFF5ECBE4);
      UXFrame.EndNode ();  // d

      UXFrame.EndNode ();   // a
   }

   static void DropDownMenu () {
      // Create the top-down menu item
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Orientation = EOrientation.TopToBottom;
      a.BgrdColor = new Color4 (0xFFA674A4);
      a.Tag = "Menu";

      for (int i = 0; i < mItems.Length; i++) {
         // Create the menu item background rectangle
         ref UXNode b = ref UXFrame.BeginNode ();
         b.Width = Grow ();
         b.BgrdColor = new Color4 (0xAABD95BC);
         b.Tag = "MenuItem";
         Text (mItems[i], 1, "Label");
         Text (mIcon[i], 2, "Icon");
         UXFrame.EndNode (); 
      }

      UXFrame.EndNode ();
   }
   static string[] mItems = ["New", "Open...", "Save", "Export To DXF", "Exit"];
   static string[] mIcon = ["\u0021", "\u0056", "\u00B7", "\u00F5", "\u00AE"];

   static void Text (string text, int font, string? tag) {
      ref UXNode a = ref UXFrame.BeginNode ();
      a.Text = text;
      a.FontId = (short)font;
      a.Tag = tag;
      a.TextColor = Color4.White;
      UXFrame.EndNode ();
   }

   // State information ----------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
