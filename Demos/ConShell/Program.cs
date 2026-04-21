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

      var dr = new DXFReader ("C:\\Dropbox\\Nori\\Dimension\\Dimensions.dxf");
      var dwg = dr.Load ();
      CurlWriter.Save (dwg, "c:/etc/test.curl", "Dimension test");
   }
}
