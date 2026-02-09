// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
using Nori;
namespace ConShell;

class Tester {
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
      RemoveZeroes ();
   }
   List<Point3f> P = [];
   List<bool> Crash = [];
   float[] F;

   public void RemoveZeroes () {
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         Vector3f area = (P[j + 1] - P[j]) * (P[j + 2] - P[j]);
         Vector3f area2 = (P[j + 4] - P[j + 3]) * (P[j + 5] - P[j + 4]);
         if (area.Length < 1e-6 || area2.Length < 1e-6)
            Console.WriteLine ($"Triangle {i}, Line {i * 3 + 1}");
      }
   }

   public void TestMCAM () {
      int crashes = 0; 
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         bool check = Tri.CollideMCAM (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
         if (check) crashes++;
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"MCAM: Found {crashes}, Expected {ecrashes}");
   }

   void OutputTris (int n, bool crashing) {
      var sb = new StringBuilder ();
      for (int i = 0; i < 6; i++) sb.AppendLine ($"{P[n + i].X} {P[n + i].Y} {P[n + i].Z}");
      sb.AppendLine (crashing ? "1" : "0");
      File.WriteAllText ("c:/etc/sampletri.txt", sb.ToString ());
   }

   public void TestFlux () {
      int crashes = 0;
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         bool check = Tri.CollideFlux (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
         if (check) crashes++;
         if (check != Crash[i]) {
            Console.WriteLine ("Triangle {0}, Line {1} >>", i, i * 3 + 1);
            OutputTris (j, Crash[i]);
         }
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"Flux: Found {crashes}, Expected {ecrashes}");
   }

   public unsafe void TestMoller () {
      int crashes = 0;
      double maxArea = 0; 
      fixed (float* pf = F) {
         for (int i = 0; i < Crash.Count; i++) {
            int j = i * 6;
            bool check = Tri.CollideMoller (pf, j, j + 1, j + 2, j + 3, j + 4, j + 5);
            if (check) crashes++;
            if (check != Crash[i]) {
               double area1 = ((P[j + 1] - P[j]) * (P[j + 2] - P[j])).Length;
               double area2 = ((P[j + 4] - P[j + 3]) * (P[j + 5] - P[j + 3])).Length;
               double area = Math.Min (area1, area2);
               if (area > maxArea) {
                  Console.WriteLine ("Triangle {0}, Line {1} >>", i, i * 3 + 1);
                  OutputTris (j, Crash[i]);
                  if (i == 2011) {
                     bool c1 = Tri.CollideFlux (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
                     bool c2 = Tri.CollideMoller (pf, j, j + 1, j + 2, j + 3, j + 4, j + 5);
                  }
                  maxArea = area;
               }
            }

         }
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"Moller: Found {crashes}, Expected {ecrashes}");
   }
}

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;

      Tester t = new ();
      t.TestMCAM ();
      t.TestFlux ();
      t.TestMoller ();
   }
}