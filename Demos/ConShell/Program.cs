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
      File.WriteAllText ("c:\\etc\\output.cs", s);
      Console.WriteLine (s);

      //InlayCompiler icomp = new (s);
      //icomp.Compile ();
   }
}
