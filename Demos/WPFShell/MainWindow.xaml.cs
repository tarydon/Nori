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
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      var dwg = DXFReader.Load (System.IO.Directory.GetFiles ("C:\\etc\\Fold", "*.dxf")[0]);
      var folder = new PaperFolder (dwg);
      folder.Process ();
      folder.Dump ("c:/etc/test.dxf");
      var dwg2 = DXFReader.Load ("c:/etc/test.dxf");

      Bound = dwg2.Bound.InflatedF (1.05);
      Root = new Dwg2VN (dwg2);
      BgrdColor = new Color4 (200, 208, 216);
   }
}
