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
      Lux.UIScene = new DemoScene ();
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
      var dwg = DXFReader.Load ("N:/TData/Misc/csmesher.dxf");
      var sPt = dwg.Ents.OfType<E2Point> ().Single (e => e.LayerName == "SIDE").Pt;
      var fPt = dwg.Ents.OfType<E2Point> ().Single (e => e.LayerName == "FRONT").Pt;
      var sPoly = dwg.Ents.OfType<E2Poly> ().Single (e => e.LayerName == "SIDE").Poly;
      var fPoly = dwg.Ents.OfType<E2Poly> ().Single (e => e.LayerName == "FRONT").Poly;
      sPoly *= Matrix2.Translation (-sPt.X, -sPt.Y);
      fPoly *= Matrix2.Translation (-fPt.X, -fPt.Y);
      var mesh = new CSMesher ([fPoly], [sPoly]).Build ();
      File.WriteAllText ("c:/etc/test.tmesh", mesh.ToTMesh ());

      Bound = mesh.Bound;
      Root = new Mesh3VN (mesh) { Color = Color4.White };
   }
}
