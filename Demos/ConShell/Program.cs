// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace ConShell;

class Program {
   static void Main (string[] args) {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      double r = args[0].ToDouble (), len = args[1].ToDouble ();
      double a = Lib.GetArcSteps (r, len / r, 0.5, Lib.PI);
      Console.WriteLine ($"0.5 = {a}");
   }
}
