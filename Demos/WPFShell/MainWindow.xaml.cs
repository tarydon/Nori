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
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);
      TraceVN.TextColor = Color4.Yellow; Lib.Tracer = TraceVN.Print;
      Root = new GroupVN([new DemoVN (), TraceVN.It]);
   }
}

class DemoVN : VNode {
   public DemoVN () { }

   public override void SetAttributes () 
      => Lux.Color = Color4.White;

   public override void Draw () {
      byte[] D = Lib.ReadBytesFromZip ("c:/etc/Logo.zip", "Logo.bmp");
      for (int y = 0; y < 128; y++)
         for (int x = 0; x < 128; x++) {
            int n = 150 + y * 512 + x * 4;
            byte b = D[n], g = D[n + 1], r = D[n + 2], a = D[n + 3];
            Lux.PxPoint (new (x + 10, 138 - y), new Color4 (a, r, g, b));
         }

      List<Vec2S> pts = [];
      for (int i = 0; i <= 100; i += 10) {
         pts.Add (new (140 + i, 20));
         pts.Add (new (140, 120 - i));
      }
      Lux.Lines (pts.AsSpan ());

      pts.Clear ();
      pts.AddM (new (280, 20), new (380, 20));
      Lux.Lines (pts.AsSpan ());
   }
}
