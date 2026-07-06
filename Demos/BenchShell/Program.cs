// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;
namespace NBench;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Lib.Tessellate = FastTess2D.Process;
   }

   [Benchmark]
   public void DoIt () {
      foreach (var file in Directory.GetFiles ("W:\\NoriSample\\GOOD", "*.stp"))
         Process (file);
   }

   void Process (string file) {
      var model = STEPReader.Load (file);
      var shmodel = new SheetMetalizer (model).Process ().Value;
      var dwg = new Unfolder (shmodel).Process ().Value;
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
   }
}
