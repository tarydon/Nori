namespace WPFDemo;
using Nori;

class RobotScene : Scene3 {
   public RobotScene () {
      mMech = Mechanism.Load ("N:/Wad/FanucX/mechanism.curl");
      BgrdColor = Color4.Gray (96);
      Bound = mMech.Bound;
      Root = new MechanismVN (mMech);
   }
   Mechanism mMech;
}
