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
   public void OldDXF () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         var _ = DXFReader.Load (file);
      }
   }

   [Benchmark]
   public void NewDXF () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         var dr = new Nori.Alt.DXFReader (file);
         var _ = dr.Load (); 
      }
   }

   int MAX = 1000;
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
      //var t = new Tester ();
      //t.LibRead ();
   }
}
