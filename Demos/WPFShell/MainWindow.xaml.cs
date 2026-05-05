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
      Lux.UIScene = new DemoScene3 ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);
      Root = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace) = (Color4.White, mFace),
         () => Lux.Text ("Welcome to Nori.", new Vec2S (100, Lux.PanelSize.Y - 100))
      );
   }
   TypeFace mFace;
}

class DemoScene3 : Scene3 {
   public DemoScene3 () {
      Ent3.MeshQuality = ETess.VeryCoarse;
      var spine = new BSpine (30, Lib.HalfPI, 0.5, true);
      var flex = new E3Flex (0, CoordSystem.World, 4, spine, [Poly.Rectangle (-50, 0, 50, spine.FlatWidth)]);
      mMesh = flex.Mesh;
      mMarker = new E3Marker (flex.GetTailCS (1), E3Marker.EKind.CS, 10) { Color = Color4.White };

      Bound = mMesh.Bound;
      BgrdColor = Color4.Gray (216);
      Root = new GroupVN ([VNode.MakeFor (mMarker), new SimpleVN (Draw) { Streaming = true }]);
   }
   Mesh3 mMesh;
   E3Marker mMarker;

   void Draw () {
      Lux.Color = Color4.White; Lux.LineWidth = 3;
      Lux.Mesh (mMesh);
      Lux.Color = Color4.Gray (128); Lux.LineWidth = 1.5f;
      Lux.MeshNormals (mMesh, 4);
   }
}