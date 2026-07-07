// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
using UXDemo;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Test ();
   }

   static void Test () {
      InlayCompiler ic = new InlayCompiler ("C:\\etc\\basic.in");
      ic.Compile ("c:\\etc\\Output.cs");
   }
}