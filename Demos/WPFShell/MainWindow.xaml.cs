using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;
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
      Lib.Tracer = TraceVN.Print;
      TraceVN.TextColor = Color4.Blue;
      new SceneManipulator ();
      Lux.UIScene = new FirstScene ();
      Lux.AddSubScene (new SecondScene (), new Bound2 (0.625, 0.625, 0.975, 0.975));
   }
}

class FirstScene : Scene2 {
   public FirstScene () {
      mDwg = DXFReader.Load ("N:/TData/Tenkai/Fold/A.dxf");
      Bound = mDwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      Root = new GroupVN ([new Dwg2VN (mDwg), new DwgFillVN (mDwg) { Color = Color4.White }, TraceVN.It]);
      mDisp = HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (OnClick);
   }
   IDisposable mDisp;
   Dwg2 mDwg;

   public override void Detached () => mDisp.Dispose ();

   void OnClick (MouseClickInfo mi) {
      if (Lux.PickScene (mi.Position) != this) return;
      Point2 pt = (Point2)PixelToWorld (mi.Position);
      mDwg.Add (pt);
   }
}

class SecondScene : Scene3 {
   public SecondScene () {
      Dwg2 dwg = DXFReader.Load ("N:/TData/Tenkai/Fold/A.dxf");
      for (int i = 0; i < 100; i++) new PaperFolder (dwg).Process (out _);
      new PaperFolder (dwg).Process (out mModel!);
      Lib.Trace ("Hello, world!");

      BgrdColor = new Color4 (128, 160, 192);
      Bound = mModel.Bound;
      Root = new Model3VN (mModel);
   }
   Model3 mModel;

   public override void Picked (object obj) {
      mModel.Ents.ForEach (a => a.IsSelected = false);
      if (obj is Ent3 ent) ent.IsSelected = true; 
   }
}
