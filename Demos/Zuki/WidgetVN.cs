using Nori;
namespace Zuki;

class CursorVN : VNode {
   public CursorVN () { Streaming = true; }

   public Point2 Pos {
   }

   public Point2 mPt = new Point2 (1e6, 1e6);
}