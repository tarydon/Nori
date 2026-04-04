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
      Lux.UIScene = new FirstScene ();
      Lux.AddSubScene (new SecondScene (), new Bound2 (0.625, 0.625, 0.975, 0.975));
   }
}

class FirstScene : Scene2 {
   public FirstScene () {
      Dwg2 dwg = DXFReader.Load ("N:/TData/Tenkai/Fold/A.dxf");
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      // Root = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg) { Color = Color4.White }]);
   }
}

class SecondScene : Scene3 {
   public SecondScene () {
      Dwg2 dwg = DXFReader.Load ("N:/TData/Tenkai/Fold/A.dxf");
      new PaperFolder (dwg).Process (out var model);

      BgrdColor = new Color4 (128, 160, 192);
      Bound = model!.Bound;
      Root = new Model3VN (model);
      // Root = new GroupVN ([new Model3VN (model)]);
   }
}
