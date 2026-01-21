// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TClean.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (27, "Poly Join Tests", "Geom.Poly")]
class PolyJoinTests {
   [Test (127, "Stitch JOIN1 (standard shapes) with 0.001")]
   void Test1 () => Test ("Join1", 0.001);

   [Test (128, "Stitch JOIN2 (4 lines meeting at corner) with 0.001")]
   void Test2 () => Test ("Join2", 0.002);

   [Test (129, "Stitch JOIN3 (Triangle with 2 gaps) with 0.21")]
   void Test3 () => Test ("Join3", 0.21);

   [Test (130, "Stitch JOIN4 (Gaps, Overlaps of 0.1) with 0.21")]
   void Test4 () => Test ("Join4", 0.21, 5);

   [Test (131, "Stitch JOIN5 (Huge drawing) with 0.0001")]
   void Test5 () {
      var dr = new DXFReader (NT.File ("Poly/Join/JOIN5.dxf")) { StitchThreshold = 0.0001 };
      var dwg = dr.Load ();
      dwg.Ents.Count.Is (15001);
   }

   void Test (string file, double threshold, double grid = 0) {
      var dr = new DXFReader (NT.File ($"Poly/Join/{file}.dxf")) { StitchThreshold = threshold };
      var dwg = dr.Load ();
      if (grid > 0) dwg.Grid = new (5, 1, true);
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual (NT.File ($"Poly/Join/{file}.curl"), NT.TmpCurl);
   }
}

[Fixture (33, "Additional DwgStitcher tests", "Geom.Poly")]
class PolyStitchTests {
   [Test (166, "No stitch must not update dwg")]
   void Test1 () {
      var (a, b) = (Poly.Line ((0, 0), (10, 0)), Poly.Line ((0, 10), (10, 10)));
      Dwg2 dwg = new ();
      dwg.Add (a); dwg.Add (b);
      using (dwg.Ents.Subscribe (a => Assert.IsTrue (false))) // Not expecting any change notifications
         new DwgStitcher (dwg).Process ();
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is (a);
      ((E2Poly)dwg.Ents[1]).Poly.Is (b);
   }
}
