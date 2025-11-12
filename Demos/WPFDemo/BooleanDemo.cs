// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ BooleanDemo.cs
// ║║║║╬║╔╣║ Demo for Union, Intersection, Subtraction boolean operations
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;
using System;

class BooleanScene : Scene2 {
   public BooleanScene () {
      List<List<Poly>> polys = [
         [Poly.Polygon ((300, 350), 300, 3),
         Poly.Polygon ((300, 350), 300, 3, Lib.PI)],

         [Poly.Circle ((325, 1250), 200),
         Poly.Circle ((200, 1000), 200),
         Poly.Circle ((425, 1000), 200)],

         [Poly.Rectangle (850, 50, 1350, 550),
         Poly.Circle ((1100, 300), 275)],

         [Poly.Circle ((1150, 1050), 250),
         Poly.Rectangle (850, 950, 1450, 1150)],

         // Results in multiple intersection polys
         [Poly.Rectangle (1700, 50, 2150, 650),
         Poly.Rectangle (1850, 150, 2300, 550).Subtract (Poly.Rectangle (1850, 275, 2225, 425)).First ()],

         // Combine outer with hole
         [Poly.Parse ("M1700,850H2500V1250Q2300,1450,-1H1900Q1700,1250,1Z"),
         Poly.Circle ((2000, 1150), 180)]
      ];
      BgrdColor = Color4.Gray (216);
      var bound = new Bound2 (polys.SelectMany (a => a).Select (a => a.GetBound ()));
      bound += new Point2 (2 * bound.X.Max, 2 * bound.Y.Max);
      Bound = bound.InflatedF (1.05);
      Root = new BooleanRootVN (polys, bound);
   }
}

class BooleanRootVN (List<List<Poly>> polys, Bound2 bound) : VNode {
   enum EPane { None = -1, Polys = 0, Union = 1, Intersection = 2, Subtraction = 3 }

   readonly Bound2 ViewBound = bound;
   readonly List<List<Poly>> mPolys = polys;

   public override void SetAttributes () {
      Lux.LineWidth = 2f;
      Lux.Color = Color4.Gray (150);
      Lux.LineType = ELineType.Continuous;
   }

   public override VNode? GetChild (int n) {
      if (n > 3) return null;
      EPane pane = (EPane) n;
      var polys = pane switch {
         EPane.Union => Union (mPolys),
         EPane.Intersection => Intersect (mPolys),
         EPane.Subtraction => Subtract (mPolys),
         _ => mPolys.SelectMany (a => a).ToList ()
      };
      return new XfmVN (Matrix3.Translation ((Vector3)GetOffset ((EPane)n, ViewBound)), new PolyVN (polys, pane));

      static List<Poly> Union (List<List<Poly>> polys) => polys.SelectMany (x => x).ToList ().AsSpan ().Union ();
      static List<Poly> Intersect (List<List<Poly>> polys) => polys.SelectMany (a => a.AsSpan ().Intersect ()).ToList ();
      static List<Poly> Subtract (List<List<Poly>> polys) => polys.SelectMany (a => a.AsSpan ()[..1].Subtract (a.AsSpan ()[1..])).ToList ();
   }

   public override void Draw () {
      var vec = GetOffset (EPane.None, ViewBound);
      var bound = ViewBound;
      var mid = bound.Midpoint;
      // Draw quadrant lines to divide the screen in four sections.
      Lux.Lines ([new Vec2F (mid.X, bound.Y.Min - vec.Y), new (mid.X, bound.Y.Max + vec.Y),
         new (bound.X.Min - vec.X, mid.Y), new (bound.X.Max + vec.X, mid.Y)]);

      Lux.Text2D ("Polys", new (mid.X - 10, mid.Y - 10), ETextAlign.TopRight, Vec2S.Zero);
      Lux.Text2D ("Union", new (mid.X + 10, mid.Y - 10), ETextAlign.TopLeft, Vec2S.Zero);
      Lux.Text2D ("Intersection", new (mid.X - 10, mid.Y + 10), ETextAlign.BotRight, Vec2S.Zero);
      Lux.Text2D ("Subtraction", new (mid.X + 10, mid.Y + 10), ETextAlign.BotLeft, Vec2S.Zero);
   }

   static Vector2 GetOffset (EPane pane, Bound2 bound) {
      double dx = bound.Width * 0.02, dy = bound.Height * 0.02;
      var (sx, sy) = (Lux.Viewport.X / bound.Width, Lux.Viewport.Y / bound.Height);
      if (sy < sx) {
         dx += (Lux.Viewport.X / sy - bound.Width) / 2;
      } else {
         dy += (Lux.Viewport.Y / sx - bound.Height) / 2;
      }
      Vector2 vec = new (dx, dy);
      if (pane >= 0) {
         (dx, dy) = vec / 2;
         (dx, dy) = pane switch {
            EPane.Union => (dx, -dy),
            EPane.Intersection => (-dx, dy),
            EPane.Subtraction => (dx, dy),
            EPane.Polys => (-dx, -dy),
            _ => throw new NotImplementedException (),
         };
         int n = (int)pane;
         (sx, sy) = ((n & 1) > 0 ? 1.0 : 0, (n & 2) > 0 ? 1.0 : 0);
         vec = new Vector2 (dx + sx * bound.Width / 2, dy + sy * bound.Height / 2);
      }
      return vec;
   }

   class PolyVN (List<Poly> polys, EPane pane) : VNode {
      readonly EPane mPane = pane;

      public override void SetAttributes () {
         Lux.LineWidth = 2f;
         Lux.Color = mPane > 0 ? new (96, 96, 192) : Color4.Gray (96);
         Lux.LineType = ELineType.Continuous;
      }

      public override void Draw () => Lux.Polys (mPolys.AsSpan ());

      public override VNode? GetChild (int n) {
         if (mPane >= EPane.Union && n == 0)
            return new FillVN (mPolys);
         return null;
      }

      readonly List<Poly> mPolys = polys;
   }

   class FillVN (List<Poly> polys) : VNode {
      public override void SetAttributes () {
         Lux.Color = new Color4 (255, 255, 192);
         Lux.ZLevel = -1;
      }

      public override void Draw () {
         List<Point2> pts = []; List<int> indices = [];
         Bound2 bound = new (mPolys.Select (x => x.GetBound ()));
         List<Vec2F> path = [bound.Midpoint];
         mPolys.ForEach (x => {
            pts.Clear ();
            x.Discretize (pts, 0.1);
            var idx0 = path.Count;
            indices.Add (0);
            path.AddRange (pts.Select (p => (Vec2F)p));
            for (int i = 0; i < pts.Count; i++) indices.Add (idx0 + i);
            indices.Add (idx0); indices.Add (-1);
         });
         Lux.FillPath (path.AsSpan (), indices.AsSpan (), bound);
      }

      readonly List<Poly> mPolys = polys;
   }
}
