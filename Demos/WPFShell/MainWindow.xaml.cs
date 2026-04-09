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

class DemoScene : Scene3 {
   public DemoScene () {
      var dwg = DXFReader.Load ("c:/etc/project.dxf");
      var plines = dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly).ToList ();
      var side = plines[0]; var front = plines[1];

      var dwg2 = new Dwg2 ();
      dwg2.Add (side.DiscretizeP (Lib.CoarseTess, Lib.CoarseTessAngle));
      dwg2.Add (front.DiscretizeP (Lib.CoarseTess, Lib.CoarseTessAngle));
      DXFWriter.Save (dwg2, "c:/etc/discrete.dxf");

      var mesh = Mesh3.Extrude ([side, front], 100, Matrix3.Rotation (EAxis.X, Lib.HalfPI));

      Bound = mesh.Bound;
      BgrdColor = new Color4 (64, 96, 128);
      Root = new Mesh3VN (mesh) { Color = Color4.White };
   }
}
