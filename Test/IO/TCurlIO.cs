namespace Nori.Testing;

[Fixture (4, "CURL tests for Dwg", "IO")]
class CurlTests {
   [Test (62, "E2Text entity: alignment")]
   void Test1 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/TextAlign.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/TextAlign.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/TextAlign.curl");
   }

   [Test (63, "E2Insert entity, Block2")]
   void Test2 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Block01.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/Block01.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/Block01.curl");
   }

   [Test (64, "E2Point test")]
   void Test3 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Point.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/Point.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/Point.curl");
   }

   void RoundTrip (string file) {
      if (!Path.IsPathRooted (file)) file = NT.File (file);
      var obj = CurlReader.FromFile (file);
      CurlWriter.ToFile (obj, NT.TmpCurl);
      Assert.TextFilesEqual (file, NT.TmpCurl);
   }
}
