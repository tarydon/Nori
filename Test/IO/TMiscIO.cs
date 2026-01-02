// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TMiscIO.cs
// ║║║║╬║╔╣║ I/O tests for Curl, STL, T3X
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (24, "CURL tests for Dwg", "IO")]
class CurlTests {
   [Test (99, "E2Text entity: alignment")]
   void Test1 () {
      var dwg = DXFReader.Load (NT.File ("IO/DXF/TextAlign.dxf"));
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/TextAlign.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/TextAlign.curl");
   }

   [Test (100, "E2Insert entity, Block2")]
   void Test2 () {
      var dwg = DXFReader.Load (NT.File ("IO/DXF/Block01.dxf"));
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/Block01.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/Block01.curl");
   }

   [Test (101, "E2Point test")]
   void Test3 () {
      var dwg = DXFReader.Load (NT.File ("IO/DXF/Point.dxf"));
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("IO/DXF/Out/Point.curl", NT.TmpCurl);
      RoundTrip ("IO/DXF/Out/Point.curl");
   }

   void RoundTrip (string file) {
      if (!Path.IsPathRooted (file)) file = NT.File (file);
      var obj = CurlReader.Load (file);
      CurlWriter.Save (obj, NT.TmpCurl);
      Assert.TextFilesEqual (file, NT.TmpCurl);
   }
}

[Fixture (29, "STL mesh read/write", "STL")]
class STLTests {
   [Test (137, "Read ASCII STL file")]
   void Test1 () {
      var pts = new STLReader (NT.File ("IO/STL/ascii_cube.stl")).GetTriangles ();
      File.WriteAllText (NT.TmpTxt, pts.Select (a => a.R6 ().ToString ()).Aggregate ("", (a, b) => a + "\n" + b));
      Assert.TextFilesEqual (NT.File ("IO/STL/ascii_cube.txt"), NT.TmpTxt);
   }

   [Test (138, "Reading Binary STL file")]
   void Test2 () {
      var pts = new STLReader (NT.File ("IO/STL/binary_cube.stl")).GetTriangles ();
      (pts.Count == 36).IsTrue ();
      File.WriteAllText (NT.TmpTxt, pts.Select (a => a.R6 ().ToString ()).Aggregate ("", (a, b) => a + "\n" + b));
      Assert.TextFilesEqual (NT.File ("IO/STL/binary_cube.txt"), NT.TmpTxt);
   }

   [Test (139, "Write ASCII STL file")]
   void Test3 () {
      var mesh = new STLReader (NT.File ("IO/STL/ascii_cube.stl")).BuildMesh ();
      using (var sr = File.Create (NT.TmpTxt))
         STLWriter.WriteASCII (mesh, sr);
      Assert.TextFilesEqual (NT.File ("IO/STL/asciicube_output.stl"), NT.TmpTxt);
   }

   [Test (140, "Write Binary STL file")]
   void Test4 () {
      var mesh = new STLReader (NT.File ("IO/STL/ascii_cube.stl")).BuildMesh ();
      using (var sr = File.Create (NT.TmpTxt))
         STLWriter.WriteBinary (mesh, sr);
      Assert.TextFilesEqual (NT.File ("IO/STL/binarycube_output.stl"), NT.TmpTxt);
   }
}

class T3XTests {
}