namespace WPFDemo;
using Nori;

class STPScene : Scene3 {
   public STPScene () {
      var sr = new STEPReader ("N:/TData/STEP/Boot.step");
      sr.Parse ();
      var model = sr.Build ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = model.Bound;
      Root = new GroupVN ([new Model3VN (model), TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
   }
}
