// ────── ╔╗
// ╔═╦╦═╦╦╬╣ World.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace WPFDemo;

#region class Road ---------------------------------------------------------------------------------
// Road is the root of our model hierarchy
class Road {
   public Road () {
      for (int i = 0; i < 10; i++) {
         int width = mRand.Next (10, 20), height = mRand.Next (5, 10);
         double x = mRand.Next ((int)Span.Max - width), y = mRand.Next (80);
         double dx = mRand.NextDouble () * 10 - 5;
         Buses.Add (new Bus (new (x, y), new (width, height), Color4.Random, dx));
      }
   }

   // A Road has an X-Span, and lies at Y = 0 (rendered as a dotted line on screen)
   public Bound1 Span = new (0, 200);

   // A Road has a list of buses on it
   public AList<Bus> Buses = [];

   public void Tick (double f) {
      Buses.ForEach (a => a.Tick (f));
   }

   readonly Random mRand = new ();
}
#endregion

#region class Bus ----------------------------------------------------------------------------------
// Bus is the next level of the hierarchy - a road has multiple buses on it
[EPropClass]
public partial class Bus : IObservable<EProp> {
   public Bus (Point2 pos, Vector2 size, Color4 color, double dx)
      => (mPos, mSize, mColor, mDX) = (pos, size, color, dx);

   public void Tick (double f) => Pos = Pos.Moved (f * mDX, 0);
   double mDX;

   // Bus position (bottom left corner of the bus bounding rectangle)
   [EPropField (EProp.Xfm)] Point2 mPos;
   // Size of the bus body
   [EPropField (EProp.Geometry)] Vector2 mSize;
   // Color of the bus
   [EPropField (EProp.Attributes)] Color4 mColor;
}
#endregion
