using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Nori;
namespace GLFWDemo;

class Program {
   static void Main () {
      Lib.Init (); 
      GLFWHost.Init (OnReady); 
      var w = new Window (1024, 768, "Welcome to GLFW", Window.EFlags.Default | Window.EFlags.Maximized);
      w.Run (true);
   }

   static void OnReady () {
      new SceneManipulator ();
      TraceVN.TextColor = Color4.Yellow;
      TraceVN.HoldTime = 15;
      Lib.Tracer = TraceVN.Print;
      Lux.UIScene = new Scene2 ();
      Lib.Tessellate = FastTess2D.Process;
      Hub.Mouse.Clicks.Where (a => a.IsRightPress).Subscribe (OnMouseClick);
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Escape)).Subscribe (OnTagBad);
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Space)).Subscribe (OnTagGood);
   }

   static void OnTagBad (KeyInfo ki) {
      File.Move (CurrentFile, "W:\\NoriSample\\UNFOLD\\" + Path.GetFileName (CurrentFile));
      Lib.Trace ($"Shunted {CurrentFile}");
   }

   static void OnTagGood (KeyInfo ki) {
      File.Move (CurrentFile, "W:\\NoriSample\\GOOD\\" + Path.GetFileName (CurrentFile));
      Lib.Trace ($"Tagged Good {CurrentFile}");
   }

   static void OnMouseClick (MouseClickInfo mi) {
      switch (Phase) {
         case 0: Lux.UIScene = new SurfaceScene (); break;
         case 1: Lux.UIScene = new SheetMetalScene (); break;
         case 2: CurrentFile = NextFile; Lux.UIScene = new UnfoldedScene (); break;
      }
      Phase++;
      if (Phase == 3) { Files.RemoveAt (0); Phase = 0; }
   }

   static int Phase = 0; 
   static string CurrentFile = "";

   public static string NextFile => Files[0];

   public static List<string> Files = Directory.GetFiles ("W:\\NoriSample", "*.stp").ToList ();
}

class SurfaceScene : Scene3 {
   public SurfaceScene () {
      var model = STEPReader.Load (Program.NextFile);

      List<VNode> nodes = [new Model3VN (model), TraceVN.It];
      Bound = model.Bound;
      Root = new Model3VN (model);
   }
}

class SheetMetalScene : Scene3 {
   public SheetMetalScene () {
      var model = STEPReader.Load (Program.NextFile);
      var shmodel = new SheetMetalizer (model).Process ().Value;

      List<VNode> nodes = [new Model3VN (shmodel), TraceVN.It];
      Bound = shmodel.Bound;
      Root = new GroupVN (nodes);
   }
}

class UnfoldedScene : Scene2 {
   public UnfoldedScene () {
      Lib.Trace (Program.NextFile);
      var model = STEPReader.Load (Program.NextFile);
      var shmodel = new SheetMetalizer (model).Process ().Value;
      var dwg = new Unfolder (shmodel).Process ().Value;

      List<VNode> nodes = [new Dwg2VN (dwg), new DwgFillVN (dwg, ETess.Medium), TraceVN.It];
      Bound = dwg.Bound.InflatedF (1.05);
      Root = new GroupVN (nodes);
   }
}

class UnfoldScene : Scene2 {
   public UnfoldScene () {
      var model = STEPReader.Load ("W:/NoriSample/S00178.stp");


      var shmodel = new SheetMetalizer (model).Process ().Value;
      var dwg = new Unfolder (shmodel).Process ().Value;

      List<VNode> nodes = [new Dwg2VN (dwg), new DwgFillVN (dwg, ETess.Medium), TraceVN.It];
      Bound = dwg.Bound.InflatedF (1.05);
      Root = new GroupVN (nodes);
   }
}

class NewScene : Scene3 {
   public NewScene () {
      var model = STEPReader.Load ("N:/TData/STEP/S00178.stp");
      var shmodel = new SheetMetalizer (model).Process ().Value;

      List<VNode> nodes = [];
      var pose = new BendPose (shmodel);
      nodes.AddRange (pose.Nodes.Select (a => new BPoseNodeVN (a)));
      nodes.Add (TraceVN.It);

      // nodes.Clear ();
      Bound = pose.GetBound (1);
      BgrdColor = new Color4 (90, 100, 110);
      Root = new GroupVN (nodes);
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);

      string message = "Welcome to Nori.";
      var size = mFace.Measure (message, true);
      int dx = size.Width, dy = size.Height;
      Vec2S cen = new (dx / 2 + dy, dy / 2 + dy);
      var vn1 = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace, Lux.ZLevel) = (new (255, 224, 226, 228), mFace, 1),
         () => Lux.Text (message, new Vec2S (cen.X - dx / 2, cen.Y + dy / 2))
      );

      var vn2 = new SimpleVN (
         () => Lux.UIRect (cen, new Vec2S (size.Width + dy, size.Height + dy), 16, 8, new (255, 64, 66, 68), new (255, 200, 202, 204))
      ) { Streaming = true };
      var gvn = new GroupVN ([vn1, vn2]);
      Root = gvn;
   }

   TypeFace mFace;
}
