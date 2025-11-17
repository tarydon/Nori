// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ DwgScene.cs
// ║║║║╬║╔╣║ Demo scene to demonstrate various types of drawing entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace WPFDemo;

class DwgScene : Scene2 {
   public DwgScene () {
      mDwg = MakeDwg ();
      BgrdColor = new Color4 (200, 200, 206);
      Bound = mDwg.Bound.InflatedF (1.2);
      Root = new GroupVN ([new Dwg2VN (mDwg), new DwgFillVN (mDwg, 0)]);
   }
   readonly Dwg2 mDwg;

   Dwg2 MakeDwg () {
      var dwg = new Dwg2 ();
      var layer = dwg.CurrentLayer;

      List<Ent2> bSet = [];
      bSet.Add (new E2Poly (layer, Poly.Parse ("M-1,-1 V-3 H1 V-1 H3 V1 H1 V3 H-1 V1 H-3 V-1Z")));
      bSet.Add (new E2Point (layer, Point2.Zero));
      bSet.Add (new E2Poly (layer, Poly.Circle (Point2.Zero, 2)));
      Block2 b = new Block2 ("Cross", Point2.Zero, bSet);
      dwg.Add (b);

      Style2 s1 = new ("Std", "Simplex", 0, 1, 0);
      dwg.Add (s1);

      Point2[] pts = [new (80, 80), new (76, 58), new (70, 20), new (108, 78), new (102, 40), new (100, 20)];
      double[] knots = [0, 0, 0, 0, 20, 34, 54, 54, 54, 54];
      dwg.Add (new E2Spline (layer, new Spline2 ([.. pts], [.. knots], []), 0));

      dwg.Add (Poly.Parse ("M0,0 H200 V100 Q150,150,1 H0Z"));
      dwg.Add (Poly.Circle (new (150, 100), 20));
      dwg.Add (new E2Insert (dwg, layer, "Cross", new Point2 (15, 15), 45.D2R (), 4, 3));
      dwg.Add (new E2Solid (layer, Point2.List (30, 30, 40, 30, 40, 35, 30, 35)));
      dwg.Add (new E2Text (layer, s1, "Hello, World!", new Point2 (50, 20), 5, 0, 0, 1, ETextAlign.BaseLeft));

      dwg.Add (new E2Bendline (dwg, Point2.List (200, 75, 125, 0), Lib.HalfPI, 2, 0.42, 1));
      dwg.Add (new E2Bendline (dwg, Point2.List (200, 55, 145, 0), -Lib.HalfPI, 2, 0.42, 1));
      return dwg;
   }
}
