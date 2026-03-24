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
   public void TextRead () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         string s = File.ReadAllText (file);
      }
   }

   [Benchmark]
   public void ByteRead () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         byte[] data = File.ReadAllBytes (file);
      }
   }

   [Benchmark]
   public void LibRead () {
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf").Take (MAX)) {
         byte[] data = Lib.ReadBytes (file);
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
