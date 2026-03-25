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

      Nori.Alt.DXFReader dr = new ("N:/TData/IO/DXF/AllEnts.dxf");
      var dwg = dr.Load ();
   }

   static void LoadAllDXFs () {
      var files = Directory.GetFiles ("W:/DXF", "*.dxf");
      for (int i = 0; i < files.Length; i++) {
         if (i % 1000 == 0) Console.WriteLine ($"{i} {files[i]}");
         Nori.Alt.DXFReader dr = new (files[i]);
         var dwg = dr.Load ();
      }
   }
}
