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
      var lines = File.ReadAllLines ("C:\\ETC\\classify.txt");

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
            Move (name, "PARK");
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write (" " + e.Message);
            Console.ResetColor ();
            //if (e.Message.Contains ("Unsupported entity"))
            //   Move (name, "UNHANDLED-ENT");
            //if (e.Message.Contains ("MakeArc"))
            //   Move (name, "MAKEARC");
         }
         Console.WriteLine ();
      }
   }

   static void Move (string name, string dir) {
      Directory.CreateDirectory ($"W:\\STEP\\{dir}");
      File.Move ($"W:\\STEP\\{name}.stp", $"W:\\STEP\\{dir}\\{name}.stp");
   }
}
