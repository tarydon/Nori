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

   void Test (string file, double threshold) {
      var dr = new DXFReader (NT.File ($"Poly/Join/{file}.dxf")) { StitchThreshold = threshold };
      var dwg = dr.Load ();
      DXFWriter.Save (dwg, "c:/etc/output.dxf");
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual (NT.File ($"Poly/Join/{file}.curl"), NT.TmpCurl);
   }
}
