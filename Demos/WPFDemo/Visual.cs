// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Visual.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace WPFDemo;

#region class RoadVN -------------------------------------------------------------------------------
// Draws a Road as a dotted line
class RoadVN : VNode {
   public RoadVN (Road road) : base (road) => mRoad = road;
   readonly Road mRoad;

   public override void SetAttributes () {
      Lux.LineWidth = 6f;
      Lux.LineType = ELineType.DashDotDot;
      Lux.Color = new Color4 (128, 0, 0);
   }

   public override void Draw () {
      Lux.Lines ([new (mRoad.Span.Min, 0), new (mRoad.Span.Max, 0)]);
   }

   public override VNode? GetChild (int n) {
      if (n < mRoad.Buses.Count) return new BusVN (mRoad.Buses[n]);
      return null;
   }
}
#endregion

#region class BusVN --------------------------------------------------------------------------------
// Draws a Bus body (and has children to draw the windows and wheels)
class BusVN : VNode {
   public BusVN (Bus bus) => mBus = bus;
   Bus mBus;

   public override void SetAttributes () {
      Lux.Xfm = Matrix3.Translation (mBus.Pos.X, mBus.Pos.Y, 0);
      Lux.Color = mBus.Color;
   }

   public override void Draw () {
      // Bus base is 1 unit off the ground
      Lux.Poly (Poly.Rectangle (0, 1, mBus.Size.X, mBus.Size.Y));
   }

   public override VNode? GetChild (int n) {
      return n switch {
         0 => MakeWheel ("Left", 0, 1),
         1 => MakeWheel ("Right", mBus.Size.X, 1),
         2 => new WindowVN (mBus.Size),
         _ => null
      };

      static VNode MakeWheel (string name, double x, double y)
         => new XfmVN (name, Matrix3.Translation (x, y, 0), WheelVN.It);
   }
}
#endregion

#region class WindowVN -----------------------------------------------------------------------------
// Draws a Bus Window, inheriting the color and Xfm from the Bus itself
class WindowVN : VNode {
   public WindowVN (Vector2 busSize) => mSize = busSize;
   Vector2 mSize;

   public override void Draw () {
      Bound2 rect = new (0.5, mSize.Y / 2, mSize.X - 0.5, mSize.Y - 0.5);
      Lux.Poly (Poly.Rectangle (rect));
      for (int i = 1; i < 5; i++) {
         double x = (i / 5.0).Along (rect.X.Min, rect.X.Max);
         Lux.Lines ([new (x, rect.Y.Min), new (x, rect.Y.Max)]);
      }
   }
}
#endregion

#region class WheelVN ------------------------------------------------------------------------------
// Every wheel is the same so we have a singleton VN that will participate multiple times
// in the scene-graph. It will inherit just the Xfm from the parent.
[Singleton]
partial class WheelVN : VNode {
   public override void SetAttributes () 
      => Lux.Color = Color4.Black;

   public override void Draw () {
      Lux.Poly (Poly.Circle (Point2.Zero, 1));
      Lux.Poly (Poly.Circle (Point2.Zero, 0.7));
   }
}
#endregion
