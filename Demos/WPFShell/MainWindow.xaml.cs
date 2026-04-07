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
      Root = new GroupVN([new Demo2D (), TraceVN.It]);
   }
}

class DemoPx : VNode {
   public DemoPx () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(96 * Lux.DPIScale));
      Streaming = true;
   }

   public override void SetAttributes () 
      => (Lux.Color, Lux.TypeFace) = (Color4.White, mFace);

   public override void Draw () {
      List<Vec2F> vecs = [];
      // Lux.PxLines (vecs);

      Vec2S p = new (100, 400);
      string text = "Ha";
      Lux.TextPx (text, p);
      var r = mFace.Measure (text, true);

      Vec2F a = new (p.X + r.Left, p.Y + r.Bottom), b = new (p.X + r.Right, p.Y + r.Bottom);
      Vec2F c = new (p.X + r.Right, p.Y + r.Top), d = new (p.X + r.Left, p.Y + r.Top);
      vecs.AddM ([a, b, b, c, c, d, d, a]);
      Lux.PxLines (vecs.AsSpan ());
   }

   TypeFace mFace;
}

class Demo2D : VNode {
   public Demo2D () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(96 * Lux.DPIScale));
      Streaming = true;
   }

   public override void SetAttributes ()
      => (Lux.Color, Lux.TypeFace) = (Color4.White, mFace);

   public override void Draw () {
      List<Vec2F> vecs = [];
      vecs.AddM ([new (10, 10), new (90, 10), new (90, 10), new (90, 40),
                  new (90, 40), new (10, 40), new (10, 40), new (10, 10),
                  new (50, 10), new (50, 40), new (10, 25), new (90, 25)]);
      Lux.Lines (vecs.AsSpan ());

      Lux.Text2D ("Ha", new (50, 25), ETextAlign.BotLeft, Vec2S.Zero);
   }

   TypeFace mFace;
}
