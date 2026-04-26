// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;

      var dwg = Make3PAngularDwg ();
      var bound = dwg.Bound;
      CurlWriter.Save (dwg, "c:/etc/test.curl", "3-P Angular");
      DXFWriter.Save (dwg, "c:/etc/test.dxf", true);
   }

   static Dwg2 Make3PAngularDwg () {
      Dwg2 dwg = new ();
      var layer = dwg.CurrentLayer;
      var style = dwg.CurrentDimStyle; var s = style;
      dwg.Add (Poly.Rectangle (10, 5, 280, 170));
      Add (20, 20, 40, 20, 34, 34, 40, 32);
      Add (50, 20, 80, 20, 70, 40, 70, 30);
      Add (80, 20, 105, 20, 100, 40, 97, 28);
      Add (110, 20, 130, 20, 125, 35, 121, 22);
      Add (140, 20, 160, 20, 155, 35, 149, 25);
      Add (170, 20, 185, 20, 180, 30, 177, 24);
      Add (200, 20, 215, 20, 210, 30, 206, 21);

      DimStyle2 style4 = new ("BELOW", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 4, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style4); dwg.CurrentDimStyle = style4;
      Add (20, 50, 40, 50, 34, 64, 40, 62);
      Add (50, 50, 80, 50, 70, 70, 70, 60);
      Add (80, 50, 105, 50, 100, 70, 97, 58);
      Add (110, 50, 130, 50, 125, 65, 121, 52);
      Add (140, 50, 160, 50, 155, 65, 149, 55);
      Add (170, 50, 185, 50, 180, 60, 177, 54);
      Add (200, 50, 215, 50, 210, 60, 206, 51);

      DimStyle2 style3 = new ("ABOVE", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 1, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style3); dwg.CurrentDimStyle = style3;
      Add (20, 80, 40, 80, 34, 94, 40, 92);
      Add (50, 80, 80, 80, 70, 100, 70, 90);
      Add (80, 80, 105, 80, 100, 100, 97, 88);
      Add (110, 80, 130, 80, 125, 95, 121, 82);
      Add (140, 80, 160, 80, 155, 95, 149, 85);
      Add (170, 80, 185, 80, 180, 90, 177, 84);
      Add (200, 80, 215, 80, 210, 90, 206, 81);

      DimStyle2 style2 = new ("HORZ", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, true, true, s.TOFL, (int)s.TextPos, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style2); dwg.CurrentDimStyle = style2;
      Add (20, 110, 40, 110, 34, 124, 40, 122);
      Add (50, 110, 80, 110, 70, 130, 70, 120);
      Add (80, 110, 105, 110, 100, 130, 89, 113);
      Add (110, 110, 130, 110, 125, 125, 121, 112);
      Add (140, 110, 160, 110, 155, 125, 149, 115);
      Add (170, 110, 185, 110, 180, 120, 176, 112);
      Add (200, 110, 215, 110, 210, 120, 205, 114);

      DimStyle2 style5 = new ("MIXED", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TextSize, s.DimCen,
         s.DimGap, false, true, s.TOFL, 1, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style5); dwg.CurrentDimStyle = style5;
      Add (20, 140, 40, 140, 34, 154, 40, 152);
      Add (50, 140, 80, 140, 70, 160, 70, 150);
      Add (80, 140, 105, 140, 100, 160, 97, 148);
      Add (110, 140, 130, 140, 125, 155, 121, 142);
      Add (140, 140, 160, 140, 155, 155, 149, 145);
      Add (170, 140, 185, 140, 180, 150, 177, 144);
      Add (200, 140, 215, 140, 210, 150, 206, 141);

      dwg.CurrentDimStyle = style3;
      Add (245, 30, 255, 30, 245, 40, 260, 45);
      Add (245, 30, 245, 40, 235, 30, 231, 44);
      Add (245, 30, 235, 30, 245, 20, 232, 17);
      Add (245, 30, 245, 20, 255, 30, 257, 18);
      dwg.CurrentDimStyle = style;
      Add (245, 80, 245, 70, 238, 87, 255, 90);
      Add (245, 120, 255, 130, 255, 110, 233, 120);
      Add (245, 120, 255, 130, 255, 110, 263, 120);

      void Add (params double[] vals)
         => dwg.Add (new E2Dim3PAngle (layer, dwg.CurrentDimStyle, Point2.List (vals)));
      return dwg;
   }
}
