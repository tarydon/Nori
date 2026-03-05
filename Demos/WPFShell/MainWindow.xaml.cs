using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media.Animation;
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
      TraceVN.TextColor = Color4.Yellow ; Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);
      var contentVN = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace) = (Color4.White, mFace),
         () => Lux.TextPx ("Welcome to Nori.", new Vec2S (100, 100))
      );
      Root = new GroupVN ([contentVN, TraceVN.It]);
      Lib.Trace ("TraceVN output...");
      Lib.Trace ($"Started at {DateTime.Now}");
   }
   TypeFace mFace;
}
