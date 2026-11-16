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
      var lines = File.ReadAllLines ("W:\\STEP\\classify.txt");
      foreach (var line in lines) {
         Console.Write ('.');
         var w = line.SplitCSV ();
         string file = w[0], classify1 = w[1];
         var sr = new STEPReader ($"W:\\STEP\\{file}.stp");
         sr.Load ();
         string classify2 = sr.Classify ();
         if (classify1 != classify2) {
            if (classify1 == "Assembly" || classify2 == "Assembly") {
               Console.WriteLine ($" {file} {classify1} {classify2}");
               return;
            }
         }
      }
   }

   static void Main1 () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      var files = Directory.GetFiles ("W:\\STEP", "*.stp")
         .Select (a => new FileInfo (a))
         .OrderBy (a => a.Length)
         .Select (a => a.FullName)
         .ToList ();
      for (int i = 0; i < files.Count; i++) {
         string file = files[i];
         var sr = new STEPReader (file);
         try {
            Console.Write ($"{i} {Path.GetFileName (file)} ");
            sr.Load ();
            Console.WriteLine ();
         } catch (Exception e) {
            Console.WriteLine (e);
            return;
         }
      }
   }
}
