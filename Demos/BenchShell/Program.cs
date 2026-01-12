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
      foreach (var line in File.ReadAllLines ("c:/etc/tri/input.txt")) {
         int[] a = [.. line.Split ().Select (int.Parse)];
         for (int i = 0; i < 9; i += 3) P.Add (new (a[i], a[i + 1], a[i + 2]));
      }
   }
   List<Point3f> P = [];

   [Benchmark]
   public void CollideMCAM () {
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = Tri.CollideMCAM (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
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
            bool check = Tri.CollideFlux (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (i == j) check = true;
            if (check) crashes++;
         }
      }
      if (crashes != 202) throw new NotImplementedException ();
   }

   [Benchmark]
   public void CollideHeld () {
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = Tri.CollideHeld (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
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
