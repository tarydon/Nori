// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Text;
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
      mBound = model.Bound;
   }
   Mesh3[] mMeshes;
   Bound3 mBound;

   [Benchmark]
   public void MeshSlicer () {
      int step = 1;
      List<Polyline3> output = [];
      var pmi = new MeshSlicer ([.. mMeshes]);
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (mBound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         output.Clear (); pmi.Compute (pdef, output);
         if (output.Count != 1) throw new InvalidOperationException ();

         double y = (i / 100.0).Along (mBound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         output.Clear (); pmi.Compute (pdef, output);
         if (output.Count != 1) throw new InvalidOperationException ();
      }
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
   }
}
