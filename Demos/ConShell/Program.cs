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

      Compare ();

      //Nori.Alt.DXFReader dr = new ("N:/TData/IO/DXF/AllEnts.dxf");
      //var dwg = dr.Load ();
   }

   static void Compare () {
      var files = Directory.GetFiles ("W:\\DXF", "*.dxf")
                           .Select (a => new FileInfo (a))
                           .OrderBy (a => a.Length)
                           .Select (a => a.FullName)
                           .ToArray ();
      for (int i = 0; i < files.Length; i++) {
         if ((i % 1000) == 0) Console.Write (i + " ");
         var file = files[i];
         File.Copy (file, "c:\\etc\\compare\\test.dxf", true);
         Console.Write (file + " ");
         var dwg1 = DXFReader.Load (file);
         var dwg2 = new Nori.Alt.DXFReader (file).Load ();
         CurlWriter.Save (dwg1, "c:/etc/compare/old.curl", Path.GetFileNameWithoutExtension (file));
         CurlWriter.Save (dwg2, "c:/etc/compare/new.curl", Path.GetFileNameWithoutExtension (file));
         if (File.ReadAllText ("c:/etc/compare/old.curl") == File.ReadAllText ("c:/etc/compare/new.curl")) {
            Console.Write ("SAME");
            File.Move (file, "W:\\DXF\\SAME\\" + Path.GetFileName (file));
         } else {
            Process.Start ("winmergeu.exe", "c:\\etc\\compare\\old.curl c:\\etc\\compare\\new.curl");
            break;
         }
         Console.WriteLine ();
      }
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
