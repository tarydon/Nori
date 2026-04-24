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

      //var dwg = DXFReader.Load ("N:/TData/IO/DXF/AllEnts.dxf");
      //while (dwg.Ents.Count > 9) dwg.Ents.RemoveAt (dwg.Ents.Count - 1);
      //foreach (var e in dwg.Ents)
      //   Console.WriteLine (e.ToString ());

      //DXFWriter.Save (dwg, "c:/etc/test.dxf", true);

      var dwg = DXFReader.Load ("c:/etc/rect.dxf");
   }
}
