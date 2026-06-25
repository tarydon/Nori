using System.Diagnostics;
using System.Reactive.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;

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

         UXFrame.Begin ();
         ref UXNode a = ref UXFrame.N;
         a.SizeMode = ESizeMode.Grow;
         a.Padding = new MarginS (24);
         a.ChildGap = 12;

         Border (200, 80);
         Button (210, 90);

         UXFrame.End ();
         UXFrame.EndLayout ();
      }
      UXFrame.Render ();
   }
   bool mFirst = true;

   static void Border (int cx, int cy) {
      UXFrame.Begin ();
      ref UXNode a = ref UXFrame.N;
      a.SizeMode = ESizeMode.Fixed;
      a.Width = new (cx); a.Height = new (cy); a.BgrdColor = Color4.Yellow;
      UXFrame.End ();
   }

   static void Button (int cx, int cy) {
      UXFrame.Begin ();
      ref UXNode a = ref UXFrame.N;
      a.SizeMode = ESizeMode.Fit;
      a.BgrdColor = Color4.White;
      a.Padding = new (3);

      UXFrame.Begin ();
      ref UXNode b = ref UXFrame.N;
      b.Width = new (cx); b.Height = new (cx);
      b.SizeMode = ESizeMode.Grow;
      b.BgrdColor = Color4.Red;
      UXFrame.End ();
      
      UXFrame.End ();
   }

   // State information --------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}

