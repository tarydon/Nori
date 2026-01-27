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
      for (int i = 0; i < lines.Length; i++) {
         string line = lines[i];
         if (i % 3 == 2) Crash.Add (line.Trim () == "1");
         else {
            float[] f = [.. line.Split ().Select (float.Parse)];
            for (int j = 0; j < 9; j += 3)
               P.Add (new (f[j], f[j + 1], f[j + 2]));
         }
      }

      F = new float[P.Count * 3];
      for (int i = 0; i < P.Count; i++) {
         F[i * 3] = P[i].X; F[i * 3 + 1] = P[i].Y; F[i * 3 + 2] = P[i].Z;
      }

      CT = new CTri[P.Count / 3];
      for (int i = 0; i < CT.Length; i++) {
         int a = i * 9;
         CT[i] = new CTri (F, a, a + 3, a + 6);
      }
   }
   List<Point3f> P = [];
   List<bool> Crash = [];
   CTri[] CT;
   float[] F;

   public void CollideMCAM () {
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideMCAM (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
         }
      }
   }

   [Benchmark (Baseline = true)]
   public void CollideFlux () {
      int crashes = 0;
      for (int i = 0, n = Crash.Count; i < n; i++) {
         int a = i * 6;
         bool check = Tri.CollideFlux (P[a], P[a + 1], P[a + 2], P[a + 3], P[a + 4], P[a + 5]);
         if (check) crashes++;
      }
   }

   [Benchmark]
   public unsafe void CollideMoller () {
      int crashes = 0; 
      fixed (float* pf = F) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 6;
            bool check = Tri.CollideMoller (pf, a, a + 1, a + 2, a + 3, a + 4, a + 5);
            if (check) crashes++;
         }
      }
   }

   [Benchmark]
   public unsafe void CollideMollerFast () {
      int crashes = 0;
      fixed (float* pf = F) {
         for (int i = 0, n = Crash.Count; i < n; i++) {
            int a = i * 2;
            bool check = Tri.CollideMollerFast (pf, ref CT[a], ref CT[a + 1]);
            if (check) crashes++;
         }
      }
   }

   public void CollideHeld () {
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideHeld (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
         }
      }
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();

      //var t = new Tester ();
      //t.CollideFlux ();
      //t.CollideMoller ();
      //t.CollideMollerFast ();
   }
}
