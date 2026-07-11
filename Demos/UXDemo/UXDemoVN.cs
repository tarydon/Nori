using System.Reactive.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;
using static SizeS;
using static Elements;
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
         TypeFace tf2 = new ("C:/Windows/Fonts/webdings.ttf", (int)(10.5 * Lux.DPIScale + 0.5));
         UXFrame.TypeFaces = [tf1, tf2];
      }

      for (int i = 0; i < 2; i++) {
         UXFrame.BeginLayout (Lux.PanelSize);
         UXFrame.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

         UXFrame.BeginNode ();
         ref UXNode a = ref UXFrame.N;
         a.Width = Grow (); a.Height = Grow (); a.Orientation = EOrientation.TopToBottom;
         a.Padding = new MarginS (700, 0, 0, 0);
         a.Tag = "Root";

         // NewDemo ();
         UXCompile.Render ();

         UXFrame.EndNode ();
         UXFrame.EndLayout ();
         UXFrame.Render (i == 1);
      }
   }
   bool mFirst = true;

   static void DoFileNew () => Lib.Trace ("FILE.NEW");
   static void DoFileOpen (string? file = null) { }
   static void DoFileSave () { }
   static void DoExit () => Program.Window?.ShouldClose = true;
   static void DoCut () { }
   static void DoCopy () { }
   static void DoPaste () { }
   static void DoHelpAbout () { }
   static void DoExport (string fmt) { }
   static object? CurrentPart;
   static bool CurrentPartIs3D = false;
   static List<string> MRUList = ["c:/etc/test.fx", "c:/demos/pointer/output.dxf"];

   // Testing --------------------------------------------------------------------
   static void NewDemo () {
      if (TOPMENU ()) {
         if (MENUITEM_P ("File")) {
            POPUPMENU ();
            if (MENU ("New", "Ctrl N")) DoFileNew (); END ();
            if (MENU ("Open", "Ctrl O")) DoFileOpen (); END ();
            if (MRUList.Count > 0) {
               if (MENUITEM_P ("Open Recent")) {
                  POPUPMENU ();
                  for (int i = 0; i < Math.Min (MRUList.Count, 9); i++) {
                     var file = MRUList[i];
                     if (MENU ($"{file}", $"Ctrl {i + 1}")) DoFileOpen (file); END ();
                  }
                  END ();
               }
               END ();
            }
            if (MENU ("Save", "Ctrl S", CurrentPart is null)) DoFileSave (); END ();
            if (MENUITEM_P ("Export")) {
               POPUPMENU ();
               if (CurrentPartIs3D) {
                  if (MENU ("To IGES")) DoExport ("IGES"); END ();
                  if (MENU ("To STEP")) DoExport ("STEP"); END ();
               } else {
                  if (MENU ("To DXF")) DoExport ("DXF"); END ();
                  if (MENU ("To GEO")) DoExport ("GEO"); END ();
               }
               END ();
            }
            END ();
            SEPARATOR ();
            if (MENU ("Exit", "Ctrl X")) DoExit (); END ();
            END ();
         }
         END ();
         if (MENUITEM_P ("Edit")) {
            POPUPMENU ();
            if (MENU ("Cut", "Ctrl X")) DoCut (); END ();
            if (MENU ("Copy", "Ctrl C")) DoCopy (); END ();
            if (MENU ("Paste", "Ctrl V")) DoPaste (); END ();
            END ();
         }
         END ();
         if (MENUITEM_P ("Help")) {
            POPUPMENU ();
            if (MENU ("About...")) DoHelpAbout (); END ();
            END ();
         }
         END ();
      }
      END ();

      FILLER ().BgrdColor = new Color4 (0xC0C0C0);
   }

   // State information ----------------------------------------------------------
   Vec2S mPos;
   int mWheel;
   bool mPressed;
}
