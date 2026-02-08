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
      Random r = new Random (1);
      var model = new T3XReader ("C:/Etc/T3/5X-004.t3x").Load ();
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         Point3f[] set = [.. ent.Mesh.Vertex.Select (a => a.Pos)];
         if (set.Length > 5) {
            mSets.Add (set);
            for (int i = 0; i < 5; i++) {
               double xRot = Lib.PI * r.NextDouble (), yRot = Lib.PI * r.NextDouble (), zRot = Lib.PI * r.NextDouble ();
               var xfm = Matrix3.Rotation (EAxis.X, xRot) * Matrix3.Rotation (EAxis.Y, yRot) * Matrix3.Rotation (EAxis.X, zRot);
               mSets.Add ([.. set.Select (a => a * xfm)]);
            }
         }
      }
   }
   List<Point3f[]> mSets = [];

   [Benchmark (Baseline = true)]
   public void Dito () {
      foreach (var set in mSets) OBB.Build (set);
   }

   [Benchmark]
   public void PCA () {
      foreach (var set in mSets) OBB.BuildFast (set);
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
   }
}
