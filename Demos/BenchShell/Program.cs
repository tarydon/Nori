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
   }
   Matrix3 mXfmBack, mXfm;
   Point3f[] PT, PT2;
   List<bool> Crash = [];
   CTri[] CT, CT2;
   float[] F, F2;

   public unsafe void CollideMoller () {
      int crashes = 0;
      fixed (float* pf1 = F) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 2;
            bool check = Tri.CollideMoller (pf1, ref CT[a], ref CT[a + 1]);
            if (check) crashes++;
         }
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   [Benchmark (Baseline =true)]
   public unsafe void CollideTriTri () {
      int crashes = 0;
      for (int i = 0, n = Crash.Count; i < n; i++) {
         int a = i * 2;
         bool check = Tri.TriTri (PT, in CT[a], in CT[a + 1]);
         if (check) crashes++;
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }

   [Benchmark]
   public unsafe void CollideGD () {
      int crashes = 0;
      fixed (Point3f* pf = PT) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 2;
            bool check = Tri.TriGD (pf, in CT[a], in CT[a + 1]);
            if (check) crashes++;
         }
      }
      if (crashes != 14708) throw new NotImplementedException ();
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();

      var t = new Tester ();
      t.CollideMoller ();
      t.CollideTriTri ();
      t.CollideGD ();
   }
}
