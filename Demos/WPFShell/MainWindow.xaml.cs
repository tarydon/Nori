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
      BgrdColor = new Color4 (240, 244, 248);
      Root = new SimpleVN (Draw) { Streaming = true };
   }

   void Draw () {
      Lux.UIRect (new (250, 150), new (300, 100), 6, 2, new Color4 (251, 252, 253), new Color4 (0, 96, 160));
      Lux.UIRect (new (250, 300), new (300, 100), 6, 0, new Color4 (251, 252, 253), new Color4 (251, 252, 253));
   }
}
