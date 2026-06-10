using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
      Lux.UIScene = new UnfoldScene ();
   }
}

class UnfoldScene : Scene2 {
   public UnfoldScene () {
      var model = STEPReader.Load ("N:/TData/STEP/S00178.stp");
      var shmodel = new SheetMetalizer (model).Process ();
      var dwg = new Unfolder (shmodel).Process ().Value;

      List<VNode> nodes = [new Dwg2VN (dwg), new DwgFillVN (dwg, ETess.Medium), TraceVN.It];
      Bound = dwg.Bound.InflatedF (1.05);
      Root = new GroupVN (nodes);
   }
}

class NewScene : Scene3 {
   public NewScene () {
      var model = STEPReader.Load ("N:/TData/STEP/S00178.stp");
      var shmodel = new SheetMetalizer (model).Process ();

      List<VNode> nodes = [];
      var pose = new BendPose (shmodel);
      pose.SetLie (0.5);
      nodes.AddRange (pose.Nodes.Select (a => new BPoseNodeVN (a)));
      nodes.Add (TraceVN.It);

      Bound = pose.GetBound (0);
      var dwg = GetDrawing (pose);
      // nodes.Clear ();

      var b2 = dwg.Bound;
      Bound = new Bound3 (b2.X.Min, b2.Y.Min, -10, b2.X.Max, b2.Y.Max, 10);
      nodes.Add (new Dwg2VN (dwg));
      BgrdColor = new Color4 (90, 100, 110);
      Root = new GroupVN (nodes);
   }

   Dwg2 GetDrawing (BendPose pose) {
      Dwg2 dwg = new ();
      pose.SetLie (0);

      var plane = (E3Flat)pose.Nodes.First ().Ent;
      var xfmRoot = Matrix3.From (plane.CS);

      foreach (var node in pose.Nodes) {
         if (node.Ent is E3Flat flat) {
            var xfm = flat.ToXfm * node.Xfm * xfmRoot;
            foreach (var shape in flat.Shape)
               dwg.Add (shape * xfm);
         }
         if (node.Ent is E3Flex flex) {
            var xfm = flex.ToXfm * node.Xfm * xfmRoot;
            foreach (var shape in flex.Shape)
               dwg.Add (shape * xfm);
         }
      }
      return dwg;
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
