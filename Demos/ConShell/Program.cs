// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace ConShell;

class Program {
   static void Main (string[] args) {
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
