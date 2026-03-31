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

      var dwg = DXFReader.Load ("c:/etc/FOLD/20.dxf");
      var folder = new Folder (dwg);
      var model = folder.Process ();
      foreach (var ep in model.Ents.OfType<E3Plane> ()) {
         Console.WriteLine ($"{ep.Id}");
         var dwg2 = new Dwg2 ();
         foreach (var con in ep.Contours)
            dwg2.Add (con.Flatten (ep.CS));
         DXFWriter.Save (dwg2, "c:/etc/tessinput.dxf");
         var mesh = ep.Mesh;
      }
   }
}
