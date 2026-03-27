// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.ComponentModel.DataAnnotations;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;

      var files = Directory.GetFiles ("W:\\DXF", "*.dxf");
      for (int i = 0; i < files.Length; i++) {
         var file = files[i];
         if ((i % 1000) == 0) Console.Write (i);
         var dwg1 = Nori.Old.DXFReader.Load (file);
         CurlWriter.Save (dwg1, "c:/etc/compare/old.curl");
         var dwg2 = DXFReader.Load (file);
         CurlWriter.Save (dwg2, "c:/etc/compare/new.curl");
         string s1 = File.ReadAllText ("c:/etc/compare/old.curl"), s2 = File.ReadAllText ("c:/etc/compare/new.curl");
         if (s1 == s2) Console.Write ('.');
         else Console.Write ('*');
      }
   }
}
