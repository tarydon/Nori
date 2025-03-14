// ────── ╔╗
// ╔═╦╦═╦╦╬╣ World.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
using Nori;
namespace WPFDemo;

#region class Road ---------------------------------------------------------------------------------
// Road is the root of our model hierarchy
class Road {
   // A Road has an X-Span, and lies at Y = 0 (rendered as a dotted line on screen)
   public Bound1 Span = new (0, 100);

   // A Road has a list of buses on it
   public List<Bus> Buses = [
      new Bus (new (10, 0), new (10, 5), Color4.Blue),
      new Bus (new (25, 0), new (14, 6), new (0x008000)),
   ];
}
#endregion

#region class Bus ----------------------------------------------------------------------------------
// Bus is the next level of the hierarchy - a road has multiple buses on it
public class Bus : IObservable<EProp> {
   public Bus (Point2 pos, Vector2 size, Color4 color)
      => (mPos, mSize, mColor) = (pos, size, color);

   // Bus position (bottom left corner of the bus bounding rectangle)
   public Point2 Pos {
      get => mPos;
      set { if (Lib.Set (ref mPos, value)) Notify (EProp.Xfm); }
   }
   Point2 mPos;

   // Size of the bus body
   public Vector2 Size {
      get => mSize;
      set { if (Lib.Set (ref mSize, value)) Notify (EProp.Geometry); }
   }
   Vector2 mSize;

   // Color of the bus
   public Color4 Color {
      get => mColor;
      set { if (Lib.Set (ref mColor, value)) Notify (EProp.Attributes); }
   }
   Color4 mColor;

   public IDisposable Subscribe (IObserver<EProp> observer) => (mSubject ??= new ()).Subscribe (observer);
   void Notify (EProp prop) => mSubject?.OnNext (prop);
   Subject<EProp>? mSubject;
}
#endregion
