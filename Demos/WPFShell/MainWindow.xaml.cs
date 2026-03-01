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
      var dwg = DXFReader.Load ("c:/etc/tess0.dxf");
      var xfm = Matrix2.Rotation (0.0812);
      xfm = Matrix2.Identity;
      List<Poly> polys = [];
      for (int i = dwg.Ents.Count - 1; i >= 0; i--) {
         if (dwg.Ents[i] is not E2Poly e2p || e2p.Layer.Name != "0") dwg.Ents.RemoveAt (i);
         else {
            e2p = (E2Poly)(e2p * xfm);
            dwg.Ents[i] = e2p;
            polys.Add (e2p.Poly);
         }
      }

      int n = polys.MaxIndexBy (a => a.GetBound ().Area);
      List<Point2> pts = [];
      mT = new Triangulator ();
      mT.Reset ();

      for (int i = 0; i < polys.Count; i++) {
         var p = polys[i];
         pts.Clear (); p.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         bool hole = (p.GetWinding () == Poly.EWinding.CCW ^ i == n);
         if (hole) pts.Reverse ();
         mT.AddContour (pts.AsSpan (), hole);
      }

      mSteps = mT.Process ().GetEnumerator ();
      HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (a => OnClick ());    

      Bound = dwg.Bound.InflatedL (1).InflatedF (1.1f);
      BgrdColor = Color4.Gray (216);
      List<VNode> nodes = [new Dwg2VN (dwg), TraceVN.It, mDebugVN = new TessDebugVN (mT)];
      Root = new GroupVN (nodes);
   }
   Triangulator mT;
   IEnumerator<string> mSteps;
   VNode mDebugVN;

   void OnClick () {
      if (mSteps.MoveNext ()) {
         Lib.Trace ($"{++mN}. {mSteps.Current}");
         mDebugVN.Redraw ();
      } 
   }
   int mN;
}

class TessDebugVN : VNode {
   public TessDebugVN (Triangulator mt) => (mT, Streaming) = (mt, true);
   readonly Triangulator mT;

   public override void Draw () {
      var dwg = mT.GetDebugDwg ();
      DrawPoly ("TILE", Color4.Red, 4f);
      DrawText ("TILETEXT", Color4.Blue);
      DrawText ("VERTTEXT", Color4.DarkGreen);
      DrawPoly ("LINKS", Color4.Blue, 1.5f);

      // Helpers ..........................................
      void DrawPoly (string layer, Color4 color, float lineWidth) {
         (Lux.Color, Lux.LineWidth) = (color, lineWidth);
         foreach (var e2p in dwg.Ents.OfType<E2Poly> ()) 
            if (e2p.Layer.Name == layer) Lux.Poly (e2p.Poly);
      }

      void DrawText (string layer, Color4 color) {
         (Lux.Color, Lux.LineWidth) = (color, 1.5f);
         foreach (var e2t in dwg.Ents.OfType<E2Text> ())
            if (e2t.Layer.Name == layer) Lux.Polys (e2t.Polys.AsSpan ());
      }
   }
}
