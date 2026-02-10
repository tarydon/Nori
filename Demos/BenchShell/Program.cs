// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;

namespace NBench;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      var lines = File.ReadAllLines ("C:/Dropbox/Nori/TriTri.txt");
      mXfm = Matrix3.Translation (1, 2, 3) * Matrix3.Rotation (EAxis.X, 45.D2R ()) * Matrix3.Rotation (EAxis.Y, 30.D2R ());
      mXfm = Matrix3.Identity;
      mXfmBack = mXfm.GetInverse ();

      List<Point3f> P = [], P2 = [];
      for (int i = 0; i < lines.Length; i++) {
         string line = lines[i];
         if (i % 3 == 2) Crash.Add (line.Trim () == "1");
         else {
            float[] f = [.. line.Split ().Select (float.Parse)];
            for (int j = 0; j < 9; j += 3) {
               P.Add (new (f[j], f[j + 1], f[j + 2]));
               P2.Add (P[^1] * mXfmBack);
            }
         }
      }
      PT = [.. P]; PT2 = [.. P2];

      F = new float[P.Count * 3];
      F2 = new float[P.Count * 3];
      for (int i = 0; i < P.Count; i++) {
         F[i * 3] = P[i].X; F[i * 3 + 1] = P[i].Y; F[i * 3 + 2] = P[i].Z;
         F2[i * 3] = P2[i].X; F2[i * 3 + 1] = P2[i].Y; F2[i * 3 + 2] = P2[i].Z;
      }

      CT = new CTri[P.Count / 3];
      CT2 = new CTri[P.Count / 3];
      for (int i = 0; i < CT.Length; i++) {
         int a = i * 3;
         CT[i] = new CTri (PT, a, a + 1, a + 2);
         CT2[i] = new CTri (PT2, a, a + 1, a + 2);
      }
      V = [.. PT.Select (pt => Vector128.Create (pt.X, pt.Y, pt.Z, 0))];
   }

   Matrix3 mXfmBack, mXfm;
   Point3f[] PT, PT2;
   List<bool> Crash = [];
   CTri[] CT, CT2;
   float[] F, F2;
   Vector128<float>[] V;

   //[Benchmark (Baseline = true)]
   public void CollideFlux () {
      int crashes = 0;
      for (int i = 0, n = Crash.Count; i < n; i++) {
         int a = i * 6;
         bool check = Tri.CollideFlux (PT[a], PT[a + 1], PT[a + 2], PT[a + 3], PT[a + 4], PT[a + 5]);
         if (check) crashes++;
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   //[Benchmark]
   public unsafe void CollideMoller () {
      int crashes = 0; 
      fixed (float* pf = F) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 6;
            bool check = Tri.CollideMoller (pf, a, a + 1, a + 2, a + 3, a + 4, a + 5);
            if (check) crashes++;
         }
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   //[Benchmark (Baseline = true)]
   public unsafe void CollideMollerFast () {
      int crashes = 0;
      fixed (float* pf1 = F) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 2;
            bool check = Tri.CollideMollerFast (pf1, ref CT[a], ref CT[a + 1]);
            if (check) crashes++;
         }
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   //[Benchmark]
   public unsafe void CollideXformed () {
      int crashes = 0;
      fixed (Point3f* pp1 = PT)
      fixed (Point3f* pp2 = PT2) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 2;
            bool check = Tri.Collide (pp1, ref CT[a], pp2, ref CT2[a + 1], mXfm);
            if (check) crashes++;
         }
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   [Benchmark (Baseline = true)]
   public void CollideDevillers () {
      int crashes = 0;
      for (int i = 0, n = Crash.Count; i < n; i++) {
         int a = i * 2;
         bool check = Collision.TriTri (PT, in CT[a], in CT[a + 1]);
         if (check) crashes++;
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   [Benchmark]
   public void CollideDevillersVector () {
      int crashes = 0;
      for (int i = 0, n = Crash.Count; i < n; i++) {
         int a = i * 2;
         bool check = Collision.TriTri (V, in CT[a], in CT[a + 1]);
         if (check) crashes++;
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

}

static class Program {
   public static void Main () {
      var t = new Tester ();

      int N = 10;
      Stopwatch sw = Stopwatch.StartNew ();
      sw.Start ();
      for (int i = 0; i < N; i++)
         t.CollideDevillers ();
      sw.Stop ();
      Console.WriteLine ($"Devillers Scalar ({N} iteration): {sw.Elapsed.TotalMilliseconds.Round (2)} ms");

      sw.Restart ();
      for (int i = 0; i < N; i++)
         t.CollideDevillersVector ();
      sw.Stop ();
      Console.WriteLine ($"Devillers Vector ({N} iteration): {sw.Elapsed.TotalMilliseconds.Round (2)} ms");

      BenchmarkRunner.Run<Tester> ();
      return;

      //t.CollideFlux ();
      //t.CollideMoller ();
      //t.CollideMollerFast ();
      //t.CollideXformed ();
   }
}
