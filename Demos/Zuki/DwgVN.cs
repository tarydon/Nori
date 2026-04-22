using Nori;
namespace Zuki;

[Singleton]
partial class CursorVN : VNode {
   public CursorVN () => Streaming = true;
   public Point2 Pt { get; set { field = value; Redraw (); } }

   public override void Draw () {
      double a = Hub.PixelScale * 20;
      (Lux.Color, Lux.LineWidth) = (Color4.Gray (96), 1.25f);
      Lux.Lines ([Pt.Moved (-a, 0), Pt.Moved (a, 0), Pt.Moved (0, -a), Pt.Moved (0, a)]);
   }
}

[Singleton]
partial class WidgetVN : VNode {
   public WidgetVN () => Streaming = true;

   public override void Draw () => Hub.Widget?.Draw ();
}