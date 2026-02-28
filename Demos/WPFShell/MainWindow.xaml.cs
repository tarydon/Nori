using System.ComponentModel.DataAnnotations;
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
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new TessScene ();
   }
}

class TessScene : Scene2 {
   public TessScene () {
      var dwg = DXFReader.Load ("c:/etc/tess2.dxf");
      var xfm = Matrix2.Rotation (0.0812);
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
      List<Point2> pts = new ();
      List<int> splits = [0];
      for (int i = 0; i < polys.Count; i++) {
         var p = polys[i];
         if (p.GetWinding () == Poly.EWinding.CCW ^ i == n) p = p.Reversed ();
         p.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
         splits.Add (pts.Count); 
      }

      new Triangulator (pts, splits);

      Bound = dwg.Bound.InflatedF (1.2);
      BgrdColor = Color4.Gray (216);
      Root = new Dwg2VN (dwg);
   }
}

