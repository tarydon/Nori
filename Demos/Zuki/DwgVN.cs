using Nori;
namespace Zuki;

[Singleton]
partial class CursorVN : VNode {
   public CursorVN () {
      Streaming = true;
      mFace = new (TypeFace.Default, (int)(7 * Lux.DPIScale + 0.5));
   }
   TypeFace mFace;

   public Point2 Pt { get; set { field = value; Redraw (); } }

   public override void Draw () {
      double a = Hub.PixelScale * 20;
      (Lux.Color, Lux.LineWidth, Lux.TypeFace) = (Color4.Gray (96), 1.25f, mFace);
      Lux.Lines ([Pt.Moved (-a, 0), Pt.Moved (a, 0), Pt.Moved (0, -a), Pt.Moved (0, a)]);

      int d = Hub.PixelScale switch { > 1 => 0, > 0.1 => 1, > 0.01 => 2, > 0.001 => 3, _ => 4 };
      string fmt = $"F{0}";
      string text = $"{Pt.X.ToString (fmt)}, {Pt.Y.ToString (fmt)}";
      Lux.Text2D (text, (Vec2F)Pt, ETextAlign.BaseLeft, new Vec2S (10, 10));
   }
}

[Singleton]
partial class WidgetVN : VNode {
   public WidgetVN () => Streaming = true;

   public override void Draw () => Hub.Widget?.Draw ();
}
