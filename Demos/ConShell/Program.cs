// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
using Nori;
namespace ConShell;

class Tester {
   public Tester () {
      foreach (var line in File.ReadAllLines ("c:/etc/tri/input.txt")) {
         int[] a = [.. line.Split ().Select (int.Parse)];
         for (int i = 0; i < 9; i += 3) P.Add (new (a[i], a[i + 1], a[i + 2]));
      }

      F = new float[P.Count * 3];
      for (int i = 0; i < P.Count; i++) {
         F[i * 3] = P[i].X; F[i * 3 + 1] = P[i].Y; F[i * 3 + 2] = P[i].Z;
      }
   }
   List<Point3f> P = [];
   float[] F;

   public void TestMCAM () {
      var sb = new StringBuilder ();
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideMCAM (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideMCAM: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output1.txt", sb.ToString ());
   }

   public void TestFlux () {
      var sb = new StringBuilder ();
      var crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideFlux (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideFlux: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output2.txt", sb.ToString ());
   }

   public void TestHeld () {
      var sb = new StringBuilder ();
      var crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = i == j || Tri.CollideHeld (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideHeld: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output3.txt", sb.ToString ());
   }

   public unsafe void TestFluxAlt () {
      var sb = new StringBuilder ();
      var crashes = 0;
      fixed (float* pf = F) {
         for (int i = 0; i < 100; i++) {
            int a = i * 3;
            for (int j = 0; j < 100; j++) {
               int b = j * 3;
               bool check = Tri.CollideMoller (pf, a, a + 1, a + 2, b, b + 1, b + 2);
               if (check) crashes++;
               sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
            }
         }
      }
      Console.WriteLine ($"CollideHeld: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output3.txt", sb.ToString ());
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
      t.TestFluxAlt ();
   }
}