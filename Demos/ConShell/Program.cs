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
      foreach (var file in Directory.GetFiles ("W:\\Kogu\\Original", "*.dxf")) {
         Console.WriteLine (file);
         var dwg = DXFReader.Load (file);
         new DwgStitcher (dwg).Process ();
         dwg.Purge ();
         DXFWriter.Save (dwg, "W:\\Kogu\\Tmp\\" + Path.GetFileName (file));
      }
   }
}
