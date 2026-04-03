using System.IO;
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

class DemoScene : Scene3 {
   public DemoScene () {
      var file = System.IO.Directory.GetFiles ("C:\\etc\\Fold", "*.dxf")[0];
      var dwg = DXFReader.Load (file);
      System.IO.File.Move (file, "c:\\etc\\fold\\good\\" + Path.GetFileName (file));
      var folder = new PaperFolder (dwg);
      var model = folder.Process ();
      folder.Dump ("c:/etc/test.dxf");
      var dwg2 = DXFReader.Load ("c:/etc/test.dxf");

      Bound = model.Bound.InflatedF (1.05);
      Root = new Model3VN (model);
      BgrdColor = new Color4 (200, 208, 216);
   }
}
