// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;

      Check1 ();
   }

   static void Check1 () {
      List<Point3f> P = [];
      foreach (var line in File.ReadAllLines ("c:/etc/tri/input.txt")) {
         int[] a = [.. line.Split ().Select (int.Parse)];
         for (int i = 0; i < 9; i += 3) P.Add (new (a[i], a[i + 1], a[i + 2]));
      }

      var sb = new StringBuilder ();
      int crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = Tri.CollideMCAM (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideMCAM: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output1.txt", sb.ToString ());

      sb = new StringBuilder ();
      crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = Tri.CollideFlux (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (i == j) check = true;
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideFlux: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output2.txt", sb.ToString ());

      sb = new StringBuilder ();
      crashes = 0;
      for (int i = 0; i < 100; i++) {
         int a = i * 3;
         for (int j = 0; j < 100; j++) {
            int b = j * 3;
            bool check = Tri.CollideHeld (P[a], P[a + 1], P[a + 2], P[b], P[b + 1], P[b + 2]);
            if (check) crashes++;
            sb.AppendLine ($"{i} {j} {(check ? 1 : 0)}");
         }
      }
      Console.WriteLine ($"CollideHeld: {crashes} crashes");
      File.WriteAllText ("c:/etc/tri/output3.txt", sb.ToString ());

      //if (File.ReadAllText ("c:/etc/tri/output1.txt") != File.ReadAllText ("c:/etc/tri/output.txt"))
      //   throw new InvalidOperationException ();
      //if (File.ReadAllText ("c:/etc/tri/output2.txt") != File.ReadAllText ("c:/etc/tri/output.txt"))
      //   throw new InvalidOperationException ();

      Console.WriteLine ("Done");
   }
}