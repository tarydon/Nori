// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO.Compression;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Test1 ();
   }

   static void Test1 () {
      var stm = File.OpenRead ("c:/etc/openfx1.fx");
      var zar = new ZipArchive (stm, ZipArchiveMode.Read, false);
      var data = zar.ReadAllBytes ("Data\\Dwg");
      new AgMetaReader (data, 8).Parse ();
   }
}
