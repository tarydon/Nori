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

      TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
      TypeFace tf2 = new ("C:/Windows/Fonts/seguisym.ttf", (int)(12 * Lux.DPIScale + 0.5));
      UXSystem.Typefaces = [tf1, tf2];
   }

   void OnMouseMove (Vec2S pos) { mPos = pos; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      mUID = 0; 
      ref var root = ref UXSystem.BeginLayout (Lux.PanelSize);
      root.SetPadding (20); root.Data = "Root";
      UXSystem.SetMouseState (mPos, mWheel, mPressed); mWheel = 0; 

      ref var node = ref PANEL (NextUID, Fit (100, 1500), Fit (100, 700), true, Color4.DarkGreen);      
      node.Data = "DarkGreen"; node.SetPadding (20); node.ChildGap = 20;
      node.Y.ChildAlign = EAlign.Middle;  

      //node = ref RECT (NextUID, 450, 350, Color4.Green); node.Data = "Green"; END ();
      node = ref LISTBOX (NextUID, 450, 350, mFiles, 3); END ();
      node = ref PANEL (NextUID, Fit (200), Fit (), true, Color4.Yellow); node.Data = "Yellow";
      node.SetPadding (10, 10, 8, 10); node.X.ChildAlign = EAlign.Middle;
      VSCROLL (NextUID, new Color4 (216, 216, 0));
      MTEXT (NextUID, Lorem, 0, Color4.Black, Color4.Transparent); END ();
      END ();  // VSCroll
      END ();  // Yellow panel
      node = ref RECT (NextUID, Grow (250, 500), Fit (100), Color4.Cyan); node.Data = "Cyan";
      if (node.IsHovered (200)) {
         node = ref POPUP (NextUID, Fit (), Fit (), Color4.Gray (128), ECorner.Bottom, ECorner.TopLeft, new (0, 6));
         node.CornerRadius = 6;
         node.SetPadding (16, 8, 16, 8);
         TEXT (NextUID, "A Cyan Rectangle!", 0, Color4.White); END ();
         END ();
      } else { _ = NextUID; _ = NextUID; }
      END ();  // Cyan Rect
      node = ref BLOCK (NextUID, 300, 200, Color4.DarkBlue, Color4.Blue, Color4.Red); node.Data = "DarkBlue"; END ();

      END ();

      UXSystem.EndLayout ();
      UXSystem.Render ();
   }

   static uint NextUID => ++mUID;
   static uint mUID = 0;
   static string[] mFiles = [.. Directory.GetFiles ("N:/Demos/WPFDemo", "*.cs").Select (a => Path.GetFileName (a))];

   static string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Ut efficitur cursus consequat. Suspendisse at ultrices leo. Fusce vitae volutpat lacus, quis convallis lorem. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Aenean aliquam lectus non neque molestie, non dignissim turpis aliquet. Maecenas ac accumsan nisi. Sed dignissim lacinia quam nec tristique. Pellentesque a egestas augue. Phasellus porta, ex ac interdum maximus, elit turpis gravida elit, nec pulvinar nunc neque quis lorem. Nunc pretium ipsum sed malesuada volutpat. In mollis bibendum eros ac ultricies. Pellentesque pellentesque commodo dapibus. Sed feugiat fermentum ultrices. Fusce non purus ac mi mollis ullamcorper et at mauris.";

   // Private data -------------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
