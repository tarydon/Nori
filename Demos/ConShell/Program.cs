// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
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
            sr.Load ();
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine (e);
            return;
            File.Move (file, $"W:\\STEP\\BAD\\{name}.stp");
            Console.ResetColor ();
         }
         Console.WriteLine ();
      }
   }
}
