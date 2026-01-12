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
   }
   List<Point3f> P = [];
   List<bool> Crash = [];
   float[] F;

//   [Benchmark]
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
      if (crashes != 202) throw new NotImplementedException ();
   }

   [Benchmark (Baseline = true)]
   public void CollideFlux () {
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideFlux (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
         }
      }
      if (crashes != 202) throw new NotImplementedException ();
   }

   [Benchmark]
   public unsafe void CollideMoller () {
      int crashes = 0; 
      fixed (float* pf = F) {
         for (int i = 0; i < 100; i++) {
            int a = i * 3;
            for (int j = 0; j < 100; j++) {
               int b = j * 3;
               bool check = i == j || Tri.CollideMoller (pf, a, a + 1, a + 2, b, b + 1, b + 2);
               if (check) crashes++;
            }
         }
      }
   }

   [Benchmark]
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
      if (crashes != 202) throw new NotImplementedException ();
   }
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
      // new Tester ().TestHeld ();
   }
}
