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
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);

      string message = "Welcome to Nori.";
      var size = mFace.Measure (message, true);
      var vn1 = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace, Lux.ZLevel) = (new (255, 224, 226, 228), mFace, 1),
         () => Lux.Text (message, new (150, 210))
      );

      var vn2 = new SimpleVN (
         () => Lux.UIRect (new Vec2S (670, 165), new Vec2S (1200, 210), 16, 8, new (255, 64, 66, 68), new (255, 200, 202, 204))
      ) { Streaming = true };
      var gvn = new GroupVN ([vn1, vn2]);
      Root = gvn;
   }

   TypeFace mFace;
}