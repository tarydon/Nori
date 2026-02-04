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
         if (set.Length > 0) {
            mSets.Add (set);
            mSets2.Add ([.. set.Select (a => (Point3)a)]);

            for (int i = 0; i < 5; i++) {
               double xRot = Lib.PI * r.NextDouble (), yRot = Lib.PI * r.NextDouble (), zRot = Lib.PI * r.NextDouble ();
               var xfm = Matrix3.Rotation (EAxis.X, xRot) * Matrix3.Rotation (EAxis.Y, yRot) * Matrix3.Rotation (EAxis.X, zRot);
               mSets.Add ([..set.Select (a => a * xfm)]);
               mSets2.Add ([.. set.Select (a => (Point3)(a * xfm))]);
            }
         }
      }
   }
   List<Point3f[]> mSets = [];
   List<Point3[]> mSets2 = [];

   [Benchmark]
   public void TestOBBDito () {
      foreach (var set in mSets2) 
         OBB.From (set);
   }

   [Benchmark (Baseline = true)]
   public void TestOBBPCA () {
      foreach (var set in mSets)
         OBB2.FromPCA (set);
   }

   [Benchmark]
   public void TestOBBHull13 () {
      foreach (var set in mSets)
         OBB2.FromHull13 (set);
   }

   [Benchmark]
   public void TestOBBHull6 () {
      foreach (var set in mSets)
         OBB2.FromHull6 (set);
   }

   [Benchmark]
   public void TestOBBHull7 () {
      foreach (var set in mSets)
         OBB2.FromHull7 (set);
   }

   public void Compare () {
      double a1Total = 0, a2Total = 0, a3Total = 0, a4Total = 0, a5Total = 0, a6Total = 0;
      for (int i = 0; i < mSets.Count; i++) {
         var obb1 = OBB.From (mSets2[i]);
         var obb2 = OBB2.FromPCA (mSets[i]);
         var obb3 = OBB2.FromHull6 (mSets[i]);
         var obb4 = OBB2.FromHull13 (mSets[i]);
         var obb5 = OBB2.FromHull10 (mSets[i]);
         var obb6 = OBB2.FromHull7 (mSets[i]);
         double a1 = obb1.Area, a2 = obb2.Area, a3 = obb3.Area, a4 = obb4.Area, a5 = obb5.Area, a6 = obb6.Area;
         a1Total += a1; a2Total += a2; a3Total += a3; a4Total += a4; a5Total += a5; a6Total += a6;
      }
      Console.WriteLine ("Tightness (smaller is better)");
      Console.WriteLine ($"Dito   : {Math.Round (a1Total / a1Total, 3)}");
      Console.WriteLine ($"PCA    : {Math.Round (a2Total / a1Total, 3)}");
      Console.WriteLine ($"Hull13 : {Math.Round (a4Total / a1Total, 3)}");
      Console.WriteLine ($"Hull10 : {Math.Round (a5Total / a1Total, 3)}");
      Console.WriteLine ($"Hull6  : {Math.Round (a3Total / a1Total, 3)}");
      Console.WriteLine ($"Hull7  : {Math.Round (a6Total / a1Total, 3)}");
   }
}

static class Program {
   public static void Main () {
      // BenchmarkRunner.Run<Tester> ();
      new Tester ().Compare ();
   }
}
