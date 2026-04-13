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

      //foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf")) {
      //   Console.WriteLine (file);
      //   var dr = new Nori.Alt.DXFReader (file);
      //   var dwg = dr.Load ();
      //   File.Move (file, "W:/DXF/DONE/" + Path.GetFileName (file));
      //}

      foreach (var file in Directory.GetFiles ("N:/", "*.dxf", SearchOption.AllDirectories)) 
         Read (file);

      using var bt = new BlockTimer ();
      foreach (var file in Directory.GetFiles ("N:/", "*.dxf", SearchOption.AllDirectories))
         Read (file);
   }

   static void Read (string file) {
      var dr = new Nori.Alt.DXFReader (file);
      var dwg = dr.Load ();
//      Console.WriteLine ();
   }
}
