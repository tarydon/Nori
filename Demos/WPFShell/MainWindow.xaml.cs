using System.IO;
using System.IO.Compression;
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
      Lux.UIScene = new DemoScene2 ();
   }
}

class DemoScene2 : Scene3 {
   public DemoScene2 () {
      var zar = new ZipArchive (File.OpenRead ("N:/TData/IO/MESH/cow.zip"));
      var ze = zar.GetEntry ("cow.obj")!;
      var zstm = new ZipReadStream (ze.Open (), ze.Length);
      var mesh = Mesh3.LoadObj (zstm.ReadAllLines ());
      mesh *= Matrix3.Rotation (EAxis.X, Lib.HalfPI) * Matrix3.Rotation (EAxis.Z, -Lib.HalfPI);
      mesh *= Matrix3.Translation (1, 2, 3);

      Bound = mesh.Bound;
      Root = new Mesh3VN (mesh);
   }
}

class DemoScene : Scene3 {
   public DemoScene () {
      var dwg = DXFReader.Load ("c:/etc/project.dxf");
      var plines = dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly).ToList ();
      var side = plines[0]; var front = plines[1];

      var dwg2 = new Dwg2 ();
      dwg2.Add (front.DiscretizeP (ETess.Coarse));
      DXFWriter.Save (dwg2, "c:/etc/test.dxf");

      side *= Matrix2.Translation (-side.GetBound ().X.Mid, 0);
      front *= Matrix2.Translation (-front.GetBound ().X.Mid, 0);
      var csm = new CSMesher ([front], [side]);
      var mesh = csm.Build ();

      // var mesh = Mesh3.Extrude ([side], 100, Matrix3.Rotation (EAxis.X, Lib.HalfPI));
      Bound = mesh.Bound;
      BgrdColor = new Color4 (64, 96, 128);
      Root = new Mesh3VN (mesh) { Color = Color4.White };
   } 
}
