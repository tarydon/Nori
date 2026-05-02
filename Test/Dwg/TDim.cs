// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TDim.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (42, "Tests for Dimension entities", "Dwg")]
class DimEntTests () {
   [Test (244, "Test Dim3PAngle dimensions")]
   void Test1 () => Test (MakeDim3PAngle (), "Dim3PAngle", new (880, 600));

   [Test (245, "Test Radius dimensions")]
   void Test2 () => Test (MakeDimRad (), "DimRad", new (1616, 918));

   [Test (246, "Test Diameter dimensions")]
   void Test3 () => Test (MakeDimDia (), "DimDia", new (1360, 918));

   [Test (247, "Test DimAngle dimensions")]
   void Test4 () => Test (MakeDimAngle (), "DimAngle", new (880, 600));

   [Test (248, "Test DimAligned dimensions")]
   void Test5 () => Test (MakeDimAligned (), "DimAligned", new (1616, 1101));

   void Test (Dwg2 dwg, string name, Vec2S size) {
      // Test saving curl files
      var bound = dwg.Bound.InflatedF (1.05);
      string curl = NT.File ($"Dwg/Dim/{name}.curl");
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual (curl, NT.TmpCurl);

      // Test saving DXF files
      string dxf = Path.ChangeExtension (curl, ".dxf");
      DXFWriter.Save (dwg, NT.TmpDXF, true);
      Assert.TextFilesEqual (dxf, NT.TmpDXF);

      // Round trip DXF file
      var dwg2 = DXFReader.Load (dxf, true);
      CurlWriter.Save (dwg2, NT.TmpCurl);
      string curl2 = NT.File ($"Dwg/Dim/{name}-alt.curl");
      Assert.TextFilesEqual (curl2, NT.TmpCurl);

      // Save and test PNG files      
      string png = Path.ChangeExtension (curl, ".png");
      var scene = new Scene2 { Bound = bound, BgrdColor = Color4.White, Root = new Dwg2VN (dwg) };
      //int cx = (int)(bound.Width * 7), cy = (int)(bound.Height * 7);
      //Console.WriteLine ($"{cx} x {cy}");
      var dib = scene.RenderImage (size, DIBitmap.EFormat.Gray8);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual (png, NT.TmpPNG, dib);
   }

   static Dwg2 MakeDimAligned () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/DimAligned-Blank.dxf"), true);
      var tstyle = dwg.GetStyle ("STANDARD")!;
      DimStyle2 style; double dx, dy;

      dx = 0; dy = 0;
      style = new DimStyle2 ("BREAK", tstyle);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff ();

      dx = 65; dy = 0;
      style = new DimStyle2 ("ABOVE", tstyle) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff ();

      dx = 130; dy = 0;
      style = new DimStyle2 ("BELOW", tstyle) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff ();

      dx = 0; dy = 65;
      style = new DimStyle2 ("HORZ-BREAK", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff ();

      dx = 0; dy = 0;
      style = new DimStyle2 ("HORZ-ABOVE", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff2 ();

      dx = 65; dy = 0;
      style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      AddStuff2 ();
      return dwg;

      void AddStuff () {
         Add (0, 50, 20, 50, 9, 54); Add (20, 50, 50, 35, 47, 41);
         Add (50, 0, 50, 35, 54, 16); Add (15, 0, 50, 0, 39, -4);
         Add (15, 0, 15, 10, 19, 11); Add (15, 10, 0, 10, -1, 14);
         Add (8, 10, 8, 50, 8, 30); Add (20, 25, 30, 25, 41, 32);
         Add (25, 20, 25, 25, 15, 22); Add (25, 20, 25, 25.67, 35, 22);
         Add (15, 0, 0, 10, 0, 3);
      }

      void AddStuff2 () {
         Add (65, 115, 85, 115, 64, 119); Add (65, 115, 85, 115, 86, 123);
         Add (115, 100, 115, 65, 112, 70); Add (115, 100, 115, 65, 118, 63);
         Add (65, 75, 80, 75, 74, 78); Add (85, 115, 115, 100, 80, 109);
         Add (85, 115, 115, 100, 115, 105); Add (85, 90, 90, 90, 86, 98);
         Add (85, 90, 87, 90, 85.9, 82); Add (95, 90, 93.75, 90, 94.9, 82);
         Add (80, 65, 115, 65, 100, 69); Add (65, 75, 80, 65, 80, 61);
      }

      void Add (params double[] vals) {
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.Add (new E2DimAligned (dwg.CurrentLayer, dwg.CurrentDimStyle, pts));
      }
   }

   static Dwg2 MakeDimAngle () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/DimAngle-Blank.dxf"), true);
      var tstyle = dwg.GetStyle ("STANDARD")!;
      var layer = new Layer2 ("DIMENSION", Color4.Black, ELineType.Continuous);
      dwg.Add (layer); dwg.CurrentLayer = layer;

      double dx = 0, dy = 0;
      var style = new DimStyle2 ("BREAK", tstyle);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 26, 21); Add (35, 20, 30, 30, 29, 24);
      Add (35, 20, 30, 30, 30, 29); Add (35, 20, 30, 30, 37, 23);
      Add (35, 20, 30, 30, 40, 29); Add (35, 20, 30, 30, 44, 34);

      dx = 35; dy = 0;
      style = new DimStyle2 ("ABOVE", tstyle) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 26, 21); Add (35, 20, 30, 30, 29, 24);
      Add (35, 20, 30, 30, 32, 29); Add (35, 20, 30, 30, 39, 23);
      Add (35, 20, 30, 30, 42, 29); Add (35, 20, 30, 30, 49, 29);

      dx = 70; dy = 0;
      style = new DimStyle2 ("BELOW", tstyle) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 26, 21); Add (35, 20, 30, 30, 30, 24);
      Add (35, 20, 30, 30, 32, 29); Add (35, 20, 30, 30, 39, 23);
      Add (35, 20, 30, 30, 42, 29); Add (35, 20, 30, 30, 49, 29);

      dx = 0; dy = 40;
      style = new DimStyle2 ("HORZ-BREAK", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 21.9, 20.5); Add (35, 20, 30, 30, 23, 22);
      Add (35, 20, 30, 30, 28.7, 24.2); Add (35, 20, 30, 30, 32.6, 24.1);
      Add (35, 20, 30, 30, 32.0, 30.2); Add (35, 20, 30, 30, 36.8, 30.4);

      dx = 35; dy = 40;
      style = new DimStyle2 ("HORZ-ABOVE", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 22.7, 21.4); Add (35, 20, 30, 30, 24.6, 20.4);
      Add (35, 20, 30, 30, 29.2, 22.4); Add (35, 20, 30, 30, 31, 28);
      Add (35, 20, 30, 30, 35.5, 24.3); Add (35, 20, 30, 30, 37, 33);

      dx = 70; dy = 40;
      style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (35, 20, 30, 30, 22.7, 21.4); Add (35, 20, 30, 30, 24.6, 20.4);
      Add (35, 20, 30, 30, 29.2, 22.4); Add (35, 20, 30, 30, 31, 28);
      Add (35, 20, 30, 30, 35.5, 24.3); Add (35, 20, 30, 30, 37, 33);
      Add (35, 20, 30, 30, 44, 37);
      return dwg;

      // Helpers ...........................................
      void Add (params double[] vals) {
         List<Point2> pts = [];
         var input = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         if (dwg.PickPoly (input[0], 5, out var tp1)) pts.AddM (tp1.Poly.A, tp1.Poly.B);
         if (dwg.PickPoly (input[1], 5, out var tp2)) pts.AddM (tp2.Poly.A, tp2.Poly.B);
         pts.Add (input[2]);
         dwg.Add (new E2DimAngle (dwg.CurrentLayer, dwg.CurrentDimStyle, pts));
      }
   }

   static Dwg2 MakeDim3PAngle () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/Dim3PAngle-Blank.dxf"), true);
      var tstyle = dwg.GetStyle ("STANDARD")!;
      var layer = new Layer2 ("DIMENSION", Color4.Black, ELineType.Continuous);
      dwg.Add (layer); dwg.CurrentLayer = layer;

      double dx = 0, dy = 0;
      var style = new DimStyle2 ("BREAK", tstyle);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (22.9, 21.7); Add (26, 21); Add (29, 24);
      Add (30, 29); Add (37, 23); Add (40, 29);

      dx = 35; dy = 0;
      style = new DimStyle2 ("ABOVE", tstyle) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (22, 21); Add (26, 21); Add (29, 24);
      Add (30, 29); Add (37, 23); Add (40, 29);

      dx = 70; dy = 0;
      style = new DimStyle2 ("BELOW", tstyle) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (22, 21); Add (26, 21); Add (29, 24);
      Add (31, 29); Add (37, 23); Add (40, 29);

      dx = 0; dy = 40;
      style = new DimStyle2 ("HORZ-BREAK", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (21.9, 20.5); Add (23, 22); Add (28.7, 24.2);
      Add (32.6, 24.1); Add (32.0, 30.2); Add (36.8, 30.4);

      dx = 35; dy = 40;
      style = new DimStyle2 ("HORZ-ABOVE", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (22.7, 21.4); Add (24.6, 20.4); Add (29.2, 22.4);
      Add (31, 28); Add (35.5, 24.3); Add (37, 33);

      dx = 70; dy = 40;
      style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (22.7, 21.4); Add (24.6, 20.4); Add (29.2, 22.4); Add (31, 28);
      Add (35.5, 24.3); Add (37, 33); Add (44, 37);
      return dwg;

      void Add (double x, double y) {
         List<double> vals = [];
         vals.AddM (20, 20, 35, 20, 30, 30, x, y);
         var input = Point2.List ([.. vals]).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.Add (new E2Dim3PAngle (dwg.CurrentLayer, dwg.CurrentDimStyle, input));
      }
   }

   static Dwg2 MakeDimDia () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/DimDia-Blank.dxf"), true);
      var tstyle = dwg.GetStyle ("STANDARD")!;
      DimStyle2 style; double dx, dy;

      dx = 0; dy = 0;
      style = new DimStyle2 ("BREAK", tstyle);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      dx = 60; dy = 0;
      style = new DimStyle2 ("ABOVE", tstyle) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      dx = 120; dy = 0;
      style = new DimStyle2 ("BELOW", tstyle) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      dx = 0; dy = 60;
      style = new DimStyle2 ("HORZ-BREAK", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      dx = 60; dy = 60;
      style = new DimStyle2 ("HORZ-ABOVE", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      dx = 120; dy = 60;
      style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 14); Add (false, 11, 11);
      Add (false, 40, 40); Add (false, 53, 53);
      Add (true, 49, 12); Add (true, 51, 23);

      return dwg;

      // Header ............................................
      void Add (bool tofl, double x, double y) {
         double[] vals = [12.3, 12.3, x, y];
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg]; pts[0] = seg.Center;
         dwg.Add (new E2DimDia (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }

   static Dwg2 MakeDimRad () {
      var dwg = DXFReader.Load ("N:/TData/Dwg/Dim/DimRad-Blank.dxf");
      var tstyle = dwg.GetStyle ("STANDARD")!;
      DimStyle2 style; double dx, dy;

      dx = 0; dy = 0;
      style = new DimStyle2 ("BREAK", tstyle);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 68, 55); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 56, 53);

      dx = 70; dy = 0;
      style = new DimStyle2 ("ABOVE", tstyle) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 70, 57); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 56, 53);

      dx = 140; dy = 0;
      style = new DimStyle2 ("BELOW", tstyle) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 70, 57); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 56, 53);

      dx = 0; dy = 55;
      style = new DimStyle2 ("HORZ-BREAK", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 68, 55); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 52, 55);

      dx = 70; dy = 55;
      style = new DimStyle2 ("HORZ-ABOVE", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 68, 55); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 52, 55);

      dx = 140; dy = 55;
      style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 14, 15, 12, 13); Add (false, 14, 15, 14, 19);
      Add (false, 54, 52, 63, 38); Add (false, 54, 52, 64, 44);
      Add (false, 54, 52, 68, 55); Add (true, 54, 52, 62, 30);
      Add (true, 54, 52, 52, 55);

      return dwg;

      // Header ............................................
      void Add (bool tofl, params double[] vals) {
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg];
         pts[0] = seg.Center;
         dwg.Add (new E2DimRad (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }
}
