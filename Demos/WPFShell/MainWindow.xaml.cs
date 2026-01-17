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
      Lux.UIScene = new MeshDemo ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);
      Root = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace) = (Color4.White, mFace),
         () => Lux.TextPx ("Welcome to Nori.", new Vec2S (100, 100))
      );
   }
   TypeFace mFace;
}

class MeshDemo : Scene3 {
   public MeshDemo () {
      Mesh3 mesh = Mesh3.Extrude (
         [Poly.Parse ("M0,0 H100 V30 Q80,50,1 H20 Q0,30,-1 Z"), Poly.Circle (new (80, 30), 10)],
         20, Matrix3.Rotation (EAxis.X, 30.D2R ()));

      Root = new Mesh3VN (mesh) { Color = Color4.White };
      Bound = mesh.Bound;
   }
}