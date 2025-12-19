using Nori;
namespace SurfLab;

class SurfScene : Scene3 {
   public SurfScene (string file) {
      mModel = new T3XReader (file).Load ();
      mModel.Ents.RemoveIf (a => a.Id != 602);

      BgrdColor = new (96, 128, 160);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It]);

      mHooks = HW.MouseMoves.Subscribe (OnMouseMove);
   }
   Model3 mModel;
   IDisposable mHooks;

   public override void Detached () => mHooks.Dispose ();

   public override void Picked (object obj)
      => Lib.Trace ($"Picked {obj}");

   // Called when the mouse is moving
   void OnMouseMove (Vec2S pt) {
      if (!HW.IsDragging) {
         var obj = Lux.Pick (pt);
         if (obj != null) Lib.Trace ($"{pt} {obj}");
      }
   }
}
