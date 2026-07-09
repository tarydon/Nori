using System.Reactive.Linq;
using Nori;
using Nori.UX;
namespace UXDemo;
using static UXNode;
using static SizeS;
using static Elements;
using static UXApi;

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
      for (int i = 0; i < 2; i++) {
         if (mFirst) {
            mFirst = false;
            TypeFace tf1 = new TypeFace ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
            TypeFace tf2 = new ("C:/Windows/Fonts/webdings.ttf", (int)(10.5 * Lux.DPIScale + 0.5));
            UXFrame.TypeFaces = [tf1, tf2];
         }
         UXFrame.BeginLayout (Lux.PanelSize);
         UXFrame.SetMouseState (mPos, mWheel, mPressed); mWheel = 0;

         UXFrame.BeginNode ();
         ref UXNode a = ref UXFrame.N;
         a.Width = Grow (); a.Height = Grow (); a.Orientation = EOrientation.TopToBottom;
         a.Padding = new MarginS (700, 0, 0, 0);
         a.Tag = "Root";

         // FullDemo ();
         NewDemo ();
         // Demo1 ();

         UXFrame.EndNode ();

         UXFrame.EndLayout ();
         if (mFirst) UXFrame.DumpAll ();
         mFirst = false;
         UXFrame.Render (i == 1);
      }
   }
   bool mFirst = true;

   static void DoFileNew () { }
   static void DoFileOpen (string? file = null) { }
   static void DoFileSave () { }
   static void DoExit () { }
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
         if (MENUITEM ("File", "\u0033")) {
            POPUPMENU ();
            if (MENUITEM ("New", "Ctrl N")) DoFileNew (); END ();
            if (MENUITEM ("Open", "Ctrl O")) DoFileOpen (); END ();
            if (MRUList.Count > 0) {
               if (MENUITEM ("Open Recent", "\u0034")) {
                  POPUPMENU ();
                  for (int i = 0; i < Math.Min (MRUList.Count, 9); i++) {
                     var file = MRUList[i];
                     if (MENUITEM ($"{file}", $"Ctrl {i + 1}")) DoFileOpen (file); END ();
                  }
                  END ();
               }
               END ();
            }
            if (MENUITEM ("Save", "Ctrl S")) DoFileSave (); END ();
            if (CurrentPart is null) DISABLE ("No Active Part");
            if (MENUITEM ("Export", "\u0034")) {
               POPUPMENU ();
               if (CurrentPartIs3D) {
                  if (MENUITEM ("To IGES")) DoExport ("IGES"); END ();
                  if (MENUITEM ("To STEP")) DoExport ("STEP"); END ();
               } else {
                  if (MENUITEM ("To DXF")) DoExport ("DXF"); END ();
                  if (MENUITEM ("To GEO")) DoExport ("GEO"); END ();
               }
               END ();
            }
            END ();
            SEPARATOR ();
            if (MENUITEM ("Exit", "Ctrl X")) DoExit (); END ();
            END ();
         }
         END ();
         if (MENUITEM ("Edit", "\u0033")) {
            POPUPMENU ();
            if (MENUITEM ("Cut", "Ctrl X")) DoCut (); END ();
            if (MENUITEM ("Copy", "Ctrl C")) DoCopy (); END ();
            if (MENUITEM ("Paste", "Ctrl V")) DoPaste (); END ();
            END ();
         }
         END ();
         if (MENUITEM ("Help", "\u0033")) {
            POPUPMENU ();
            if (MENUITEM ("About...")) DoHelpAbout (); END ();
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
