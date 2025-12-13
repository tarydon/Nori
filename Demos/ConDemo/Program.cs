namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.Write;
   }
}
