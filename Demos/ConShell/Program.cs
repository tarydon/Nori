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

      var reader = new STEPReader ("N:/TData/STEP/S00178.stp");
      var model = reader.Load ();
      var thickener = new ModelThickener (model);
      var sheetmodel = thickener.Process ();
   }
}