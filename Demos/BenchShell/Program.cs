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
   }

   [Benchmark (Baseline = true)]
   public void OldDXF () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         var dwg = Nori.Old.DXFReader.Load (file);
      }
   }

   [Benchmark]
   public void NewDXF () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         var dwg = Nori.DXFReader.Load (file);
      }
   }

   int MAX = 10000;
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
      //var t = new Tester ();
      //t.LibRead ();
   }
}
