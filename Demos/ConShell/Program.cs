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
      Lib.Tessellate = Tess2D.Process;
      var lines = Directory.GetFiles ("W:\\STEP", "*.stp")
                           .Select (a => new FileInfo (a))
                           .OrderBy (a => a.Length)
                           .Select (a => Path.GetFileNameWithoutExtension (a.FullName))
                           .ToArray ();
      Console.Title = $"Processing {lines.Length} files";

      for (int i = 0; i < lines.Length; i++) {
         var line = lines[i];
         var w = line.SplitCSV ();
         var name = w[0];
         var file = $"W:\\STEP\\{name}.stp";
         if (!File.Exists (file)) continue;

         Console.Write ($"{i} {file}");
         var sr = new STEPReader (file);
         try {
            var model = sr.Load ();
            var desc = new DescribeModel (file, model).Process ();
            var desc1 = File.ReadAllText ($"W:\\STEP\\NOTES\\{name}.txt");
            if (Same (desc, desc1)) {
               Console.Write (" ..........");
               Move (name, "DONE");
            } else {
               // File.WriteAllText ("c:/etc/desc.txt", desc);
               // Process.Start ("winmergeu.exe", $"c:/etc/desc.txt W:\\STEP\\NOTES\\{name}.txt").WaitForExit ();
               Console.ForegroundColor = ConsoleColor.Yellow;
               Console.Write (" **********");
               Console.ResetColor ();
            }
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write (" " + e.Message);
            Console.ResetColor ();
            Move (name, "CRASH");
         }
         Console.WriteLine ();
      }
   }

   static bool Same (string d1, string d2) {
      if (d1 == d2) return true;
      var w1 = d1.Split ('\n'); var w2 = d2.Split ('\n');
      if (w1.Length != w2.Length) return false;
      for (int i = 0; i < w1.Length; i++) {
         if (w1[i] == w2[i]) continue;
         string s1 = w1[i].Trim (), s2 = w2[i].Trim ();
         if (s1.StartsWith ("BOUND:") && s2.StartsWith ("BOUND:")) {
            int[] a = [.. s1[6..].Split ('x').Select (int.Parse)];
            int[] b = [.. s2[6..].Split ('x').Select (int.Parse)];
            bool same = true;
            for (int j = 0; j < 3; j++)
               if (Math.Abs (a[j] - b[j]) > 1) same = false;
            if (same) continue; 
         }
         return false;
      }
      return true; 
   }

   static void Move (string name, string dir) {
      Directory.CreateDirectory ($"W:\\STEP\\{dir}");
      File.Move ($"W:\\STEP\\{name}.stp", $"W:\\STEP\\{dir}\\{name}.stp");
   }
}
