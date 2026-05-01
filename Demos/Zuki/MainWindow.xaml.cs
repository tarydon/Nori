using System.Diagnostics;
using System.Windows;
using System.IO;
using Nori;

namespace Zuki;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); Lux2.Init ();
      InitializeComponent ();
      mContent.Child = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.DarkGreen; TraceVN.HoldTime = 8;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();

      MenuCmds.Connect (mMenu);
      (Hub.MainWindow, Hub.CmdName, Hub.Status) = (this, mCommand, mStatus);
      var dxf = Environment.GetCommandLineArgs ().FirstOrDefault (a => a.EndsWith (".dxf"));
      if (dxf != null) Hub.LoadDXF (dxf);
      else {
         Hub.Dwg = MakeDimAligned ();
         Hub.Widget = new DimAlignedMaker ();
      }
   }

   Dwg2 MakeDimAligned () {
      var dwg = DXFReader.Load ("c:/etc/dimlinear.dxf");
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
      Add (65, 115, 85, 115, 64, 119);
      Add (65, 115, 85, 115, 86, 123);
      Add (115, 100, 115, 65, 112, 70);
      Add (115, 100, 115, 65, 118, 63);
      Add (65, 75, 80, 75, 74, 78);
      Add (85, 115, 115, 100, 80, 109);
      Add (85, 115, 115, 100, 115, 105);
      Add (85, 90, 90, 90, 86, 98);
      Add (85, 90, 87, 90, 85.9, 82);
      Add (95, 90, 93.75, 90, 94.9, 82);
      Add (80, 65, 115, 65, 100, 69);
      Add (65, 75, 80, 65, 80, 61);

      //dx = 130; dy = 65;
      //style = new DimStyle2 ("HORZ-BELOW", tstyle) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      //dwg.Add (style); dwg.CurrentDimStyle = style;
      //AddStuff ();
      return dwg; 

      void AddStuff () {
         Add (0, 50, 20, 50, 9, 54); Add (20, 50, 50, 35, 47, 41);
         Add (50, 0, 50, 35, 54, 16); Add (15, 0, 50, 0, 39, -4);
         Add (15, 0, 15, 10, 19, 11); Add (15, 10, 0, 10, -1, 14);
         Add (8, 10, 8, 50, 8, 30); Add (20, 25, 30, 25, 41, 32);
         Add (25, 20, 25, 25, 15, 22); Add (25, 20, 25, 25.67, 35, 22);
         Add (15, 0, 0, 10, 0, 3);
      }

      void Add (params double[] vals) {
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.Add (new E2DimAligned (dwg.CurrentLayer, dwg.CurrentDimStyle, pts));
      }
   }

   Dwg2 MakeDim3PAngle () {
      var dwg = DXFReader.Load ("N:/TData/Dwg/Dim/Dim3PAngle-Blank.dxf", true);
      var tstyle = dwg.GetStyle ("STANDARD")!;
      var layer = new Layer2 ("DIMENSION", Color4.Blue, ELineType.Continuous);
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

   Dwg2 MakeDimAngle () {
      var dwg = DXFReader.Load ("c:/dropbox/wip/dimangle.dxf");
      var tstyle = dwg.GetStyle ("STANDARD")!;
      var layer = new Layer2 ("DIMENSION", Color4.Blue, ELineType.Continuous);
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

      CurlWriter.Save (dwg, "c:/etc/test.curl");
      if (File.ReadAllText ("c:\\etc\\ref.curl") != File.ReadAllText ("c:\\etc\\test.curl"))
         Process.Start ("winmergeu.exe", "c:\\etc\\ref.curl c:\\etc\\test.curl");

      return dwg; 

      void Add (params double[] vals) {
         List<Point2> pts = [];
         var input = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         if (dwg.PickPoly (input[0], 5, out var tp1)) pts.AddM (tp1.Poly.A, tp1.Poly.B);
         if (dwg.PickPoly (input[1], 5, out var tp2)) pts.AddM (tp2.Poly.A, tp2.Poly.B);
         pts.Add (input[2]);
         dwg.Add (new E2DimAngle (dwg.CurrentLayer, dwg.CurrentDimStyle, pts));
      }
   }

   Dwg2 MakeDimDia () {
      var dwg = DXFReader.Load ("N:/TData/Dwg/Dim/DimDia-Blank.dxf", true);
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

   Dwg2 MakeDimRad () {
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
