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

      var dwg = DXFReader.Load ("c:/etc/FOLD.00.dxf");
      var folder = new Folder (dwg);
      folder.Process ();
      folder.Dump ("c:/etc/test.dxf");
   }
}
