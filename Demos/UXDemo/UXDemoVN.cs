using System.Diagnostics;
using System.Reactive.Linq;
using System.Xml.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;
using static SizeS;
using static Elements;

class UXDemoVN : VNode {
   public UXDemoVN () {
      Streaming = true;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);
   }

   void OnMouseMove (Vec2S vec) { mPos = vec; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); Lib.Trace (info); }

   public override void Draw () {
      if (mFirst) {
         // TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
         TypeFace tf1 = new TypeFace ("C:/Etc/NotoSans-Regular.ttf", (int)(9.5 * Lux.DPIScale + 0.5));
         TypeFace tf2 = new TypeFace ("C:/Windows/Fonts/webdings.ttf", (int)(10.5 * Lux.DPIScale + 0.5));
         UXFrame.TypeFaces = [tf1, tf2];
      }
      UXFrame.BeginLayout (Lux.PanelSize);
      UXFrame.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

      UXFrame.BeginNode ();
      ref UXNode a = ref UXFrame.N; 
      a.Width = Grow (); a.Height = Grow (); a.Orientation = EOrientation.TopToBottom;
      a.Padding = new MarginS (1000, 0, 0, 0);
      a.Tag = "Root";

      FullDemo ();
      // Demo1 ();

      UXFrame.EndNode ();
      UXFrame.EndLayout ();
      if (mFirst) UXFrame.DumpAll ();
      UXFrame.Render ();
      mFirst = false;
   }
   bool mFirst = true;

   // Testing --------------------------------------------------------------------
   static void FullDemo () {
      BeginMenuBar ();
      if (BeginMenu ("File") || mPopupsOpen.SafeGet (1, false) || mPopupsOpen.SafeGet (2, false)) {
         mPopupsOpen[1] = BeginPopupMenu ();
         MenuItem ("New", "Ctrl N");
         MenuItem ("Open...", "Ctrl S");
         if (BeginMenuItem ("Open Recent", "\u0034") || mPopupsOpen.SafeGet (2, false)) {
            mPopupsOpen[2] = BeginPopupMenu ();
            MenuItem ("1. c:/etc/test.fx");
            MenuItem ("2. c:/nori/demos/flange.igs");
            MenuItem ("3. c:/documents/settings.curl");
            End ();
         }
         End ();
         MenuItem ("Save");
         Separator ();
         MenuItem ("Export", "Ctrl Shift E");
         MenuItem ("Import");
         Separator ();
         MenuItem ("Exit", "Alt F4");
         End ();
      }
      End ();
      BeginMenu ("Edit"); End ();
      BeginMenu ("Help"); End ();
      End ();

      Filler ().BgrdColor = new Color4 (0xC0C0C0);
   }
   static Dictionary<int, bool> mPopupsOpen = [];

   static void DropDownMenu () {
      //// Create the top-down menu item
      //ref UXNode a = ref UXFrame.BeginNode ();
      //a.Orientation = EOrientation.TopToBottom;
      //a.BgrdColor = new Color4 (0xFFA674A4);
      //a.Padding = 16; a.ChildGap = 16;
      //a.Tag = "Menu";

      //for (int i = 0; i < mItems.Length; i++) {
      //   // Create the menu item background rectangle
      //   ref UXNode b = ref UXFrame.BeginNode ();
      //   b.Padding = new MarginS (16, 16, 0, 0);
      //   b.Width = Grow (); b.ChildAlignY = EChildAlignY.Middle;
      //   b.BgrdColor = new Color4 (0xAABD95BC);
      //   b.Tag = "MenuItem";
      //   Text (mItems[i], 0, "Label");
      //   ref UXNode c = ref UXFrame.BeginNode ();
      //   c.Width = Grow (80);
      //   UXFrame.EndNode ();
      //   Text (mIcon[i], 1, "Icon");
      //   UXFrame.EndNode (); 
      //}

      //UXFrame.EndNode ();
   }
   static string[] mItems = ["New", "Open...", "Save", "Export to DXF", "Exit"];
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
