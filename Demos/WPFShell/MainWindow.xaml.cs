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

class DemoScene : Scene2 {
   public DemoScene () {
      var dr = new DXFReader ("C:/DropBox/Nori/Dimension/Dimensions.dxf");
      dr.DarkenColors = dr.WhiteToBlack = dr.RelayerDimensions = true;
      var dwg = dr.Load ();

      Bound = dwg.Bound.InflatedF (1.1);
      Root = new Dwg2VN (dwg);
      BgrdColor = Color4.Gray (216);
   }
}
