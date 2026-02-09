// ────── ╔╗
// ╔═╦╦═╦╦╬╣ ConvexHullDemo.cs
// ║║║║╬║╔╣║ Demonstrates the convex-hull computation algorithm
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;

namespace WPFDemo;

class ConvexHullScene : Scene2 {
   public ConvexHullScene () {
      BgrdColor = Color4.Gray (40);
      Bound = new (-700, -500, 700, 500);
      Root = new GroupVN ([new HullNode (Bound.InflatedF (0.8)), TraceVN.It]);
      Lib.Tracer = TraceVN.Print;
   }
}

class HullNode : VNode {
   public HullNode (Bound2 bound) => mBound = bound;
   Bound2 mBound;

   public override void OnAttach () {
      DisposeOnDetach (HW.MouseMoves.Subscribe (OnMouse));
      DisposeOnDetach (HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (OnClick));
      Lib.Trace ("Move mouse about to modify point set");
      Lib.Trace ("Click to add random points");
      AddRandom (10 * mMax);
   }

   public override void SetAttributes ()
      => (Lux.PointSize, Lux.LineWidth) = (6, 1.5f);

   public override void Draw () {
      var pts = mPts.Select (a => (Vec2F)a).ToList ();
      Lux.Points (pts.AsSpan ());
      var hull = ConvexHull.Compute (mPts).ToList ();
      Lux.LineLoop (hull);
   }

   void OnMouse (Vec2S pix) {
      Point2 pt = (Point2)Lux.PixelToWorld (pix);
      if (mLast.DistTo (pt) < 10) return;
      mPts.Add (mLast = pt);
      for (int i = 0; i < 10; i++)
         if (mPts.Count > mMax) mPts.RemoveAt (0);
      Redraw (); 
   }
   Point2 mLast = Point2.Zero;

   void OnClick (MouseClickInfo mi) {
      AddRandom (5 * mMax);
      Redraw ();
   }

   void AddRandom (int c) {
      for (int i = 0; i < c; i++) {
         double x = mR.NextDouble () * mBound.X.Length + mBound.X.Min;
         double y = mR.NextDouble () * mBound.Y.Length + mBound.Y.Min;
         mPts.Add (new (x, y));
      }
      using var bt = new BlockTimer ($"Hull of {mPts.Count} pts");
      var hull = ConvexHull.Compute (mPts);
   }

   List<Point2> mPts = [];
   Random mR = new ();
   int mMax = 300;
}
