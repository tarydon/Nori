// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ STPScene.cs
// ║║║║╬║╔╣║ Load and display a STEP file, select entities, connected entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using System.Reactive.Linq;
using Nori;

class STPScene : Scene3 {
   public STPScene () {
      var sr = new STEPReader ("N:/TData/Step/S00178.stp");
      sr.Parse ();
      mModel = sr.Build ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
   }
   Model3 mModel;

   public override void Picked (object obj) {
      if (!HW.IsShiftDown)
         mModel.Ents.ForEach (a => a.IsSelected = false);
      if (obj is E3Surface ent) {
         Lib.Trace ($"Picked: {ent.GetType ().Name} #{ent.Id}");
         ent.IsSelected = true;
         if (HW.IsCtrlDown)
            foreach (var ent2 in mModel.GetNeighbors (ent)) ent2.IsSelected = true;
      }
   }
}
