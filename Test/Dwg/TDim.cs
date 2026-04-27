// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TDim.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.ComponentModel.Design.Serialization;

namespace Nori.Testing;

[Fixture (42, "Tests for Dimension entities", "Dwg")]
class DimEntTests () {
   [Test (244, "Test 3-P Angular dimensions")]
   void Test1 () => Test (MakeDim3PAngle (), "Dim3PAngle", new (1308, 800));

   [Test (245, "Test Radius dimensions")]
   void Test2 () => Test (MakeDimRad (), "DimRad", new (1328, 752));

   [Test (246, "Test Diameter dimensions")]
   void Test3 () => Test (MakeDimDia (), "DimDia", new (1140, 727));

   void Test (Dwg2 dwg, string name, Vec2S size) {
      var bound = dwg.Bound.InflatedF (1.05);
      string curl = NT.File ($"Dwg/Dim/{name}.curl");
      CurlWriter.Save (dwg, curl);
      string dxf = Path.ChangeExtension (curl, ".dxf");
      DXFWriter.Save (dwg, dxf, true);
      
      string png = Path.ChangeExtension (curl, ".png");
      var scene = new Scene2 { Bound = bound, BgrdColor = Color4.White, Root = new Dwg2VN (dwg) };
      int cx = (int)(bound.Width * 4.618), cy = (int)(bound.Height * 4.618);
      var dib = scene.RenderImage (size, DIBitmap.EFormat.Gray8);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual (png, NT.TmpPNG, dib);
   }

   static Dwg2 MakeDim3PAngle () {
      Dwg2 dwg = new ();
      var layer = dwg.CurrentLayer;
      var style = dwg.CurrentDimStyle; var s = style;
      dwg.Add (Poly.Rectangle (10, 5, 280, 170));
      Add (20, 20, 40, 20, 34, 34, 40, 32); Add (50, 20, 80, 20, 70, 40, 70, 30);
      Add (80, 20, 105, 20, 100, 40, 97, 28); Add (110, 20, 130, 20, 125, 35, 121, 22);
      Add (140, 20, 160, 20, 155, 35, 149, 25); Add (170, 20, 185, 20, 180, 30, 177, 24);
      Add (200, 20, 215, 20, 210, 30, 206, 21);

      DimStyle2 style4 = new ("BELOW", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 4, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style4); dwg.CurrentDimStyle = style4;
      Add (20, 50, 40, 50, 34, 64, 40, 62); Add (50, 50, 80, 50, 70, 70, 70, 60);
      Add (80, 50, 105, 50, 100, 70, 97, 58); Add (110, 50, 130, 50, 125, 65, 121, 52);
      Add (140, 50, 160, 50, 155, 65, 149, 55); Add (170, 50, 185, 50, 180, 60, 177, 54);
      Add (200, 50, 215, 50, 210, 60, 206, 51);

      DimStyle2 style3 = new ("ABOVE", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 1, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style3); dwg.CurrentDimStyle = style3;
      Add (20, 80, 40, 80, 34, 94, 40, 92); Add (50, 80, 80, 80, 70, 100, 70, 90);
      Add (80, 80, 105, 80, 100, 100, 97, 88); Add (110, 80, 130, 80, 125, 95, 121, 82);
      Add (140, 80, 160, 80, 155, 95, 149, 85); Add (170, 80, 185, 80, 180, 90, 177, 84);
      Add (200, 80, 215, 80, 210, 90, 206, 81);

      DimStyle2 style2 = new ("HORZ", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, true, true, s.TOFL, (int)s.TextPos, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style2); dwg.CurrentDimStyle = style2;
      Add (20, 110, 40, 110, 34, 124, 40, 122); Add (50, 110, 80, 110, 70, 130, 70, 120);
      Add (80, 110, 105, 110, 100, 130, 89, 113); Add (110, 110, 130, 110, 125, 125, 121, 112);
      Add (140, 110, 160, 110, 155, 125, 149, 115); Add (170, 110, 185, 110, 180, 120, 176, 112);
      Add (200, 110, 215, 110, 210, 120, 205, 114);

      DimStyle2 style5 = new ("MIXED", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, false, true, s.TOFL, 1, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style5); dwg.CurrentDimStyle = style5;
      Add (20, 140, 40, 140, 34, 154, 40, 152); Add (50, 140, 80, 140, 70, 160, 70, 150);
      Add (80, 140, 105, 140, 100, 160, 97, 148); Add (110, 140, 130, 140, 125, 155, 121, 142);
      Add (140, 140, 160, 140, 155, 155, 149, 145); Add (170, 140, 185, 140, 180, 150, 177, 144);
      Add (200, 140, 215, 140, 210, 150, 206, 141);

      dwg.CurrentDimStyle = style3;
      Add (245, 30, 255, 30, 245, 40, 260, 45); Add (245, 30, 245, 40, 235, 30, 231, 44);
      Add (245, 30, 235, 30, 245, 20, 232, 17); Add (245, 30, 245, 20, 255, 30, 257, 18);
      dwg.CurrentDimStyle = style;
      Add (245, 80, 245, 70, 238, 87, 255, 90); Add (245, 120, 255, 130, 255, 110, 233, 120);
      Add (245, 120, 255, 130, 255, 110, 263, 120);

      void Add (params double[] vals)
         => dwg.Add (new E2Dim3PAngle (layer, dwg.CurrentDimStyle, Point2.List (vals)));
      return dwg;
   }

   static Dwg2 MakeDimDia () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/DimDia-Blank.dxf"));

      double dx = 0, dy = 0;
      var style = new DimStyle2 ("BREAK", dwg.GetStyle ("STANDARD")!);
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);

      dx = 75; dy = 0;
      style = new DimStyle2 ("ABOVE", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);

      dx = 150;
      style = new DimStyle2 ("BELOW", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);

      dx = 0; dy = 70;
      style = new DimStyle2 ("HORZ", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);

      dx = 75; dy = 70;
      style = new DimStyle2 ("HORZABOVE", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);

      dx = 150; dy = 70;
      style = new DimStyle2 ("HORZBELOW", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 79, 58, 82, 58); Add (false, 79, 58, 75, 60);
      Add (false, 79, 58, 80, 72); Add (false, 79, 58, 35, 36);
      Add (true, 79, 58, 27, 55); Add (true, 79, 58, 78, 26);
      return dwg;

      void Add (bool tofl, params double[] vals) {
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg];
         pts[0] = seg.Center;
         dwg.Add (new E2DimDia (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }

   static Dwg2 MakeDimRad () {
      var dwg = DXFReader.Load (NT.File ("Dwg/Dim/DimRad-Blank.dxf"));
      dwg.Add (new DimStyle2 ("BREAK", dwg.GetStyle ("STANDARD")!));
      dwg.CurrentDimStyle = dwg.GetDimStyle ("BREAK");
      Add (false, 70, 56, 81, 68); Add (false, 70, 56, 65, 43);
      Add (true, 70, 56, 54, 52); Add (false, 22, 13, 19, 19);
      Add (false, 22, 13, 15, 15); Add (true, 79, 12, 76, 14);

      var style = new DimStyle2 ("ABOVE", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 160, 56, 171, 68); Add (false, 160, 56, 155, 43);
      Add (true, 160, 56, 144, 52); Add (false, 112, 13, 109, 19);
      Add (false, 112, 13, 105, 15); Add (true, 169, 12, 166, 14);

      style = new DimStyle2 ("BELOW", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 250, 56, 261, 68); Add (false, 250, 56, 245, 43);
      Add (true, 250, 56, 234, 52); Add (false, 202, 13, 199, 19);
      Add (false, 202, 13, 195, 15); Add (true, 259, 12, 256, 14);

      style = new DimStyle2 ("HORZ", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      double dy2 = 75, dy1 = 70;
      Add (false, 70, 61 + dy1, 81, 73 + dy1); Add (false, 70, 61 + dy1, 65, 48 + dy1);
      Add (true, 70, 61 + dy1, 54, 57 + dy1); Add (false, 22, 13 + dy2, 19, 19 + dy2);
      Add (false, 22, 13 + dy2, 15, 15 + dy2); Add (true, 79, 12 + dy2, 76, 14 + dy2);

      style = new DimStyle2 ("HORZABOVE", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 160, 61 + dy1, 171, 73 + dy1); Add (false, 160, 61 + dy1, 155, 48 + dy1);
      Add (true, 160, 61 + dy1, 144, 57 + dy1); Add (false, 112, 13 + dy2, 109, 19 + dy2);
      Add (false, 112, 13 + dy2, 105, 15 + dy2); Add (true, 169, 12 + dy2, 166, 14 + dy2);

      style = new DimStyle2 ("HORZBELOW", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 250, 61 + dy1, 261, 73 + dy1); Add (false, 250, 61 + dy1, 245, 48 + dy1);
      Add (true, 250, 61 + dy1, 234, 57 + dy1); Add (false, 202, 13 + dy2, 199, 19 + dy2);
      Add (false, 202, 13 + dy2, 195, 15 + dy2); Add (true, 259, 12 + dy2, 256, 14 + dy2);
      Add (true, 229, 144, 230, 150);
      return dwg;

      void Add (bool tofl, params double[] vals) {
         var pts = Point2.List (vals);
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg];
         pts[0] = seg.Center;
         dwg.Add (new E2DimRad (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }
}
