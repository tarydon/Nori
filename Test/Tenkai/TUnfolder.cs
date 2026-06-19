// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TUnfolder.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (43, "Sheetmetalizer + Unfolder tests", "Unfolder")]
class TUnfolder {
   [Test (256, "S00178.stp | Planes=2, Flexes=1, Poly2=2")]
   void Test1 () => Test ("STEP/S00178.stp");

   [Test (257, "S00143.stp | Planes=8, Flexes=7, Polys=14")]
   void Test2 () => Test ("STEP/S00143.stp");

   [Test (258, "S00176.stp | Planes=17, Flexes=16, Polys=4")]
   void Test3 () => Test ("STEP/S00176.stp");

   [Test (259, "S00666.stp | Planes=3, Flexes=2, Polys=3")]
   void Test5 () => Test ("STEP/S00666.stp");

   [Test (260, "S01004 | Planes=8, Flexes=7, Polys=11")]
   void Test6 () => Test ("STEP/S01004.stp");

   [Test (261, "S06298 | Planes=11, Flexes=10, Polys=2")]
   void Test7 () => Test ("STEP/S06298.stp");
   
   void Test (string file) {
      var sb = new StringBuilder ();
      var model = new STEPReader (NT.File (file)).Load ();
      sb.Append ($"Surface model: {model.Ents.Count} surfaces, {S1 (model.Bound)}\n");
      var shModel = new SheetMetalizer (model).Process ().Value;
      int cFlats = shModel.Ents.Count (a => a is E3Flat);
      int cFlexes = shModel.Ents.Count (a => a is E3Flex);
      sb.Append ($"Sheet metal model: {cFlats} planes, {cFlexes} flexes, {S1 (shModel.Bound)}\n");
      var dwg = new Unfolder (shModel).Process ().Value;
      int cPoly = dwg.Ents.Count (a => a is E2Poly);
      int cBends = dwg.Ents.Count (a => a is E2Bendline);
      sb.Append ($"Drawing: {cPoly} polys, {cBends} bends, {S1 (dwg.Bound)}\n");

      string root = Path.GetFileNameWithoutExtension (file);
      var outtxt = NT.File ($"Tenkai/Unfold/{root}.txt");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual (outtxt, NT.TmpTxt);

      var outcurl = NT.File ($"Tenkai/Unfold/{root}.curl");
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual (outcurl, NT.TmpCurl);
   }

   static string S1 (Bound3 b) {
      double dx = Math.Round (b.X.Length), dy = Math.Round (b.Y.Length), dz = Math.Round (b.Z.Length);
      double x = Math.Round (b.X.Min), y = Math.Round (b.Y.Min), z = Math.Round (b.Z.Min);
      return $"{dx}x{dy}x{dz}@{x},{y},{z}";
   }

   static string S1 (Bound2 b) {
      double dx = Math.Round (b.X.Length), dy = Math.Round (b.Y.Length);
      double x = Math.Round (b.X.Min), y = Math.Round (b.Y.Min);
      return $"{dx}x{dy}@{x},{y}";
   }
}
