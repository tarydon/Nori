// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DemoVN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using Nori;
namespace UXDemo;
using static UXApi;

class DemoVN : VNode {
   public DemoVN () {
      Streaming = true;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);

      TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
      TypeFace tf2 = new ("C:/Windows/Fonts/seguisym.ttf", (int)(12 * Lux.DPIScale + 0.5));
      UXSystem.Typefaces = [tf1, tf2];
   }

   void OnMouseMove (Vec2S pos) { mPos = pos; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      bool _b1 = PANEL (600, 400, Color4.Red);
   }

   // Private data -------------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
