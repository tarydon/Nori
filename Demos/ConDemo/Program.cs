namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.Write;
      foreach (var file in Directory.GetFiles ("c:\\etc\\t3", "*.t3x")) {
         Console.WriteLine (file);
         try {
            new T3XReader (file).Load ();
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine (e);
            Console.ResetColor ();
         }
      }
   }
}
 