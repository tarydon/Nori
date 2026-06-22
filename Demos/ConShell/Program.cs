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
      Lib.Tessellate = FastTess2D.Process;
      for (int i = 0; i < 1; i++) {
         using var bt = new BlockTimer ();
         foreach (var file in Directory.GetFiles ("W:\\NoriSample\\GOOD", "*.stp").Take (10))
            Process (file);
      }
   }

   static void Process (string file) {
      Console.WriteLine (file);
      var model = STEPReader.Load (file);
      var shmodel = new SheetMetalizer (model).Process ().Value;
      var dwg = new Unfolder (shmodel).Process ().Value;
   }

   static int N;
}
