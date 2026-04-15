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

      var dwg = DXFReader.Load ("C:/ETC/DEMO.dxf");
      var polys = dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly).OrderBy (a => a.GetBound ().Midpoint.X).ToList ();
      mMesher = new CSMesher ([polys[0]], [polys[1]]);
      mEnum = mMesher.IncBuild ().GetEnumerator ();
   }
   IEnumerator<string> mEnum;

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Blue; TraceVN.HoldTime = 20;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();

      Lux.UIScene = new Scene2 { BgrdColor = Color4.Gray (128) };
      // HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (a => OnClick ());
      HW.Keys.Where (a => a.IsPress (EKey.Space)).Subscribe (a => Next ());
   }
   CSMesher mMesher;
   Scene2? mScene2;
   Scene3? mScene3;

   void Next () {
      if (!mEnum.MoveNext ()) return;
      Lib.Trace (mEnum.Current);
      if (mScene2 != null) Lux.RemoveSubScene (mScene2);
      if (mScene3 != null) Lux.RemoveSubScene (mScene3);

      var dwg = mMesher.Dwg;
      var gvn = new GroupVN ([new Dwg2VN (dwg), TraceVN.It]);
      mScene2 = new Scene2 { Root = gvn, Bound = dwg.Bound.InflatedF (1.1), BgrdColor = Color4.Gray (216) };
      Lux.AddSubScene (mScene2, new (0.02, 0.02, 0.49, 0.98));

      var mesh = mMesher.Mesh;
      mScene3 = new Scene3 { Root = new Mesh3VN (mesh), Bound = mesh.Bound, BgrdColor = Color4.Gray (200) };
      Lux.AddSubScene (mScene3, new (0.51, 0.02, 0.98, 0.98));
   }
}
