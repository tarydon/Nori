// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TMiscIO.cs
// ║║║║╬║╔╣║ I/O tests for Curl, STL, T3X, OBJ, MESH
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO.Compression;

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

[Fixture (29, "Mesh IO", "IO.Mesh")]
class STLTests {
   [Test (137, "Read ASCII STL file")]
   void Test1 () {
      var pts = new STLReader (NT.File ("IO/MESH/ascii_cube.stl")).GetTriangles ();
      File.WriteAllText (NT.TmpTxt, pts.Select (a => a.R6 ().ToString ()).Aggregate ("", (a, b) => a + "\n" + b));
      Assert.TextFilesEqual (NT.File ("IO/MESH/ascii_cube.txt"), NT.TmpTxt);
   }

   [Test (138, "Reading Binary STL file")]
   void Test2 () {
      var pts = new STLReader (NT.File ("IO/MESH/binary_cube.stl")).GetTriangles ();
      (pts.Count == 36).IsTrue ();
      File.WriteAllText (NT.TmpTxt, pts.Select (a => a.R6 ().ToString ()).Aggregate ("", (a, b) => a + "\n" + b));
      Assert.TextFilesEqual (NT.File ("IO/MESH/binary_cube.txt"), NT.TmpTxt);
   }

   [Test (139, "Write ASCII STL file")]
   void Test3 () {
      var mesh = new STLReader (NT.File ("IO/MESH/ascii_cube.stl")).BuildMesh ();
      using (var sr = File.Create (NT.TmpTxt))
         STLWriter.WriteASCII (mesh, sr);
      Assert.TextFilesEqual (NT.File ("IO/MESH/asciicube_output.stl"), NT.TmpTxt);
   }

   [Test (140, "Write Binary STL file")]
   void Test4 () {
      var mesh = new STLReader (NT.File ("IO/MESH/ascii_cube.stl")).BuildMesh ();
      using (var sr = File.Create (NT.TmpTxt))
         STLWriter.WriteBinary (mesh, sr);
      Assert.TextFilesEqual (NT.File ("IO/MESH/binarycube_output.stl"), NT.TmpTxt);
   }

   [Test (159, "Load Mesh3 from OBJ, transform it")]
   void Test5 () {
      var zar = new ZipArchive (File.OpenRead (NT.File ("IO/MESH/cow.zip")));
      var ze = zar.GetEntry ("cow.obj")!;
      var zstm = new ZipReadStream (ze.Open (), ze.Length);
      var mesh = Mesh3.LoadObj (zstm.ReadAllLines ());
      mesh *= Matrix3.Rotation (EAxis.X, Lib.HalfPI) * Matrix3.Rotation (EAxis.Z, -Lib.HalfPI);
      mesh *= Matrix3.Translation (1, 2, 3);
      mesh.IsEmpty.IsFalse ();
      mesh.Bound.Is ("(0.72783~1.27201,1.01957~2.68977,2.41668~3.43965)");
      mesh.GetBound (Matrix3.Translation (0, 0, 1)).Is ("(0.72783~1.27201,1.01957~2.68977,3.41668~4.43965)");
      mesh.Vertex[0].Is ("(1.14103,1.61218,2.85889) <0.371,-0.76,-0.533>");
      File.WriteAllText (NT.TmpTxt, mesh.ToTMesh ());
      Assert.TextFilesEqual (NT.File ("IO/MESH/cow.tmesh"), NT.TmpTxt);
   }

   [Test (160, "Load Mesh3 from Flux .mesh format")]
   void Test6 () {
      var mesh = Mesh3.LoadFluxMesh (NT.File ("IO/MESH/Carriage.mesh"));
      File.WriteAllText (NT.TmpTxt, mesh.ToTMesh ());
      Assert.TextFilesEqual (NT.File ("IO/MESH/Carriage.tmesh"), NT.TmpTxt);
   }
}

[Fixture (31, "T3X import of primitives", "IO")]
class T3XTests {
   [Test (150, "Load CONE entity")]
   void Test1 () => Test ("CONE");

   [Test (151, "Load CYLINDER entity")]
   void Test2 () => Test ("CYLINDER");

   [Test (152, "Load NURB entity")]
   void Test3 () => Test ("NURB");

   [Test (153, "Load PLANE entity")]
   void Test4 () => Test ("PLANE");

   [Test (154, "Load RULEDSURFACE entity")]
   void Test5 () => Test ("RULEDSURFACE");

   [Test (155, "Load SPHERE entity")]
   void Test6 () => Test ("SPHERE");

   [Test (156, "Load SPUNSURFACE entity")]
   void Test7 () => Test ("SPUNSURFACE");

   [Test (157, "Load SWEPTSURFACE entity")]
   void Test8 () => Test ("SWEPTSURFACE");

   [Test (158, "Load TORUS entity")]
   void Test9 () => Test ("TORUS");

   void Test (string name) {
      var model = new T3XReader (NT.File ($"IO/T3X/{name}.t3x")).Load ();
      var surf = (E3Surface)model.Ents[0];
      surf.IsNormalFlipped = !surf.IsNormalFlipped;

      List<Point3> pts = [];
      var sb = new StringBuilder ();
      foreach (var con in surf.Contours) {
         var curves = con.Curves;
         foreach (var c in curves) {
            c.Discretize (pts, Lib.FineTess, Lib.FineTessAngle); pts.RemoveLast ();
            if (c is Line3) pts.Add (c.Start.Midpoint (c.End));
         }
      }
      List<Point2> uvs = [.. pts.Select (surf.GetUV)];
      List<Vector3> normal = [.. uvs.Select (p => surf.GetNormal (p.X, p.Y))];
      sb.AppendLine (surf.GetType ().Name);
      sb.AppendLine ($"Flags: {surf.Flags}");
      sb.AppendLine ($"Domain: {surf.Domain}");
      sb.AppendLine ($"Bound: {surf.Bound}");
      sb.AppendLine ($"Area: {surf.Area.Round (6)}");
      for (int i = 0; i < pts.Count; i++)
         sb.AppendLine ($"{i} {pts[i].R6 ()} {uvs[i].R6 ()} {normal[i].R6 ()}");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual (NT.File ($"IO/T3X/{name}.txt"), NT.TmpTxt);
   }
}
