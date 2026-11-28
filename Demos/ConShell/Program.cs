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
         CompareDisjoint (name);
         Console.WriteLine ();
      }
   }

   static void AssyProcses (string name) {
      var file = $"W:\\STEP\\{name}.stp";
      try {
         var sr = new STEPReader (file);
         var model = sr.Load ();
         if (sr.IsSimplePart ()) Console.Write (" SIMPLE");
         else { Console.Write (" **ASSY**"); Move (name, "ASSY"); }
      } catch (Exception e) {
      }
   }

   static void CompareSurface (string name) {
      // Load the sheet metal model      
      var file = $"W:\\STEP\\{name}.stp";
      var sr = new STEPReader (file);
      Model3 model = sr.Load ();

      var lines = File.ReadAllLines ($"W:\\STEP\\NOTES\\{name}.txt");
      int cEnts = lines[0][5..].ToInt ();
      if (model.Ents.Count != cEnts) Console.Write (" ***ENTS***");
      var b0 = lines[1][6..].Split ('x').Select (int.Parse).Order ();

      var b = model.Bound.DiagVector;
      int[] b1 = [b.X.RoundN (), b.Y.RoundN (), b.Z.RoundN ()];
      b1 = [.. b1.Order ()];
      if (!b0.SequenceEqual (b1)) {
         Console.WriteLine ();
         Console.WriteLine ($"  {b0.ToCSV ()}");
         Console.WriteLine ($"  {b1.ToCSV ()}");
         Console.ReadKey ();
      }
   }

   static void CompareDisjoint (string name) {
      // Load the sheet metal model      
      var file = $"W:\\STEP\\{name}.stp";
      var sr = new STEPReader (file);
      Model3 model = sr.Load ();

      try {
         var tup1 = new SheetMetalizer (model).Process ();
         if (tup1.IsErr) {
            Move (name, "THICKEN");
            WriteY (tup1.Error.ToString ());
            return;
         }
         var shmodel = tup1.Value;

         var lines = File.ReadAllLines ($"W:\\STEP\\NOTES\\{name}.txt");
         int cEnts = lines[0][5..].ToInt (), cFlat = lines[1][5..].ToInt (), cFlex = lines[2][5..].ToInt ();
         double fThick = lines[3][5..].ToDouble ();
         var b0 = lines[4][6..].Split ('x').Select (int.Parse).Order ();

         bool bad = cEnts != model.Ents.Count;
         bad |= shmodel.Ents.OfType<E3Flat> ().Count () != cFlat;
         bad |= shmodel.Ents.OfType<E3Flex> ().Count () != cFlex;
         var b = model.Bound.DiagVector;
         int[] b1 = [b.X.RoundN (), b.Y.RoundN (), b.Z.RoundN ()];
         b1 = [.. b1.Order ()];
         bad |= !b0.SequenceEqual (b1);
         if (bad) Console.Write (" **BAD** ");
         else Console.Write (" .....");

      } catch (Exception e) {
         Move (name, "CRASH");
         WriteY (e.Message);
         return;
      }
      Console.WriteLine ();
   }

   // COMPARES with FLUX - if things pass here, move the file to TSHEETMETAL
   static void CompareSheetMetal (string name) {
      // Load the sheet metal model      
      var file = $"W:\\STEP\\{name}.stp";
      var sr = new STEPReader (file);
      Model3 model = sr.Load ();

      try {
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
         Console.WriteLine ($"{thick} {thick2} {area} {area2}");
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

   static void Move (string name, string dir) {
      Directory.CreateDirectory ($"W:\\STEP\\{dir}");
      File.Move ($"W:\\STEP\\{name}.stp", $"W:\\STEP\\{dir}\\{name}.stp");
   }
}
