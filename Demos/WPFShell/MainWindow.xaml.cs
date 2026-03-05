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
      var dwg = DXFReader.Load ("c:/etc/tess/E.dxf");
      var polys = dwg.Ents.OfType<E2Poly> ()
                     .Where (a => a.Layer.Name == "0" && a.Poly.IsClosed)
                     .Select (a => a.Poly)
                     .ToList ();
      int nOuter = polys.MaxIndexBy (a => a.GetBound ().Area);

      mT = new Triangulator ();
      mT.Reset (41, 0.0812 * 2);
      for (int i = 0; i < polys.Count; i++) mT.AddPoly (polys[i], i != nOuter);
      mSteps = mT.Process ().GetEnumerator ();
      HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (a => OnClick ());

      var xfm = Matrix2.Rotation (mT.BiasAngle);
      for (int i = 0; i < dwg.Ents.Count; i++) dwg.Ents[i] *= xfm;

      Bound = dwg.Bound.InflatedL (1).InflatedF (1.1f);
      BgrdColor = Color4.Gray (216);
      List<VNode> nodes = [new Dwg2VN (dwg), TraceVN.It, mDebugVN = new TessDebugVN (mT)];
      Root = new GroupVN (nodes);

      for (; ; ) { string s = OnClick (); if (s == "Ready to merge") break; }
   }

   string OnClick () {
      if (!mSteps.MoveNext ()) return "";
      Lib.Trace ($"{++mN}. {mSteps.Current}");
      mDebugVN.Redraw ();
      return mSteps.Current;
   }

   Triangulator mT;
   IEnumerator<string> mSteps;
   VNode mDebugVN;
   int mN;
}

class TessDebugVN : VNode {
   public TessDebugVN (Triangulator mt) => (mT, Streaming) = (mt, true);
   readonly Triangulator mT;

   public override void Draw () {
      var dwg = mT.GetDebugDwg ();
      DXFWriter.Save (dwg, "c:/etc/test.dxf");
      DrawPoly ("TILE", Color4.Red, 4f);
      DrawText ("TILETEXT", Color4.Blue);
      DrawText ("VERTTEXT", Color4.DarkGreen);
      DrawPoly ("LINKS", Color4.Blue, 1.5f);
      DrawPoly ("TRIANGLES", Color4.Blue, 3f);
      DrawPoints ("TRIANGLES", Color4.Blue);
      FillTris ("TRIANGLES", new Color4 (128, 255, 255, 0));

      // Helpers ..........................................
      void DrawPoly (string layer, Color4 color, float lineWidth) {
         (Lux.Color, Lux.LineWidth) = (color, lineWidth);
         foreach (var e2p in dwg.Ents.OfType<E2Poly> ()) 
            if (e2p.Layer.Name == layer) Lux.Poly (e2p.Poly);
      }

      void DrawPoints (string layer, Color4 color) {
         (Lux.Color, Lux.PointSize) = (color, 4f);
         foreach (var e2p in dwg.Ents.OfType<E2Point> ())
            if (e2p.Layer.Name == layer) Lux.Points ([e2p.Pt]);
      }

      void DrawText (string layer, Color4 color) {
         (Lux.Color, Lux.LineWidth) = (color, 1.5f);
         foreach (var e2t in dwg.Ents.OfType<E2Text> ())
            if (e2t.Layer.Name == layer) Lux.Polys (e2t.Polys.AsSpan ());
      }

      void FillTris (string layer, Color4 color) {
         (Lux.Color, Lux.ZLevel) = (color, -100);
         List<Vec2F> pts = [];
         foreach (var e2p in dwg.Ents.OfType<E2Poly> ())
            if (e2p.Layer.Name == layer) 
               for (int i = 0; i < 3; i++) pts.Add (e2p.Poly.Pts[i]);
         Lux.Triangles (pts.AsSpan ());
      }
   }
}
