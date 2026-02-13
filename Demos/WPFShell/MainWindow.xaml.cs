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
      TraceVN.TextColor = Color4.Blue; Lib.Tracer = TraceVN.Print;
      TraceVN.HoldTime = 20;
      new SceneManipulator ();
      Lux.UIScene = new TessScene ();
   }
}

class TessScene : Scene2 {
   public TessScene () {
      Dwg2 dwg = DXFReader.Load ("c:/etc/Tess0.dxf");
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (200);

      Triangulator t = new ([..dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0").Select (a => a.Poly)]);
      t.Process ();

      List<VNode> nodes = [new Dwg2VN (dwg), new DwgFillVN (dwg), TraceVN.It];
      Root = new GroupVN (nodes);
   }
}
