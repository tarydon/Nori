using System.ComponentModel.DataAnnotations;
using System.Windows;
using Nori;

namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();  // REMOVETHIS later
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.DarkBlue;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mDwg = DXFReader.Load ("c:/etc/LongPart.dxf");
      mDwg.Ents.RemoveIf (a => a is E2Bendline);
      BgrdColor = new Color4 (192, 196, 200);
      Bound = mDwg.Bound.InflatedF (1.05);
      VNode[] vnodes = [new Dwg2VN (mDwg), new DwgFillVN (mDwg), TraceVN.It];
      Root = new GroupVN (vnodes);
      Lib.Post (Process);
   }

   void Process () {
      List<Node> nodes = [];
      foreach (var p in mDwg.Polys) {
         var b = p.GetBound ();
         int x0 = (int)b.X.Min, x1 = (int)b.X.Max + 1;
         if (x1 - x0 > 1000) continue;
         int y = (int)b.Y.Mid, len = (int)(p.GetPerimeter () + 0.5);
         nodes.Add (new (x0, x1, y, len));
      }
      Optimizer opt = new (nodes);
      opt.Process ();
   }

   Dwg2 mDwg;
}
