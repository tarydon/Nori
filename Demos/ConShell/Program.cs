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
      dwg.Layers.Add (new Layer2 ("DIMENSION", Color4.Blue, ELineType.Continuous));
      dwg.Layers.Add (new Layer2 ("BEND", Color4.Black, ELineType.Dot));
      dwg.Layers.Current = dwg.Layers[1];
      CurlWriter.Save (dwg, "c:/etc/test.curl");


     // var dwg2 = (Dwg2)CurlReader.Load ("c:/etc/test.curl");
   }
}
