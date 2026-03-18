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
      var model = new T3XReader ("N:/Demos/Data/5X-024-Blank.t3x").Load ();
      mMeshes = [.. model.Ents.OfType<E3Surface> ().Select (a => a.Mesh)];
   }
   List<Mesh3> mMeshes;

   [Benchmark (Baseline = true)]
   public void OldMesh () {
      mOldTrees.Clear ();
      for (int k = 0; k < Iter; k++)
         foreach (var mesh in mMeshes)
            mOldTrees.Add (new Nori.OBBTree (mesh));
   }
   List<OBBTree> mOldTrees = [];

   [Benchmark]
   public void NewMesh () {
      mNewTrees.Clear ();
      for (int k = 0; k < Iter; k++)
         foreach (var mesh in mMeshes)
            mNewTrees.Add (Nori.Alt.OBBTree.From (mesh));
   }
   List<Nori.Alt.OBBTree> mNewTrees = [];

   const int Iter = 30;
}

static class Program {
   public static void Main () {
      //BenchmarkRunner.Run<Tester> ();
      var t = new Tester ();
      //t.OldMesh ();
      //Console.WriteLine ("");
      //Console.WriteLine ("");
      t.NewMesh ();
   }
}
