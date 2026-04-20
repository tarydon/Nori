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

      string file = System.IO.Directory.GetFiles ("c:\\etc", "demo*.dxf")[^1];
      file = "c:\\etc\\demo1\\lefthorn.dxf";
      Title = file;
      var dwg = DXFReader.Load (file);
      var polys = dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly).OrderBy (a => a.GetBound ().Midpoint.X).ToList ();
      CSMesher mesher = new ([polys[0]], [polys[1]]) { Tess = ETess.VeryFine };
      var mesh = mesher.Build ();

      Lux.UIScene = new Scene3 { 
         Root = new Mesh3VN (mesh.Wireframed ()) { Color = Color4.White },
         Bound = mesh.Bound,
         BgrdColor = Color4.Gray (200)
      };
      Lux.BackFacesPink = true;
   }
}
