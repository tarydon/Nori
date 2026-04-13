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
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      Dwg2 dwg = new Nori.Alt.DXFReader ("N:/TData/IO/DXF/Poly.dxf").Load ();
      Bound = dwg.Bound.InflatedF (1.25);
      Root = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg)]);
      BgrdColor = Color4.Gray (216);
   }
}
