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

//[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      Random r = new Random (1);
      var model = new T3XReader ("C:/Etc/T3/5X-004.t3x").Load ();
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         Point3f[] set = [.. ent.Mesh.Vertex.Select (a => a.Pos)];
         if (set.Length > 5) {
            mSets.Add (set);
            mMassive.AddRange (set);

            for (int i = 0; i < 5; i++) {
               double xRot = Lib.PI * r.NextDouble (), yRot = Lib.PI * r.NextDouble (), zRot = Lib.PI * r.NextDouble ();
               var xfm = Matrix3.Rotation (EAxis.X, xRot) * Matrix3.Rotation (EAxis.Y, yRot) * Matrix3.Rotation (EAxis.X, zRot);
               mSets.Add ([.. set.Select (a => a * xfm)]);
               mMassive.AddRange (set.Select (a => a * xfm));
            }
         }
      }
   }
   List<Point3f[]> mSets = [];
   List<Point3f> mMassive = [];

   [Benchmark (Baseline = true)]
   public void Dito () {
      foreach (var set in mSets)
         OBB.Build (set);
   }

   [Benchmark]
   public void PCA () {
      foreach (var set in mSets)
         OBB.BuildFast (set);
   }

   //[Benchmark]
   //public void TestOBBDitoMassive () {
   //   OBB.From (mMassive.AsSpan ());
   //}

   //[Benchmark (Baseline = true)]
   //public void TestOBBPCAMassive () {
   //   OBB.FromPCA (mMassive.AsSpan ());
   //}

   public void Compare () {
      double a1Total = 0, a2Total = 0;
      for (int i = 0; i < mSets.Count; i++) {
         var obb1 = OBB.Build (mSets[i]);
         var obb2 = OBB.BuildFast (mSets[i]);
         double a1 = obb1.Area, a2 = obb2.Area;
         a1Total += a1;  a2Total += a2; 
      }
      int average = mSets.Sum (a => a.Length) / mSets.Count;
      Console.WriteLine ($"Average: {average} pts");
      Console.WriteLine ("Tightness (smaller is better)");
      Console.WriteLine ($"Dito   : {Math.Round (a1Total / a1Total, 5)}");
      Console.WriteLine ($"PCA    : {Math.Round (a2Total / a1Total, 5)}");
   }

   public void CompareMassive () {
      double a1Total = OBB.Build (mMassive.AsSpan ()).Area;
      double a2Total = OBB.BuildFast (mMassive.AsSpan ()).Area;
      Console.WriteLine ();
      Console.WriteLine ($"Average: {mMassive.Count} pts");
      Console.WriteLine ("Tightness (smaller is better)");
      Console.WriteLine ($"Dito   : {Math.Round (a1Total / a1Total, 5)}");
      Console.WriteLine ($"PCA    : {Math.Round (a2Total / a1Total, 5)}");
   }
}

static class Program {
   public static void Main () {
      // BenchmarkRunner.Run<Tester> ();
      new Tester ().Compare ();
      new Tester ().CompareMassive ();
   }
}
