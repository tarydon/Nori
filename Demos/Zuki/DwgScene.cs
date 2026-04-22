using Nori;
namespace Zuki;

class DwgScene : Scene2 {
   public DwgScene (Dwg2 dwg) {
      mDwg = dwg;
      VNode[] nodes = [new Dwg2VN (mDwg), new DwgFillVN (mDwg), TraceVN.It];
      Root = Hub.Root = new GroupVN (nodes);
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
   }

   readonly Dwg2 mDwg;
}
