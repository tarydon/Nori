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
   public Tester () => Lib.Init ();

   [Benchmark (Baseline = true)]
   public void DXFRead () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         var _ = DXFReader.Load (file);
      }
   }

   int MAX = 1000;
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
   }
}
