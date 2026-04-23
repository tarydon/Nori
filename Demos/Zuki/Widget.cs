using Nori;
namespace Zuki;

class Widget : IDisposable {
   public Widget () {
      mDwg = Hub.Dwg;
      mDisp.Add (Hub.MouseMoves.Subscribe (OnMouseMove));
      mDisp.Add (Hub.LeftClicks.Subscribe (OnLeftClick));
   }
   protected Dwg2 mDwg;

   public virtual void Draw () { }
   public virtual void OnMouseMove (Point2 pt) {
      Pts[^1] = pt; WidgetVN.It.Redraw ();
   }
   public virtual void OnLeftClick (Point2 pt) {
      Pts.Add (pt); WidgetVN.It.Redraw ();
   }

   public int Phase => Pts.Count;

   public void Dispose () => mDisp.Dispose ();
   MultiDispose mDisp = new ();

   protected List<Point2> Pts = [Point2.Nil];
}

class Dim3PAngularMaker : Widget {
   public Dim3PAngularMaker () => mDimStyle = mDwg.CurrentDimStyle;
   readonly DimStyle2 mDimStyle;

   public override void Draw () {
      Lux.Color = Color4.DarkGreen;
      Lux.LineWidth = 1.25f;
      Lux.Points (Pts.Select (a => (Vec2F)a).ToArray ());

      if (Phase == 4) {
         var dim = new E2Dim3PAngular (mDwg.CurrentLayer, mDimStyle, Pts, "");
         Hub.DrawEnts (dim.Ents);
      } else {
         if (Phase > 1) Lux.Lines ([Pts[0], Pts[1]]);
         if (Phase > 2) Lux.Lines ([Pts[0], Pts[2]]);
      }
   }
}
