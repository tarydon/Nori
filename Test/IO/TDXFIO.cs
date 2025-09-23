// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TDXFIO.cs
// ║║║║╬║╔╣║ DXF I/O tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (22, "Basic DXF Tests", "DXF")]
class DXFTests {
   [Test (75, "Basic DXF load test")]
   void Test1 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Basic.dxf"));
      dwg.Ents.Count.Is (57);
   }

   [Test (76, "DXF round-trip test")]
   void Test2 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Basic.dxf"));
      DXFWriter.SaveFile (dwg.Purge (), NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Basic.dxf", NT.TmpDXF);
   }

   [Test (77, "DXF round-trip test for point")]
   void Test3 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Point.dxf"));
      DXFWriter.SaveFile (dwg.Purge (), NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Point.dxf", NT.TmpDXF);
   }

   [Test (78, "DXF Reader test for POLYLINE entity")]
   void Test4 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Poly.dxf"));
      var firstPoly = dwg.Ents.First () as E2Poly;
      var secondPoly = dwg.Ents.Last () as E2Poly;
      dwg.Ents.Count.Is (2);
      Assert.IsTrue (firstPoly?.mPoly.Segs.Count () == 5 && secondPoly?.mPoly.Segs.Count () == 4 && secondPoly.mPoly.IsClosed);
   }

   [Test (79, "DXF round-trip test for ellipse")]
   void Test5 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Ellipse.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Ellipse.dxf", NT.TmpDXF);
   }

   [Test (80, "DXF Writer test for POLYLINE entity")]
   void Test6 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Poly.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Poly.dxf", NT.TmpDXF);
   }

   [Test (81, "DXF round-trip test for TEXT")]
   void Test7 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Text.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Text.dxf", NT.TmpDXF);
   }

   [Test (82, "DXF color to Pix color conversion test")]
   void Test8 () {
      // Check for the colors at the boundaries
      Assert.IsTrue (DXFReader.GetColor (0).EQ (Color4.Black));
      Assert.IsTrue (DXFReader.GetColor (255).EQ (Color4.White));
      // Check for any number in the range
      Assert.IsTrue (DXFReader.GetColor (128).EQ (new Color4 (255, 0, 79, 59)));
      // Check for cases when the given number is outside the range
      Assert.IsTrue (DXFReader.GetColor (256).EQ (Color4.Nil));
      Assert.IsTrue (DXFReader.GetColor (-5).EQ (Color4.Black));
      Assert.IsTrue (DXFReader.GetColor (260).EQ (Color4.White));
   }

   [Test (83, "DXF Reader test for LWPOLYLINE entity")]
   void Test9 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/LWPolyline.dxf"));
      var firstPoly = dwg.Ents.First () as E2Poly;
      var secondPoly = dwg.Ents.Last () as E2Poly;
      dwg.Ents.Count.Is (2);
      Assert.IsTrue (firstPoly?.mPoly.Segs.Count () == 4 && secondPoly?.mPoly.Segs.Count () == 2 && firstPoly.mPoly.IsClosed);
   }

   [Test (84, "DXF test for MTEXT")]
   void Test10 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/MText.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/MText.dxf", NT.TmpDXF);
   }

   [Test (85, "Issue.36 POLYLINE entity is not rendered correctly")]
   void Test11 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/PolyErr.dxf"));
      dwg.Purge ();
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/PolyErr.dxf", NT.TmpDXF);
   }

   [Test (86, "DXF round-trip test for layers")]
   void Test12 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Layers.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Layers.dxf", NT.TmpDXF);
   }

   [Test (87, "Pix color to DXF Color conversion test")]
   void Test13 () {
      // Standard values
      var colors = new Color4[] { Color4.White, Color4.Black, Color4.Yellow };
      colors.ForEach (c => Assert.IsTrue (DXFReader.GetColor (DXFWriter.ToACADColor (c)).EQ (c)));
      // Random values
      var random = new Color4 (255, 0, 0, 2);
      Assert.IsTrue (DXFReader.GetColor (DXFWriter.ToACADColor (random)).EQ (Color4.Black));
      random = Color4.Transparent;
      Assert.IsTrue (DXFReader.GetColor (DXFWriter.ToACADColor (random)).EQ (Color4.White));
      random = Color4.Nil;
      Assert.IsTrue (DXFReader.GetColor (DXFWriter.ToACADColor (random)).EQ (Color4.Black));
      random = new (260, 247, -1);
      Assert.IsTrue (DXFReader.GetColor (DXFWriter.ToACADColor (random)).EQ (Color4.Cyan));
   }

   [Test (88, "Test for converting encoded texts to special characters")]
   void Test14 () {
      (string Key, string Value)[] textMap = [("", "" ), ("Normal", "Normal"), ("99%", "99%"), ("99%%", "99%%"),
         ("%%", "%%"), ("%%99", "%%99"), ("18%%dN", "18°N"), ("18%%d", "18°"), ("18% dense", "18% dense"),
         ("35%%p0.1", "35±0.1"), ("12%%%p0.2%", "12%±0.2%"), ("%%p0.1", "±0.1"),  ("%%d", "°"), ("%%d%%c%%p", "°∅±")];
      foreach (var (Key, Value) in textMap) DXFReader.Clean (Key).Is (Value);
   }

   [Test (89, "Font selection doesn't seem to work for the attached MTEXT file")]
   void Test15 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/3Horns.dxf"));
      var mText = dwg.Ents.OfType<E2Text> ();
      var sb = new StringBuilder ();
      foreach (var txt in mText)
         sb.AppendLine (txt.Text);
      var tmp = NT.TmpTxt;
      File.WriteAllText (tmp, sb.ToString ());
      // Compare the generated text file with the expected text file
      Assert.TextFilesEqual1 ("IO/DXF/Out/3Horns.txt", tmp);
   }

   [Test (90, "DXF round-trip test for solid")]
   void Test16 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Solid.dxf"));
      dwg.Purge ();
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Solid.dxf", NT.TmpDXF);
   }

   [Test (91, "Color selection based on their priority ")]
   void Test17 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Color1.dxf"));
      DXFWriter.SaveFile (dwg.Purge (), NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Color1.dxf", NT.TmpDXF);
   }

   [Test (92, "DXF round-trip test for INSERT")]
   void Test18 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Block01.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Block01.dxf", NT.TmpDXF);
   }

   [Test (93, "Pix crashes when trying to import the attached DXF files")]
   void Test19 () {
      var files = new[] { "C36249_B", "47458206_B", "47616434", "48142366_A","AX0974", "337228A2" };
      foreach (var name in files) {
         var dwg = DXFReader.FromFile (NT.File ($"IO/DXF/{name}.dxf"));
         DXFWriter.SaveFile (dwg.Purge (), NT.TmpDXF);
         Assert.TextFilesEqual1 ($"IO/DXF/Out/{name}.dxf", NT.TmpDXF);
      }
   }

   [Test (94, "Text alignment flags")]
   void Test20 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/TextAlign.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/TextAlign.dxf", NT.TmpDXF);
   }

   [Test (106, "Test for BendLine")]
   public void Test21 () {
      var dwg = new Dwg2 ();
      dwg.Add (Poly.Rectangle (0, 0, 60, 50));
      dwg.Add (new E2Bendline (dwg, Point2.List (40, 0, 40, 50), Lib.HalfPI, 2, 0.42, 1));
      dwg.Add (new E2Bendline (dwg, Point2.List (20, 0, 20, 50), -Lib.HalfPI, 2, 0.42, 1));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/BendLine.dxf", NT.TmpDXF);
   }
}

[Fixture (5, "Next set of DXF tests", "DXF")]
class DXFTests2 {
   [Test (95, "Reading ATTRIB entities")]
   void Test1 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/D00537.dxf"));
      CurlWriter.ToFile (dwg.Purge (), NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D00537.curl", NT.TmpCurl);
   }

   [Test (96, "Reading attributes of MTEXT")]
   void Test2 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/D01273.dxf"));
      CurlWriter.ToFile (dwg.Purge (), NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D01273.curl", NT.TmpCurl);
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D01273.dxf", NT.TmpDXF);
   }

   [Test (97, "Test for Codepage 1252")]
   void Test3 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/D58839.dxf"));
      CurlWriter.ToFile (dwg.Purge (), NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D58839.curl", NT.TmpCurl);
   }

   [Test (98, "Test for ShapeRecognizer")]
   void Test4 () {
      List<string> shapes = [];
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Shapes1.dxf"));
      foreach (var poly in dwg.Polys) {
         var sd = ShapeRecognizer.Recognize (poly);
         if (sd.Shape != EShape.None) shapes.Add (sd.ToString ());
      }
      File.WriteAllLines (NT.TmpTxt, shapes);
      Assert.TextFilesEqual1 ("IO/DXF/Shapes1.txt", NT.TmpTxt);
   }

   [Test (108, "Issue.69: DXFReader doesn't recognize layers correctly")]
   void Test5 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Layer.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Layer.dxf", NT.TmpDXF);
   }

   [Test (105, "Issue.73: Extract bend information from the DXF special text entities")]
   void Test6 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/Bend-10.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Bend-10.curl", NT.TmpCurl);
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/Bend-10.dxf", NT.TmpDXF);
   }

   [Test (107, "Extend DXFReader to read bend line information from DXF")]
   void Test7 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/BasicBend.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/BasicBend.curl", NT.TmpCurl);
   }

   [Test (109, "Write out a BendLine with multiple segments")]
   void Test8 () {
      var dwg = new Dwg2 ();
      var layer = dwg.CurrentLayer;
      dwg.Add (new E2Poly (layer, Poly.Rectangle (0, 0, 100, 50)));
      dwg.Add (new E2Poly (layer, Poly.Rectangle (10, 10, 90, 40)));
      dwg.Add (new E2Bendline (dwg, [new (0, 30), new (10, 30), new (90, 30), new (100, 30)], Lib.HalfPI, 2.5, 0.42, 2.5));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/BendSeg.dxf", NT.TmpDXF);
   }

   [Test (116, "Read SPLINE entity from DXF")]
   void Test9 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/D17616.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D17616.curl", NT.TmpCurl);
      dwg = DXFReader.FromFile (NT.File ("IO/DXF/D17666.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D17666.curl", NT.TmpCurl);
   }

   [Test (117, "Write SPLINE entity to DXF")]
   void Test10 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/D17666.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D17666.dxf", NT.TmpDXF);
      dwg = DXFReader.FromFile (NT.File ("IO/DXF/D17292.dxf"));
      DXFWriter.SaveFile (dwg, NT.TmpDXF);
      Assert.TextFilesEqual1 ("IO/DXF/Out/D17292.dxf", NT.TmpDXF);
   }

   [Test (118, "Test of Ent2.XFormed")]
   void Test11 () {
      var dwg = DXFReader.FromFile (NT.File ("IO/DXF/AllEnts.dxf"));
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/AllEnts1.curl", NT.TmpCurl);
      var xfm = Matrix2.Rotation (45.D2R ()) * Matrix2.Scaling (2);
      for (int i = 0; i < dwg.Ents.Count; i++)
         dwg.Ents[i] = dwg.Ents[i].XFormed (xfm);
      CurlWriter.ToFile (dwg, NT.TmpCurl);
      Assert.TextFilesEqual1 ("IO/DXF/Out/AllEnts2.curl", NT.TmpCurl);
   }
}
