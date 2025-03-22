// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using System.IO;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();
      Lux.UIScene = new FillScene ();
   }


}

class FillScene : Scene2 {
   public FillScene () {
      var lines = File.ReadAllLines ($"{Lib.DevRoot}/TData/Misc/LeafDwg.txt");
      int n = 0, cPoly = lines[n++].ToInt ();

      // pts[0] is the midpoint of the entire drawing. Taking every segment of every
      // pline in the set, we draw a triangle with pts[0] as one of the vertices. Since
      // all the triangles share this common vertex, it is best to draw these using a
      // triangle-fan primitive. 
      List<int> indices = [];
      List<Vec2F> pts = [new (0, 0)], trace = [];
      Bound2 b = new ();
      // If the first polygon is using indices 1,2,3,4 then we can draw all the triangles
      // related to this using the indices set: [0,1,2,3,4,1,-1]. The initial 0 is the 
      // midpoint of the entire set and the subsequent vertices define the triangles 
      // (0,1,2), (0,2,3), (0,3,4), (0,4,1). The final -1 causes this triangle fan 
      // primtive set to be terminated a new one started with the next polygon
      for (int i = 0; i < cPoly; i++) {
         int cVerts = lines[n++].ToInt ();
         indices.Add (0); 
         int idx0 = pts.Count;
         for (int j = 0; j < cVerts; j++) {
            indices.Add (pts.Count);
            double[] v = [.. lines[n++].Split (',').Select (double.Parse)];
            var pt = new Point2 (v[0], v[1]);
            b += pt; pts.Add (pt);
            trace.Add (pt); if (j > 0) trace.Add (pt);
         }
         trace.Add (pts[idx0]);
         indices.AddRange ([idx0, -1]);
      }
      pts[0] = b.Midpoint;

      Bound = b.InflatedF (1.1);
      Root = new GroupVN ([new LinesNode (trace), new FillNode (pts, indices, b)]);
   }

   public override Color4 BgrdColor => Color4.Gray (160);
}

class LinesNode (List<Vec2F> pts) : VNode {
   public override void SetAttributes () => Lux.Color = Color4.Black;
   public override void Draw () => Lux.Lines (pts.AsSpan ());
}

class FillNode (List<Vec2F> pts, List<int> indices, Bound2 bound) : VNode {
   public override void SetAttributes () => Lux.Color = new Color4 (192, 255, 192);
   public override void Draw () => Lux.FillPath (pts.AsSpan (), indices.AsSpan (), bound);
}