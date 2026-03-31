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

      foreach (var file in Directory.GetFiles ("C:\\etc\\fold", "*.dxf")) {
         var dwg = DXFReader.Load (file);
         foreach (var e2p in dwg.Ents.OfType<E2Poly> ()) Save (e2p.Poly);

         var folder = new Folder (dwg);
         var model = folder.Process ();
         foreach (var e3p in model.Ents.OfType<E3Plane> ()) {
            e3p.Contours.Select (a => a.Flatten (e3p.CS)).ForEach (Save);
         }
      }
   }

   static void Save (Poly poly) {
      Console.Write ('.');
      Dwg2 dwg = new ();
      dwg.Add (poly);
      DXFWriter.Save (dwg, $"C:\\etc\\wind\\{mN++}.dxf");
   }
   static int mN;

   static void F0 () {
      var dwg = DXFReader.Load ("c:/etc/tessinput9.dxf");
      var pline = dwg.Ents.OfType<E2Poly> ().Single ().Poly;
      foreach (var seg in pline.Segs)
         Console.WriteLine ($"{seg}  {seg.Length.R6 ()} @ {seg.GetSlopeAt (0).R2D ().R6 ()} ");
      Console.WriteLine (pline.GetWinding ());

      var tess = FastTess2D.Borrow ();
      tess.AddPoly (pline, false);
      tess.Process ();
      Console.WriteLine (tess.Tris.Count);
   }

   static void F1 () {
      var dwg = DXFReader.Load ("c:/etc/FOLD/08.dxf");
      var folder = new Folder (dwg);
      var model = folder.Process ();
      foreach (var ep in model.Ents.OfType<E3Plane> ()) {
         Console.WriteLine ($"{ep.Id}");
         var dwg2 = new Dwg2 ();
         foreach (var con in ep.Contours)
            dwg2.Add (con.Flatten (ep.CS));
         DXFWriter.Save (dwg2, $"c:/etc/tessinput{ep.Id}.dxf");
         var mesh = ep.Mesh;
         Console.WriteLine ($"- {mesh.Triangle.Length}");
      }
   }
}
