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
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      var tess = ETess.VeryCoarse;
      mPolys = [
         Poly.Parse ("M0,0 H400 V100 Q300,200,1 H0 Z").DiscretizeP (tess),
         Poly.Rectangle (50, 50, 150, 101),
         Poly.Circle (new (300, 100), 50).DiscretizeP (tess)
      ];

      Bound = new Bound2 (-10, -10, 410, 210);
      BgrdColor = Color4.Gray (216);
      Root = new SimpleVN (Draw) { Streaming = true };

      int max = 10;
      for (int i = 0; i <= max; i++) {
         Ys.Add (((double)i / max).Along (-0.1, 200.1)); 
      }

      var tesser = FastTess2D.Borrow ();
      tesser.BiasAngle = 0.0001;
      for (int i = 0; i < mPolys.Length; i++) {
         mPolys[i] = Slice (mPolys[i]);
         tesser.AddPoly (mPolys[i], i > 0);
      }
      tesser.Process ();
      mPts = tesser.Pts;
      mTris = tesser.Tris;
   }
   Poly[] mPolys;
   List<Point2> mPts;
   List<int> mTris;
   List<double> Ys = [];

   Poly Slice (Poly input) {
      var pb = new PolyBuilder ();
      foreach (var s in input.Segs) {
         pb.Line (s.A);
         if (s.B.Y.EQ (s.A.Y)) continue;
         if (s.B.Y > s.A.Y) {
            foreach (var y in Ys) {
               if (y <= s.A.Y || y >= s.B.Y) continue;
               double lie = y.GetLieOn (s.A.Y, s.B.Y);
               double x = lie.Along (s.A.X, s.B.X);
               pb.Line (new (x, y));
            }
         } else {
            for (int i = Ys.Count - 1; i >= 0; i--) {
               double y = Ys[i];
               if (y <= s.B.Y || y >= s.A.Y) continue;
               double lie = y.GetLieOn (s.A.Y, s.B.Y);
               double x = lie.Along (s.A.X, s.B.X);
               pb.Line (new (x, y));
            }
         }
      }
      return pb.Close ().Build ();
   }

   void Draw () {
      Lux.LineWidth = 2f; Lux.Color = Color4.Black;
      Lux.Polys (mPolys);

      List<Vec2F> lines = [];
      for (int i = 0; i < mTris.Count; i++) {
         int j = i + 1; if (j % 3 == 0) j -= 3;
         lines.Add (mPts[mTris[i]]); lines.Add (mPts[mTris[j]]);
      }
      Lux.LineWidth = 1.2f; Lux.Color = Color4.Gray (160);
      Lux.Lines (lines.AsSpan ());
   }
}
