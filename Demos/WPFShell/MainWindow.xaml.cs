using System.ComponentModel.DataAnnotations;
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
      Lib.Tracer = TraceVN.Print;
      TraceVN.TextColor = Color4.Blue;
      new SceneManipulator ();

      Lux.UIScene = new Scene2 { BgrdColor = new Color4 (244, 248, 252), Root = new BaseVN () };
   }
}

class BaseVN : VNode {
   public BaseVN () => Streaming = true;   

   public override void Draw () {
      if (!mCreated) CreateSubscenes ();
      else {
         List<Vec2F> a = [];
         var size = Lux.PanelSize;
         foreach (var scene in Lux.SubScenes) {
            var r = scene.Rect;
            for (int i = 1; i < 6; i++) {
               int x0 = r.Left - i, x1 = r.Right + i - 1, y0 = size.Y - r.Top - i - 1, y1 = size.Y - r.Bottom + i - 2;
               Add (x0, y0); Add (x1, y0, 2); Add (x1, y1, 2); Add (x0, y1, 2); Add (x0, y0);
            }
         }
         Lux.Color = new Color4 (144, 148, 152);
         Lux.PxLines (a.AsSpan ());

         // Helpers .......................................
         void Add (int x, int y, int n = 1) { for (int i = 0; i < n; i++) a.Add (new (x, y)); }
      }
   }

   void CreateSubscenes () {
      if (!mCreated) {
         mCreated = true;
         var size = Lux.PanelSize;
         double xGutter = 18.0 / size.X, yGutter = 18.0 / size.Y, xMid = 0.5, yMid = 0.5;

         Color4 color = new (200, 208, 216);
         var dwg = DXFReader.Load ("N:/Demos/Data/Folder/02.dxf");
         Lux.AddSubScene (new DwgScene (dwg), new (xGutter, yGutter, xMid, 1 - yGutter));
         Lux.AddSubScene (new ModelScene (dwg), new (xMid + xGutter, yGutter, 1 - xGutter, yMid - yGutter));
         Lux.AddSubScene (new FoldScene (dwg), new (xMid + xGutter, yMid, 1 - xGutter, 1 - yGutter));
         Lib.Post (Redraw);
      }
   }

   bool mCreated;
}

class DwgScene : Scene2 {
   public DwgScene (Dwg2 dwg) {
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = new Color4 (232, 236, 240);
      Root = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg) { Color = Color4.White }]);
   }
}

class ModelScene : Scene3 {
   public ModelScene (Dwg2 dwg) {
      if (!new PaperFolder (dwg).Process (out var model)) return;
      Bound = model.Bound;
      BgrdColor = new Color4 (216, 252, 224);
      Root = new Model3VN (model);
      Lux.StartContinuousRender (Animate);
   }

   void Animate (double f) {
      mzRot += f * 20; if (mzRot > 360) mzRot -= 360;
      Viewpoint = (Viewpoint.XRot, mzRot);
   }
   double mzRot = 45;
}

class FoldScene : Scene3 {
   public FoldScene (Dwg2 dwg) {
      mDwg = dwg;
      mBends = [.. dwg.Ents.OfType<E2Bendline> ()];
      mAngles = [.. mBends.Select (a => a.Angle)];
      Lux.StartContinuousRender (Animate);
      BgrdColor = new Color4 (212, 216, 252);
   }

   void Animate (double f) {
      mLie += f * mDelta * 0.25; 
      if (mLie > 1) { mLie = 1; mDelta = -1; }
      if (mLie < 0) { mLie = 0; mDelta = 1; }
      for (int i = 0; i < mBends.Length; i++) 
         mBends[i].Angle = mAngles[i] * mLie;
      if (!new PaperFolder (mDwg).Process (out var model)) return;
      Root = new Model3VN (model);
      Bound = model.Bound; 
   }
   Dwg2 mDwg;
   E2Bendline[] mBends;
   double[] mAngles;
   double mLie, mDelta = 1;
}
