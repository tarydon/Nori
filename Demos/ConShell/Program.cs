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

      var files = Directory.GetFiles ("W:\\DXF", "*.dxf");
      for (int i = 0; i < files.Length; i++) {
         Console.Write ('.');
         if ((i % 1000) == 0) Console.Write ($" {i} ");
         var dwg = DXFReader.Load (files[i]);
      }
   }
}
