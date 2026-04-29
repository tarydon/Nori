using Nori;
namespace Zuki;

#region class DwgScene -----------------------------------------------------------------------------
/// <summary>Implements the drawing scene</summary>
/// This is trivial, since most of the heavy lifting is done by the Hub class
class DwgScene : Scene2 {
   // Constructor --------------------------------------------------------------
   public DwgScene (Dwg2 dwg) {
      mDwg = dwg;
      List<VNode> nodes = [new Dwg2VN (mDwg), TraceVN.It, CursorVN.It, WidgetVN.It];
      if (Hub.FillDrawing) nodes.Add (new DwgFillVN (mDwg));
      Root = Hub.Root = new GroupVN (nodes);
      Bound = dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      WorldDecimals = 0;
   }

   // Overrides ----------------------------------------------------------------
   public override bool CursorVisible => false;

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;
}
#endregion
