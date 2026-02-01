// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MinSphereScene.cs
// ║║║║╬║╔╣║ Demonstrates the convex-hull computation algorithm
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;

using Nori;
using System.Diagnostics;
using System.Windows;

class ConvexHullScene : Scene2 {
   public ConvexHullScene () {
      BgrdColor = Color4.Gray (40);
      Bound = new (-500, -500, 500, 500);
      Lib.Tracer = TraceVN.Print; Random R = new ();
      Build ([.. GeneratePoints (R, 2000, Bound.Width)]);
   }

   // Build the scene with the points and computed hulls
   void Build (Point2[] pts) {
      TraceVN.It.Clear ();
      Stopwatch sw = Stopwatch.StartNew ();
      List<Point2> hullG = ConvexHull.ComputeByGrahamScan (pts).ToList ();
      sw.Stop ();
      Lib.Trace ($"Convex Hull by Graham Scan: {hullG.Count} pts in {sw.Elapsed.TotalMicroseconds:F3} us");
      sw.Restart ();
      List <Point2> hullA = ConvexHull.Compute (pts);
      sw.Stop ();
      Lib.Trace ($"Convex Hull by Andrew monotone chain: {hullA.Count} pts in {sw.Elapsed.TotalMicroseconds:F3} us");
      Root = new GroupVN ([TraceVN.It, new PointsVN (pts, (Color4.RandomLight, 6)), new ConvexHullVN (hullG), new ConvexHullVN (hullA), new DrawPolyVN ()]);
   }
   readonly static (Color4 Clr, float Size)[] Styles = [(Color4.Green, 8), (Color4.White, 3), (Color4.Red, 8)];

   // Generate random points within a randomly rotated cuboid of maximum size 'size'.
   static IEnumerable<Point2> GeneratePoints (Random R, int count, double size) {
      size *= 0.9; double halfSize = size / 2.0;
      Bound2 bound = new (-halfSize, -halfSize, halfSize, halfSize);
      Matrix2 xfm = Matrix2.Rotation (R.NextDouble () * Math.PI); // Apply rotation
      int i = 0;
      do {
         var pt = P () * size;
         if (!bound.Contains (pt)) continue;
         i++;
         yield return pt * xfm;
      } while (i < count);
      // Helpers
      Point2 P () => new (R.NextDouble () - 0.5, R.NextDouble () - 0.5);
   }

   static string S (TimeSpan ts) => $"{ts.TotalMicroseconds:F0} us";

   class ConvexHullVN (List<Point2> hull) : VNode {
      readonly List<Point2> Hull = hull;

      public override void SetAttributes () => (Lux.Color, Lux.LineWidth) = (Color4.White, 1);

      public override void Draw () => Lux.LineStrip ([.. Hull, Hull[0]]);
   }

   // Draw points cloud
   class PointsVN (IEnumerable<Point2> pts, (Color4 Clr, float Size) style) : VNode {
      public override void SetAttributes () => (Lux.Color, Lux.PointSize) = Style;
      public override void Draw () => Lux.Points (Pts);
      readonly Vec2F[] Pts = [.. pts.Select (p => (Vec2F)p)];
      readonly (Color4, float) Style = style;
   }

   class DrawPolyVN : VNode {
      List<Point2> mPts = [];
      List<Point2>? mHull = null;

      public override void SetAttributes () => (Lux.Color, Lux.LineWidth) = (Color4.White, 1);
      public override void Draw () {
         if (mHull != null) {
            Lux.LineStrip ([.. mPts, mPts[0]]);
            Lux.Color = Color4.Yellow;
            Lux.LineStrip ([.. mHull, mHull[0]]);
         } else if (mPts.Count > 1)
            Lux.LineStrip (mPts);
      }
      public override void OnAttach () => mDisp = HW.MouseClicks.Subscribe (OnMouse);

      public override void OnDetach () => mDisp?.Dispose ();

      void OnMouse (MouseClickInfo mi) {
         if (mi.IsRelease || mi.Button == EMouseButton.Middle) return;

         if (mHull != null) {
            mHull = null;
            mPts.Clear ();
         }

         Point2 pt = (Point2)Lux.PixelToWorld (mi.Position);
         if (mi.Button == EMouseButton.Left)
            mPts.Add (pt);
         else if (mPts.Count > 2)
            mHull = ConvexHull.ComputeForSimplePolygon (mPts);
         Redraw ();
      }

      IDisposable? mDisp;
   }
}