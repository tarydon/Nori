// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;

      Console.WriteLine (Process ("N:\\TData\\STEP\\S00178.stp"));

      //foreach (var file in Directory.GetFiles ("C:\\STEPS", "*.stp")) {
      //   double thick = Process (file);
      //   if (!thick.IsZero ()) {
      //      Console.Write ($"{thick.R3 ()}");
      //   } else {
      //      File.Move (file, "C:\\STEPS\\0\\" + Path.GetFileName (file));
      //      Console.Write ('*');
      //   }
      //}
   }

   static Model3 Process (string file) {
      var reader = new STEPReader (file);
      var model = reader.Load ();
      var thickener = new ModelThickener (model);
      return thickener.Process ();
   }
}