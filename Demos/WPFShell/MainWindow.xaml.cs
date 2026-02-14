using System.Reactive.Linq;
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
      TraceVN.TextColor = Color4.Blue; Lib.Tracer = TraceVN.Print;
      TraceVN.HoldTime = 20;
      new SceneManipulator ();
      Lux.UIScene = new TessScene ();
   }
}

class TessScene : Scene2 {
   public TessScene () {
      Dwg2 dwg = DXFReader.Load ("c:/etc/Tess0.dxf");
      Bound = dwg.Bound.InflatedL (2);
      BgrdColor = Color4.Gray (200);

      //List<Point2> tmp = [];
      //List<Poly> input = [.. dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0").Select (a => a.Poly)];
      //for (int i = 0; i < input.Count; i++) {
      //   var poly = input[i];
      //   if (poly.HasArcs) {
      //      tmp.Clear (); poly.Discretize (tmp, Lib.FineTess, Lib.FineTessAngle);
      //      poly = Poly.Lines (tmp, true);
      //   }
      //}

      mT = new ([..dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0").Select (a => a.Poly)]);
      mSteps = mT.Process ().GetEnumerator ();
      HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (a => OnClick ());

      mDebug = new TriDebug (mT);
      List<VNode> nodes = [new Dwg2VN (dwg), new DwgFillVN (dwg), TraceVN.It, mDebug];
      Root = new GroupVN (nodes);
      for (int i = 0; i < 15; i++) OnClick ();
   }
   Triangulator mT;
   IEnumerator<string> mSteps;
   TriDebug mDebug;

   public void OnClick () {
      if (!mSteps.MoveNext ()) return;
      Lib.Trace (mSteps.Current);
      mDebug.Dirty ();
      mT.DumpNodes ("c:/etc/test.txt");
   }
}

class TriDebug : VNode {
   public TriDebug (Triangulator t) => T = t;
   readonly Triangulator T;

   public void Dirty () {
      Redraw ();
   }

   public override void SetAttributes () => (Lux.Color, Lux.ZLevel, Lux.LineWidth) = (Color4.Red, -1, 2);
   public override void Draw () {
      foreach (var t in T.GetTrapezoids ()) {
         Lux.Poly (t.Item2);
         Lux.Text2D ($"{t.Item1}", (Vec2F)t.Item2.A, ETextAlign.BaseLeft, new Vec2S (5, 5));
      }
   }
}