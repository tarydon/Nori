// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DemoVN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using Nori;
namespace UXDemo;
using static UXApi;
using static Size;

class DemoVN : VNode {
   public DemoVN () {
      Streaming = true;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);

      TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(30 * Lux.DPIScale + 0.5));
      TypeFace tf2 = new ("C:/Windows/Fonts/seguisym.ttf", (int)(12 * Lux.DPIScale + 0.5));
      UXSystem.Typefaces = [tf1, tf2];
   }

   void OnMouseMove (Vec2S pos) { mPos = pos; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      ref var root = ref UXSystem.BeginLayout (Lux.PanelSize);
      root.SetPadding (20); root.Data = "Root";

      ref var node = ref PANEL (Fit (100), Fit (100), true, Color4.DarkGreen);      
      node.Data = "DarkGreen"; node.SetPadding (20); node.ChildGap = 20;
      node.Y.ChildAlign = EAlign.Middle; 

      node = ref RECT (450, 350, Color4.Green); node.Data = "Green"; END ();
      node = ref PANEL (Fit (), Fit (), true, Color4.Yellow); node.Data = "Yellow";
      node.SetPadding (5); node.X.ChildAlign = EAlign.Middle;
      TEXT ("Hello, World!", 0, Color4.Black); END ();
      END ();
      node = ref RECT (Grow (250, 500), Fit (100), Color4.Cyan); node.Data = "Cyan"; END ();
      node = ref RECT (300, 200, Color4.DarkBlue); node.Data = "DarkBlue"; END ();

      END ();

      UXSystem.EndLayout ();
      UXSystem.Render (true);
   }

   // Private data -------------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
