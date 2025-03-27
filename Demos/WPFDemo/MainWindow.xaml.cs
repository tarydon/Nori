// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using System.IO;
using Nori;
using System.Text;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();
      Lux.UIScene = new TextScene ();
   }
}

class TextScene : Scene2 {
   public TextScene () {
      mPts.AddRange ([new (0, 0), new (0, 5), new (0, 10), new (0, 15)]);
      var lf = LineFont.Get ("simplex");
      Out (0, 0, ETextAlign.BotLeft);
      Out (0, 5, ETextAlign.BaseLeft);
      Out (0, 10, ETextAlign.MidLeft);
      Out (0, 15, ETextAlign.TopLeft);

      mPts.AddRange ([new (15, 0), new (15, 10), new (15, 22), new (15, 34), new (34, 34)]);
      Out2 (15, 0, ETextAlign.BotLeft);
      Out2 (15, 10, ETextAlign.BaseLeft);
      Out2 (15, 22, ETextAlign.MidLeft);
      Out2 (15, 34, ETextAlign.TopLeft);
      Out2 (34, 34, ETextAlign.TopRight);

      mPts.AddRange ([new (8, 20), new (8, 25), new (8, 30), new (0, 17)]);
      Out3 (8, 20, ETextAlign.BaseLeft);
      Out3 (8, 25, ETextAlign.BaseCenter);
      Out3 (8, 30, ETextAlign.BaseRight);
      lf.Render ("TRIPE", new (0, 17), ETextAlign.BaseLeft, 15.D2R (), 1, 2, 0, mPoly);

      mPoly.Add (Poly.Line (-1, 5, 9, 5));
      mPoly.Add (Poly.Line (-1, 7, 9, 7));

      mPts.AddRange ([new (30, 0), new (33, 17), new (30, 22), new (43, 0), new (58, 0), new (52, 14), new (59, 21)]);
      Out4 (30, 0, ETextAlign.BaseLeft);
      Out4 (33, 17, ETextAlign.TopRight);
      Out4 (30, 22, ETextAlign.MidCenter);
      lf.Render ("ELONGATE", new (43, 0), ETextAlign.BaseLeft, 15.D2R (), 1.5, 3, 90.D2R (), mPoly);

      lf.Render ("Sub\nSaharan\nAntarctica", new (58, 0), ETextAlign.BaseRight, 0, 1, 1.5, 0, mPoly);
      lf.Render ("Sub\nSaharan\nAntarctica", new (52, 14), ETextAlign.MidCenter, 0, 1, 1.5, 0, mPoly);
      lf.Render ("Reversed", new Point2 (59, 21), ETextAlign.BaseLeft, 0, -0.5, 4, 0, mPoly);

      Bound = new Bound2 (mPoly.Select (a => a.GetBound ())).InflatedF (1.1);
      Root = new PolyPointVN (mPts, mPoly);

      var sb = new StringBuilder ();
      mPoly.ForEach (a => sb.AppendLine (a.ToString ()));
      mPts.ForEach (a => sb.AppendLine ($"P{a.X},{a.Y}"));
      File.WriteAllText ("c:/etc/output.txt", sb.ToString ());

      // Helpers ...............................................................
      void Out (double x, double y, ETextAlign align) 
         => lf.Render ("Cray{}", new (x, y), align, 0, 1, 2, 0, mPoly);
      void Out2 (double x, double y, ETextAlign align) 
         => lf.Render ("A()\nCray{}\n[123]", new (x, y), align, 0, 1, 1.5, 0, mPoly);
      void Out3 (double x, double y, ETextAlign align)
         => lf.Render ("MAX", new (x, y), align, 0, 0.5, 3, 0, mPoly);
      void Out4 (double x, double y, ETextAlign align)
         => lf.Render ("Hello\nWorld", new (x, y), align, 0, 1, 2, 30.D2R (), mPoly);
   }

   List<Point> mPts = [];
   List<Poly> mPoly = [];

   public override Color4 BgrdColor => Color4.Gray (210);
}

class PolyPointVN (List<Point> pts, List<Poly> poly) : VNode {
   public override void SetAttributes () {
      Lux.Color = Color4.Black;
      Lux.PointSize = 11f;
   }

   public override void Draw () {
      Lux.Polys ([..poly]);
      Lux.Points ([..pts.Select (a => new Vec2F (a.X, a.Y))]);
   }
}
