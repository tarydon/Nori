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

      int n = 0;
      Directory.CreateDirectory ("c:/etc/BMDump");
      var lines = File.ReadAllLines ("N:/Demos/AuTest/bmlist.txt");
      using (var bt = new BlockTimer (lines.Length, "Timing")) {
         foreach (var line in lines) {
            var obj = CurlReader.Load ($"X:/Data/Archive/Machines/{line}/machine.curl");
            CurlWriter.Save (obj, $"c:/etc/BMDump/{line}.curl");
            n++;
         }
      }
      Lib.Trace ($"Loaded {n} machines\n\n");
   }
}
