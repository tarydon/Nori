// ────── ╔╗
// ╔═╦╦═╦╦╬╣ RootVN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static UXApi;
using static UXNode.Size;

/// <summary>Represents the root VNode of the UX system</summary>
/// This VNode takes over the entire window and is responsible for rendering the menus, status bar,
/// toolbars etc. In the 'content' area, it will typically house a SCENEHOLDER that will in turn 
/// contain a separate Lux.Scene with the actual 
public class UXRootVN : VNode {
   public UXRootVN (string uiFolder, IEnumerable<TypeFace> faces) {
      Streaming = true; UIFolder = uiFolder;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);
      UXEngine.Typefaces = [.. faces];
      UXEngine.Init ();
   }

   // Properties ---------------------------------------------------------------
   public static string UIFolder { get; private set; } = "";

   // Implementation -----------------------------------------------------------
   void OnMouseMove (Vec2S pos) { mPos = pos; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      mUID = 0;
      ushort nClipRect = Lux.NClipRect;
      ref var node = ref UXEngine.BeginLayout (Lux.PanelSize);
      node.SetPadding (20); node.Tag = "Root"; node.IsHorizontal = true; node.ChildGap = 20;
      UXEngine.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

      node = ref PANEL (++mUID, Fit (), Fit (), true, Color4.Yellow);
      node.Tag = "Yellow"; node.ChildGap = 5; node.SetPadding (5);

      node = ref PANEL (++mUID, 400, 300, true, Color4.DarkGreen);
      node.Tag = "DarkGreen";
      END ();  // "DarkGreen"

      node = ref PANEL (++mUID, 300, 200, false, Color4.DarkBlue);
      node.Tag = "DarkBlue"; node.ChildGap = 5; node.SetPadding (5);

      node = ref PANEL (++mUID, 100, 50, false, Color4.Red); END ();
      node = ref PANEL (++mUID, 100, 50, false, Color4.Red); END ();

      END ();

      END (); // "Yellow"

      //ref var node = ref PANEL (NextUID, Fit (100, 1500), Fit (100, 700), true, Color4.DarkGreen);
      //node.Data = "DarkGreen"; node.SetPadding (20); node.ChildGap = 20;
      //node.Y.ChildAlign = EAlign.Middle;

      //node = ref LISTBOX (NextUID, 450, 350, mFiles, 3); END ();
      //node = ref PANEL (NextUID, Fit (200), Fit (), true, Color4.Yellow); node.Data = "Yellow";
      //node.SetPadding (10, 10, 8, 10); node.X.ChildAlign = EAlign.Middle;
      //VSCROLL (NextUID, new Color4 (216, 216, 0));
      //MTEXT (NextUID, Lorem, 0, Color4.Black, Color4.Transparent); END ();
      //END ();  // VSCroll
      //END ();  // Yellow panel
      //node = ref RECT (NextUID, Grow (250, 500), Fit (100), Color4.Cyan); node.Data = "Cyan";
      //if (node.IsHovered (200)) {
      //   node = ref POPUP (NextUID, Fit (), Fit (), Color4.Gray (128), ECorner.Bottom, ECorner.TopLeft, new (0, 6));
      //   node.CornerRadius = 6;
      //   node.SetPadding (16, 8, 16, 8);
      //   TEXT (NextUID, "A Cyan Rectangle!", 0, Color4.White); END ();
      //   END ();
      //} else { _ = NextUID; _ = NextUID; }
      //END ();  // Cyan Rect
      //node = ref BLOCK (NextUID, 300, 200, Color4.DarkBlue, Color4.Blue, Color4.Red); node.Data = "DarkBlue"; END ();
      //END ();  // "DarkGreen"

      //node = ref CLISTBOX (NextUID, 200, Grow (), mBTools ??= new BToolList ());
      //node.SetPadding (5); node.ChildGap = 5;
      //END ();

      //node = ref CLISTBOX (NextUID, 300, Grow (), mModels ??= new ModelList ());
      //node.SetPadding (5); node.ChildGap = 5;
      //END ();

      UXEngine.EndLayout ();
      UXEngine.Render ();
      Lux.NClipRect = nClipRect;
   }
   uint mUID;

   // Private data -------------------------------------------------------------
   Vec2S mPos;       // Mouse position in pixels
   int mWheel;       // Mouse-wheel rotations since the last frame
   bool mPressed;    // Is the mouse button pressed?
}
