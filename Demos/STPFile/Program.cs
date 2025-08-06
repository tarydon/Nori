using Nori;

internal class Program {
   static void Main (string[] args) {
      Lib.Init ();
      var step = new STEPReader ("c:/step/C9386.stp");
      step.Parse ();
   }
}
