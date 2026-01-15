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
      Lux.UIScene = new TriScene (this);
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

class TriScene : Scene3 {
   public TriScene (MainWindow m) {
      List<Point3> pos = [];
      var lines = System.IO.File.ReadAllLines ("c:/etc/sampletri.txt");
      for (int i = 0; i < 6; i++) {
         double[] f = [.. lines[i].Split ().Select (double.Parse)];
         pos.Add (new Point3 (f[0], f[1], f[2]));
      }
      List<int> tris = [0, 1, 2, 3, 4, 5];
      List<int> wires = [0, 1, 1, 2, 2, 0, 3, 4, 4, 5, 5, 3];
      Vector3 v1 = ((pos[1] - pos[0]) * (pos[2] - pos[0])).Normalized ();
      Vector3  v2 = ((pos[4] - pos[3]) * (pos[5] - pos[3])).Normalized ();
      List<Mesh3.Node> nodes = [];
      nodes.Add (new (pos[0], v1)); nodes.Add (new (pos[1], v1)); nodes.Add (new (pos[2], v1));
      nodes.Add (new (pos[3], v2)); nodes.Add (new (pos[4], v2)); nodes.Add (new (pos[5], v2));
      mMesh = new Mesh3 ([.. nodes], [.. tris], [.. wires]);
      double ff = mMesh.GetArea ();
      m.Title = $"Area: {ff}";

      Bound = mMesh.Bound;
      Root = new Mesh3VN (mMesh);
   }
   Mesh3 mMesh;
}