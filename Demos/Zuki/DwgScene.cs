using Nori;
namespace Zuki;

class DwgScene : Scene2 {
   public DwgScene (Dwg2 dwg) {
      mDwg = dwg;
      List<VNode> nodes = [new Dwg2VN (mDwg), TraceVN.It, CursorVN.It, WidgetVN.It];
      if (Hub.FillDrawing) nodes.Add (new DwgFillVN (mDwg));
      Root = Hub.Root = new GroupVN (nodes);
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      WorldDecimals = 0;
   }

   public override bool CursorVisible => false;

   readonly Dwg2 mDwg;
}
