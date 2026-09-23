// ────── ╔╗
// ╔═╦╦═╦╦╬╣ RootVN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

using System.IO;
using static UXApi;
using static UXNode.Size;

/// <summary>Represents the root VNode of the UX system</summary>
/// This VNode takes over the entire window and is responsible for rendering the menus, status bar,
/// toolbars etc. In the 'content' area, it will typically house a SCENEHOLDER that will in turn 
/// contain a separate Lux.Scene with the actual 
public class UXRootVN : VNode {
   public UXRootVN (string uiFolder, IEnumerable<TypeFace> faces) {
      Streaming = true; UXCel.Root = uiFolder;
      Hub.Mouse.Moves.Subscribe (OnMouseMove);
      Hub.Mouse.Wheel.Subscribe (OnMouseWheel);
      Hub.Mouse.Clicks.Where (a => a.Button == EMouseButton.Left).Subscribe (OnMouseClick);
      UXEngine.Typefaces = [.. faces];
      UXEngine.Init ();
      UXCel.Mount ("Root.in");
   }

   // Properties ---------------------------------------------------------------
   public static string UIFolder { get; private set; } = "";

   // Implementation -----------------------------------------------------------
   void OnMouseMove (Vec2S pos) { mPos = pos; Redraw (); }
   void OnMouseWheel (MouseWheelInfo info) { mWheel += info.Delta; Redraw (); }
   void OnMouseClick (MouseClickInfo info) { mPressed = info.IsPress; Redraw (); }

   public override void Draw () {
      mUID = 1;
      ushort nClipRect = Lux.NClipRect;
      ref var node = ref UXEngine.BeginLayout (Lux.PanelSize);
      UXEngine.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

      mActive.Clear (); mActive.AddRange (UXCel.Active);
      mActive.ForEach (a => a.Render ());

      UXEngine.EndLayout ();
      UXEngine.Render ();
      Lux.NClipRect = nClipRect;
   }
   List<UXCel> mActive = [];

   public void Draw1 () {
      mUID = 1;
      ushort nClipRect = Lux.NClipRect;

      /*
      ref var node = ref UXEngine.BeginLayout (Lux.PanelSize);
      node.SetPadding (20); node.Tag = "Root"; node.IsHorizontal = true; node.ChildGap = 20;
      UXEngine.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

      node = ref PANEL (mUID, Fit (), Fit (), true, Color4.Yellow);
      node.Tag = "Yellow"; node.ChildGap = 5; node.SetPadding (5);

      node = ref PANEL (ref mUID, 400, 300, true, Color4.DarkGreen);
      node.Tag = "DarkGreen";
      END ();  // "DarkGreen"

      node = ref PANEL (ref mUID, 300, 200, false, Color4.DarkBlue);
      node.Tag = "DarkBlue"; node.ChildGap = 5; node.SetPadding (5);

      node = ref PANEL (ref mUID, 100, 50, false, Color4.Red); END ();
      node = ref PANEL (ref mUID, 100, 50, false, Color4.Red); END ();

      END ();

      END (); // "Yellow"
      */

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

#region class UXCel --------------------------------------------------------------------------------
/// <summary>This represents a 'layer' of UI that can be independently loaded/unloaded</summary>
/// For example, the main window of the UI will be a Cel containing typically the
/// main menu, toolbars, status bar etc, and also housing a large 'content area'
/// housing a SCENEHOLDER with the main editor content of the application. Subsequently,
/// we may display additional cels for dialogs, glob editors, tooltips etc. Those will
/// be short lived and will cleanup their nodes when they exit
class UXCel {
   // Properties ---------------------------------------------------------------
   /// <summary>List of all the active cels that must be rendered</summary>
   public static IReadOnlyList<UXCel> Active => sActive;
   static List<UXCel> sActive = [];

   /// <summary>Folder from which all the .in files are loaded</summary>
   public static string Root = "";

   // Methods ------------------------------------------------------------------
   public void Render () {
      if (mRenderFunc == null) {
         // First, use the generator to create a CS file from the IN file
         UXGenerator ig = new (mFile);
         string s;
         try {
            s = ig.Generate (false);
         } catch (Exception e) {
            Lib.Trace (e);
            mRenderFunc = (() => { });
            return;
         }
         File.WriteAllText ($"c:/etc/{Path.GetFileNameWithoutExtension (mFile)}.cs", s);  // REMOVETHIS

         // Then, use the compiler to compile that to a function
         UXCompiler ic = new (s);
         mRenderFunc = ic.Compile () ?? (() => { });
         foreach (var err in ic.Diagnostics) Lib.Trace (err);
      }
      mRenderFunc ();
   }
   Action? mRenderFunc;

   /// <summary>Mount a cel (makes it active for rendering)</summary>
   public static UXCel Mount (string file) {
      if (!sDict.TryGetValue (file, out var cel)) {
         sDict.Add (file, cel = new UXCel (file));
      }
      if (!cel.mMounted) { sActive.Add (cel); cel.mMounted = true; }
      return cel;
   }
   static Dictionary<string, UXCel> sDict = new (StringComparer.OrdinalIgnoreCase);

   /// <summary>Unmounts a cel (deactivates it from rendering, releases resources)</summary>
   public static void Unmount (UXCel cel) {
      if (cel.mMounted) {
         cel.Release ();
         sActive.Remove (cel); cel.mMounted = false; 
      }
   }

   // Implementation -----------------------------------------------------------
   UXCel (string file) => mFile = Path.Combine (Root, file);

   // Releases the resources used by this cel
   public void Release () {
      // TODO
   }

   // Private data -------------------------------------------------------------
   readonly string mFile;  // Source file from which this is loaded
   bool mMounted;          // Is this mounted?
}
#endregion
