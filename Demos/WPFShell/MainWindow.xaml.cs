using System.Reactive.Linq;
using System.Windows;
using Nori;
namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); Lux2.Init ();
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Blue; TraceVN.HoldTime = 200;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene3 {
   public DemoScene () {
      var model = MakeModel ();
      var pose = new BendPose (model);
      pose.SetLie (0.5);

      Bound = pose.GetBound ();
      Root = new GroupVN (pose.Nodes.Select (a => new BPoseNodeVN (a)));
      _ = model.Bound;
      Lib.Testing = true;
      CurlWriter.Save (model, "c:/etc/test.curl");

      var dib = RenderZoomedImage (new Vec2S (480, 303), DIBitmap.EFormat.Gray8, out int y);
      new PNGWriter (dib).Write ("c:/etc/test.png");
   }

   // Makes a sheet-metal model
   Model3 MakeModel () {
      Model3 model = new ();
      E3Flat p1, p3, p5, p7; E3Flex f2, f4, f6;
      model.Ents.Add (p1 = new E3Flat (1, CoordSystem.World, 4, [Poly.Rectangle (-100, -100, 100, 100), Poly.Rectangle (-40, -40, 40, 40)]));
      var cs1 = new CoordSystem (new (100, 0, 0), -Vector3.YAxis, Vector3.XAxis);
      var spine = new BSpine (8, 1.25 * Lib.HalfPI, 0.5, true);
      model.Ents.Add (f2 = new E3Flex (2, cs1, 4, spine, [Poly.Rectangle (-100, 0, 100, spine.FlatWidth)]));
      var cs2 = f2.GetTailCS (1);
      model.Ents.Add (p3 = new E3Flat (3, cs2, 4, [Poly.Rectangle (-100, 0, 100, 20)]));
      var cs3 = new CoordSystem (cs2.Org + cs2.VecY * 20, cs2.VecX, cs2.VecY);
      var spine2 = new BSpine (24, Lib.HalfPI, 0.5, false);
      model.Ents.Add (f4 = new E3Flex (4, cs3, 4, spine2, [Poly.Rectangle (-100, 0, 100, spine2.FlatWidth), Poly.Rectangle (-90, 10, -60, 27)]));
      var cs4 = f4.GetTailCS (1);
      model.Ents.Add (p5 = new E3Flat (5, cs4, 4, [Poly.Rectangle (-100, 0, 100, 20)]));
      var cs5 = new CoordSystem (new (0, -40, 0));
      var spine3 = new BSpine (8, 0.8 * Lib.HalfPI, 0.5, true);
      model.Ents.Add (f6 = new E3Flex (6, cs5, 4, spine3, [Poly.Rectangle (-35, 0, 35, spine3.FlatWidth)]));
      var cs6 = f6.GetTailCS (1);
      model.Ents.Add (p7 = new E3Flat (7, cs6, 4, [Poly.Rectangle (-35, 0, 35, 62), Poly.Rectangle (-25, 10, 0, 25)]));
      f2.Parent = p1; p3.Parent = f2; f4.Parent = p3; p5.Parent = f4; f6.Parent = p1; p7.Parent = f6;
      return model;
   }
}