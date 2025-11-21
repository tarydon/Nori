// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TSTLIO.cs
// ║║║║╬║╔╣║ STL I/O tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

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