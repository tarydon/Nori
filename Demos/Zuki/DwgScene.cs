using Nori;
namespace Zuki;

class DwgScene : Scene2 {
   public DwgScene (Dwg2 dwg) {
      mDwg = dwg;
      VNode[] nodes = [new Dwg2VN (mDwg), new DwgFillVN (mDwg), TraceVN.It,
                       CursorVN.It, WidgetVN.It];
      Root = Hub.Root = new GroupVN (nodes);
      var b = dwg.Bound.InflatedF (1.1);
      Bound = new (b.X.Min, b.Y.Min, 350, b.Y.Max);
      BgrdColor = Color4.Gray (216);
   }

   readonly Dwg2 mDwg;
}
