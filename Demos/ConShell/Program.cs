// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Text;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Lib.Tessellate = Tess2D.Process;
      var lines = Directory.GetFiles ("W:\\STEP", "*.stp")
                           .Select (a => new FileInfo (a))
                           //.OrderBy (a => a.Length)
                           .Select (a => Path.GetFileNameWithoutExtension (a.FullName))
                           .ToArray ();
      Console.Title = $"Processing {lines.Length} files";

      for (int i = 0; i < lines.Length; i++) {
         var line = lines[i];
         var w = line.SplitCSV ();
         var name = w[0];
         var file = $"W:\\STEP\\{name}.stp";
         if (!File.Exists (file)) continue;

         Console.Write ($"{i} {file}");
         var sr = new STEPReader (file);
         try {
            var model = sr.Load ();
            if (model.Ents.Count == 0) throw new Exception ("Empty Model");
            foreach (var e3 in model.Ents.OfType<E3Surface> ()) {
               Console.Write ('.');
               _ = e3.Mesh;
            }
            Move (name, "PARK");
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write (" " + e.Message);
            Console.ResetColor ();
            if (e.Message.Contains ("connect end-to-end"))
               Move (name, "DISJOINT");
            if (e.Message.Contains ("MakeArc"))
               Move (name, "MAKEARC");
            if (e.Message.Contains ("Empty Model"))
               Move (name, "EMPTY");
            if (e.Message.Contains ("Non-planar contour"))
               Move (name, "NON-PLANAR");
            if (e.Message.Contains ("not orthogonal"))
               Move (name, "NON-ORTHOGONAL");
         }
         Console.WriteLine ();
      }
   }

   static void Move (string name, string dir) {
      Directory.CreateDirectory ($"W:\\STEP\\{dir}");
      File.Move ($"W:\\STEP\\{name}.stp", $"W:\\STEP\\{dir}\\{name}.stp");
   }
}
