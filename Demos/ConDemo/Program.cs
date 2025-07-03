namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      var mech = Mechanism.Load ("N:/Wad/FanucX/mechanism.curl");
      Console.WriteLine (mech.Bound);
   }
}