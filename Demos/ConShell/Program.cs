// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
using Nori.UX;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Test ();
   }

   static void Test () {
      InlayGen igen = new ("C:\\etc\\zero.in");
      string s = igen.Generate ();
      Console.WriteLine (s);
      //igen.GenerateTo ("c:\\etc\\Output.cs");
      //InlayCompiler icomp = new ("c:\\etc\\Output.cs");
      //icomp.Compile ();
   }
}