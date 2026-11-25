// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using Nori;
namespace ConShell;

class Program {
   // MAIN used for NURB-PLANE process cycle
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Lib.Tessellate = Tess2D.Process;
      var names = Directory.GetFiles ("W:\\STEP", "*.stp")
                           .Select (a => new FileInfo (a))
                           .OrderBy (a => a.Length)
                           .Select (a => Path.GetFileNameWithoutExtension (a.FullName))
                           .ToArray ();
      Console.Title = $"Processing {names.Length} files";
      for (int i = 0; i < names.Length; i++) {
         string name = names[i];
         Console.Write ($"{i + 1}. {name} ");
         CheckTessellation (name);
      }
   }

   static void CheckTessellation (string name) {
      var file = $"W:\\STEP\\{name}.stp";
      var model = new STEPReader (file).Load ();

      bool fast = true;
      foreach (var ent in model.Ents.OfType<E3Cylinder> ()) {
         var a = ent.Mesh;
         if (!ent.IsFastTess) fast = false;
      }
      if (fast) { Console.Write (" *FAST* "); Move (name, "GOOD"); }
      Console.WriteLine (); 
   }

   static void TryUnfolding (string name) {
      // Load the sheet metal model      
      var file = $"W:\\STEP\\{name}.stp";
      var sr = new STEPReader (file);

      try {
         Model3 model = sr.Load ();
         var tup1 = new SheetMetalizer (model).Process ();
         if (tup1.IsErr) {
            Move (name, "THICKEN");
            WriteY (tup1.Error.ToString ());
            return;
         }
         var shmodel = tup1.Value;

         var tup2 = new Unfolder (shmodel).Process ();
         if (tup2.IsErr) {
            Move (name, "UNFOLD");
            WriteY (tup2.Error.ToString ());
            return;
         }

         Dwg2 dwg = tup2.Value;
         if (!dwg.MarkInOut ()) {
            WriteY ("No outer contour");
            Move (name, "MARKINOUT");
            return;
         }
         VerifyDwg (name, shmodel, dwg);
      } catch (Exception e) {
         Move (name, "CRASH");
         WriteY (e.Message);
         return;
      }
      Console.WriteLine ();
   }

   static void VerifyDwg (string name, Model3 shModel, Dwg2 dwg) {
      var lines = File.ReadAllLines ($"W:\\STEP\\NOTES\\{name}.txt");
      int cFlat = lines[0][6..].ToInt (), cFlex = lines[1][6..].ToInt (), cPoly = lines[2][6..].ToInt ();
      double thick = lines[3][6..].ToDouble (), area = lines[4][6..].ToDouble ();

      int cFlat2 = shModel.Ents.OfType<E3Flat> ().Count ();
      int cFlex2 = shModel.Ents.OfType<E3Flex> ().Count ();
      int cPoly2 = dwg.Ents.OfType<E2Poly> ().Count ();
      double thick2 = shModel.Ents.OfType<E3Flat> ().First ().Thickness;
      double area2 = dwg.Bound.Area;
      if (cFlat != cFlat2 || cFlex != cFlex2 || cPoly != cPoly2) {
         WriteY ("Ents Different");
         Move (name, "ENTSDIFFERENT");
         return;
      }
      if (!thick.EQ (thick2, 0.1) || !area.EQ (area2, area * 0.05)) {
         WriteY ("Area different");
         Move (name, "SIZING");
         return;
     }
   }

   static void WriteY (string s) {
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.Write (s);
      Console.ResetColor ();
   }

   static void Handle (string name, string file) {
      var sr = new STEPReader (file);
      var model = sr.Load ();
      var desc0 = File.ReadAllText ($"W:\\STEP\\NOTES\\{name}.txt");
      var desc1 = new DescribeModel (file, model).Process ();
      if (Same (desc0, desc1)) {
         Console.Write (" ..........");
         Move (name, "DONE");
      } else {
         File.WriteAllText ("c:/etc/desc.txt", desc1);
         Process.Start ("winmergeu.exe", $"c:/etc/desc.txt W:\\STEP\\NOTES\\{name}.txt").WaitForExit ();
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.Write (" **********");
         Console.ResetColor ();
      }
      Console.WriteLine (); 
   }

   static void Main1 () {
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
