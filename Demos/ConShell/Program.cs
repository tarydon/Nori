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
      Dwg2 dwg = new ();
      dwg.Add (new Point2 (3, 4));
      CurlWriter.Save (dwg, "c:/etc/test.curl");
      dwg.Layers.RemoveAt (0);
   }
}
