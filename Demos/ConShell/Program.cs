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
      Lib.Tessellate = FastTess2D.Process;
      foreach (var file in Directory.GetFiles ("W:\\NoriSample", "*.stp"))
         Process (file);
   }

   static void Process (string file) {
      Console.Write ($"{++N}. {file}");
      try {
         var model = STEPReader.Load (file);
         var meshes = model.Ents.OfType<E3Surface> ().Select (s => s.Mesh).ToList ();
         var shmodel = new SheetMetalizer (model).Process ().Value;
         var dwg = new Unfolder (shmodel).Process ().Value;
         if (!dwg.MarkInOut ()) File.Move (file, "W:\\NoriSample\\INOUT\\" + Path.GetFileName (file));
      } catch (Exception e) {
         File.Move (file, "W:\\NoriSample\\BAD\\" + Path.GetFileName (file));
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.Write ($" {e.Message}");
         Console.ResetColor ();
      }
      Console.WriteLine ();
   }

   static int N;
}