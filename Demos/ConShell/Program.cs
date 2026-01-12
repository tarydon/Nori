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
   }
   List<Point3f> P = [];
   List<bool> Crash = [];
   float[] F;

   public void TestMCAM () {
      int crashes = 0; 
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         bool check = Tri.CollideMCAM (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
         if (check) crashes++;
         if (check != Crash[i])
            Tri.CollideFlux (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"MCAM: Found {crashes}, Expected {ecrashes}");
   }

   public void TestFlux () {
      int crashes = 0;
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         bool check = Tri.CollideFlux (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
         if (check) crashes++;
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"Flux: Found {crashes}, Expected {ecrashes}");
   }

   public void TestHeld () {
      int crashes = 0;
      for (int i = 0; i < Crash.Count; i++) {
         int j = i * 6;
         bool check = Tri.CollideHeld (P[j], P[j + 1], P[j + 2], P[j + 3], P[j + 4], P[j + 5]);
         if (check) crashes++;
      }
      int ecrashes = Crash.Count (b => b);
      Console.WriteLine ($"Held: Found {crashes}, Expected {ecrashes}");
   }

   public unsafe void TestMoller () {
      int crashes = 0; 
      fixed (float* pf = F) {
         for (int i = 0; i < Crash.Count; i++) {
            int j = i * 6;
            bool check = Tri.CollideMoller (pf, j, j + 1, j + 2, j + 3, j + 4, j + 5);
            if (check) crashes++;
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
      t.TestHeld ();
      t.TestMoller ();
   }
}