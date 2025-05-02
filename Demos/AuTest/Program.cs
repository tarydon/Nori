using System.Diagnostics;
using System.Reflection;
using Nori;
namespace AuTest;

class Program {
   static void Main (string[] args) {
      Lib.Init ();
      Lib.AddAssembly (Assembly.GetExecutingAssembly ());
      Lib.AddNamespace ("Flux");
      Lib.AddMetadata (File.ReadAllLines ("N:/Demos/AuTest/metadata.txt"));
      Lib.Tracer = Console.Write;

      for (int i = 0; i < 10; i++) {
         int n = 0;
         var lines = File.ReadAllLines ("N:/Demos/AuTest/bmlist.txt");
         using (var bt = new BlockTimer (100064, "Timing")) {
            for (int t = 0; t < 59; t++) {
               foreach (var line in lines) {
                  var obj = AuReader.Load ($"X:/Data/Archive/Machines/{line}/machine.curl");
                  n++;
               }
            }
         }
         Lib.Trace ($"Loaded {n} machines\n\n");
      }
   }
}
