using System.ComponentModel.DataAnnotations;
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
      List<Poly> poly = [];
      Dwg2 dwg = DXFReader.Load ("c:/etc/tess0.dxf");
      for (int i = 0; i < dwg.Ents.Count; i++) {
         if (dwg.Ents[i] is not E2Poly e2p) continue;
         if (e2p.Layer.Name != "0" || e2p.Poly.IsOpen) continue;
         poly.Add (e2p.Poly);
      }

      int n = poly.MaxIndexBy (a => a.GetBound ().Area);
      mT = new Triangulator ();
      mT.Reset ();
      for (int i = 0; i < poly.Count; i++) mT.AddPoly (poly[i], i != n);
      mSteps = mT.Process ().GetEnumerator ();
      HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (a => OnClick ());

      mDebug = new TriangulatorDebug (mT);
      List<VNode> nodes = [TraceVN.It, mDebug];

      Bound = new Bound2 (poly.Select (a => a.GetBound ())).InflatedL (2);
      BgrdColor = Color4.Gray (200);
      Root = new GroupVN (nodes);
      // for (int i = 0; i < 82; i++) OnClick ();
   }
   Triangulator mT;
   IEnumerator<string> mSteps;
   VNode mDebug;

   public void OnClick () {
      if (!mSteps.MoveNext ()) return;
      Lib.Trace ($"{++mN}) {mSteps.Current}");
      mDebug.Redraw ();
   }
   int mN;
}

class TriangulatorDebug : VNode {
   public TriangulatorDebug (Triangulator t) { T = t; Streaming = true; }
   readonly Triangulator T;

   public override void Draw () {
      Dwg2 dwg = T.GetDebugDwg ();
      DXFWriter.Save (dwg, "c:/etc/test.dxf");
      var set = dwg.Ents.OfType<E2Poly> ().ToList ();
      var set1 = set.Where (a => a.Layer.Name == "MARKER").Select (a => a.Poly).ToList ();
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (0, Color4.DarkBlue, 2f);
      Lux.Polys (set1.AsSpan ());

      set1 = [.. set.Where (a => a.Layer.Name == "0").Select (a => a.Poly)];
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (0, Color4.Black, 2f);
      Lux.Polys (set1.AsSpan ());

      set1 = [.. set.Where (a => a.Layer.Name == "TILE").Select (a => a.Poly)];
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (-1, Color4.Red, 5f);
      Lux.Polys (set1.AsSpan ());

      set1 = [.. set.Where (a => a.Layer.Name == "LINKS").Select (a => a.Poly)];
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (1, Color4.Blue, 1.2f);
      Lux.Polys (set1.AsSpan ());

      set1 = [.. set.Where (a => a.Layer.Name == "DIAG").Select (a => a.Poly)];
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (2, Color4.DarkGreen, 3f);
      Lux.Polys (set1.AsSpan ());

      var texts = dwg.Ents.OfType<E2Text> ().ToList ();
      var polys = texts.SelectMany (a => a.Polys).ToList ();
      (Lux.ZLevel, Lux.Color, Lux.LineWidth) = (1, Color4.DarkGreen, 1.2f);
      Lux.Polys (polys.AsSpan ());
   }
}
