// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangulator_WPFShell.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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
      Dwg2 dwg = DXFReader.Load ("N:/TData/Geom/Tess/D.dxf");
      for (int i = 0; i < dwg.Ents.Count; i++) {
         if (dwg.Ents[i] is not E2Poly e2p) continue;
         if (e2p.Layer.Name != "0" || e2p.Poly.IsOpen) continue;
         poly.Add (e2p.Poly);
      }

      int n = poly.MaxIndexBy (a => a.GetBound ().Area);
      mT = new Triangulator ();
      mT.Reset ();
      for (int i = 0; i < poly.Count; i++) mT.AddPoly (poly[i], i != n);
      mT.Process (); 

      mDebug = new TriangulatorDebug (mT);
      List<VNode> nodes = [TraceVN.It, mDebug];

      Bound = mT.Bound.InflatedL (1).InflatedF (1.05);
      BgrdColor = Color4.Gray (200);
      Root = new GroupVN (nodes);
   }
   Triangulator mT;
   VNode mDebug;
}

class TriangulatorDebug : VNode {
   public TriangulatorDebug (Triangulator t) { T = t; Streaming = true; }
   readonly Triangulator T;

   public override void Draw () {
      Dwg2 dwg = T.GetDebugDwg ();
      DXFWriter.Save (dwg, "c:/etc/test.dxf");

      DrawPoly ("OUTLINE", Color4.Black, 2f);
      DrawPoly ("TILE", Color4.Red, 3f);
      DrawText ("VERTNO", Color4.DarkGreen);
      DrawText ("TILETEXT", Color4.Black);
      DrawPoly ("LINKS", Color4.Blue, 1.5f);
      DrawPoly ("TRIS", Color4.Gray (144), 1.5f);
      DrawPoints ("TRIS", Color4.Gray (144));

      void DrawPoly (string layer, Color4 color, float lineWidth) {
         (Lux.Color, Lux.LineWidth) = (color, lineWidth);
         foreach (var e2p in dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == layer))
            Lux.Poly (e2p.Poly);
      }

      void DrawText (string layer, Color4 color) {
         (Lux.Color, Lux.LineWidth) = (color, 1.5f);
         foreach (var e2t in dwg.Ents.OfType<E2Text> ().Where (a => a.Layer.Name == layer))
            Lux.Polys (e2t.Polys.AsSpan ());
      }

      void DrawPoints (string layer, Color4 color) {
         (Lux.Color, Lux.PointSize) = (color, 4f);
         foreach (var e2p in dwg.Ents.OfType<E2Point> ().Where (a => a.Layer.Name == layer))
            Lux.Points ([e2p.Pt]);
      }
   }
}
