using System.Reactive.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;
using static SizeS;
using static UXApi;
using static UXFrame;

class UXDemoVN : VNode {
   public UXDemoVN () {
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
         TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
         TypeFace tf2 = new ("C:/Windows/Fonts/seguisym.ttf", (int)(12 * Lux.DPIScale + 0.5));
         TypeFaces = [tf1, tf2];

         var dwg = DXFReader.Load ("N:/TData/IO/DXF/AllEnt.dxf");
         var gvn = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg)]);
         Scene scene = new Scene2 { BgrdColor = Color4.Gray (192), Bound = dwg.Bound.InflatedF (1.1), Root = gvn };
         Scenes = [scene];

         UXLayout.Root = "N:/Demos/UXDemo/Inlay";
         UXLayout.Add (new UXLayout ("root.in"));
         // UXLayout.Add (new UXLayout ("dialog.in"));
      }

      for (int i = 0; i < 2; i++) {
         BeginLayout (Lux.PanelSize);
         SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

         BeginNode ();
         ref UXNode a = ref N;
         a.Width = Grow (); a.Height = Grow (); a.Orientation = EOrientation.TopToBottom;
         a.Padding = new MarginS (700, 0, 0, 0);
         a.Tag = "Root";

         var set = UXLayout.All.ToList ();
         set.ForEach (a => a.Render ());
         // SCENEHOLDER (0);
         FILLER ().BgrdColor = Color4.Gray (108);
         FILLER (Grow (), 100).BgrdColor = UXTheme.MENUBAR_Bgrd;

         EndNode ();
         EndLayout ();
         Render (i == 1);
      }
   }
   bool mFirst = true;

   // State information ----------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
