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

      string ofile = "c:\\etc\\old.curl", nfile = "c:\\etc\\new.curl";
      foreach (var file in Directory.GetFiles ("W:\\DXF", "*.dxf")) {
         var dwg1 = DXFReader.Load (file); 
         var cl1 = dwg1.CurrentLayer;
         CurlWriter.Save (dwg1, ofile);
         var s1 = File.ReadAllText (ofile);

         var dwg2 = Nori.Alt.DXFReader.Load (file); 
         var cl2 = dwg2.CurrentLayer;
         CurlWriter.Save (dwg2, nfile);
         var s2 = File.ReadAllText (nfile);

         if (s1 != s2) {
            Console.Write ($" {Path.GetFileName (file)} ");
            Process.Start ("winmergeu.exe", $"{ofile} {nfile}");
            File.Move (file, "W:\\DXF\\Bad\\" + Path.GetFileName (file));
            break;
         } else {
            Console.Write ('.');
            File.Move (file, "W:\\DXF\\Done\\" + Path.GetFileName (file));
         }
      }
   }
}
