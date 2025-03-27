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
      Lux.UIScene = new TextScene ();
   }
}

class TextScene : Scene2 {
   public TextScene () {
      Bound = new Bound2 (-10, -10, 110, 60);
      mPts.AddRange ([new (0, 0), new (0, 20), new (20, 0), new (40, 0)]);
      var lf = LineFont.Get ("simplex");
      lf.Render ("A()\nCray{}", new (0, 20), ETextAlign.BotLeft, 0, 1, 2, 0, mPoly);
      Root = new PolyPointVN (mPts, mPoly);
   }

   List<Point> mPts = [];
   List<Poly> mPoly = [];

   public override Color4 BgrdColor => Color4.Gray (210);
}

class PolyPointVN (List<Point> pts, List<Poly> poly) : VNode {
   public override void SetAttributes () => Lux.Color = Color4.Black;

   public override void Draw () {
      Lux.Polys ([..poly]);
      Lux.Points ([..pts.Select (a => new Vec2F (a.X, a.Y))]);
   }
}
