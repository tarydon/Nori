using System.Windows;
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
         Hub.Dwg = TestAngleDim ();
         Hub.Widget = new DimAngleMaker ();
      }
   }

   Dwg2 TestAngleDim () {
      var dwg = DXFReader.Load ("c:/etc/dimangle.dxf");
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
      style = new DimStyle2 ("BREAKHORZ", tstyle) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      // Add (35, 20, 30, 30, 26, 21); 
      //Add (35, 20, 30, 30, 29, 24);
      //Add (35, 20, 30, 30, 30, 29); Add (35, 20, 30, 30, 37, 23);
      //Add (35, 20, 30, 30, 40, 29); Add (35, 20, 30, 30, 44, 34);
      return dwg; 

      void Add (params double[] vals) {
         List<Point2> pts = [];
         var input = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         if (dwg.PickPoly (input[0], 5, out var tp1)) pts.AddM (tp1.Poly.A, tp1.Poly.B);
         if (dwg.PickPoly (input[1], 5, out var tp2)) pts.AddM (tp2.Poly.A, tp2.Poly.B);
         pts.Add (input[2]);
         dwg.Add (new E2DimAngular (dwg.CurrentLayer, dwg.CurrentDimStyle, pts));
      }
   }

   Dwg2 TestDimDia () {
      double dx = 0, dy = 0;
      var dwg = DXFReader.Load ("N:/TData/Dwg/Dim/DimDia-Blank.dxf");
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

      // Header ............................................
      void Add (bool tofl, params double[] vals) {
         var pts = Point2.List (vals).Select (a => a.Moved (dx, dy)).ToList ();
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg];
         pts[0] = seg.Center;
         dwg.Add (new E2DimDia (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }

   Dwg2 TestDimRad () {
      var dwg = DXFReader.Load ("N:/TData/Dwg/Dim/DimRad-Blank.dxf");
      dwg.Add (new DimStyle2 ("BREAK", dwg.GetStyle ("STANDARD")!));
      dwg.CurrentDimStyle = dwg.GetDimStyle ("BREAK");
      Add (false, 70, 56, 81, 68); Add (false, 70, 56, 65, 43); 
      Add (true,  70, 56, 54, 52); Add (false, 22, 13, 19, 19); 
      Add (false, 22, 13, 15, 15); Add (true,  79, 12, 76, 14);

      var style = new DimStyle2 ("ABOVE", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 160, 56, 171, 68); Add (false, 160, 56, 155, 43);
      Add (true,  160, 56, 144, 52); Add (false, 112, 13, 109, 19);
      Add (false, 112, 13, 105, 15); Add (true,  169, 12, 166, 14);

      style = new DimStyle2 ("BELOW", dwg.GetStyle ("STANDARD")!) { TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 250, 56, 261, 68); Add (false, 250, 56, 245, 43);
      Add (true,  250, 56, 234, 52); Add (false, 202, 13, 199, 19);
      Add (false, 202, 13, 195, 15); Add (true,  259, 12, 256, 14);

      style = new DimStyle2 ("HORZ", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      double dy2 = 75, dy1 = 70;
      Add (false, 70, 61 + dy1, 81, 73 + dy1); Add (false, 70, 61 + dy1, 65, 48 + dy1);
      Add (true,  70, 61 + dy1, 54, 57 + dy1); Add (false, 22, 13 + dy2, 19, 19 + dy2);
      Add (false, 22, 13 + dy2, 15, 15 + dy2); Add (true,  79, 12 + dy2, 76, 14 + dy2);

      style = new DimStyle2 ("HORZABOVE", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Above };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 160, 61 + dy1, 171, 73 + dy1); Add (false, 160, 61 + dy1, 155, 48 + dy1);
      Add (true,  160, 61 + dy1, 144, 57 + dy1); Add (false, 112, 13 + dy2, 109, 19 + dy2);
      Add (false, 112, 13 + dy2, 105, 15 + dy2); Add (true,  169, 12 + dy2, 166, 14 + dy2);

      style = new DimStyle2 ("HORZBELOW", dwg.GetStyle ("STANDARD")!) { TIHorz = true, TOHorz = true, TextPos = DimStyle2.EPos.Below };
      dwg.Add (style); dwg.CurrentDimStyle = style;
      Add (false, 250, 61 + dy1, 261, 73 + dy1); Add (false, 250, 61 + dy1, 245, 48 + dy1);
      Add (true,  250, 61 + dy1, 234, 57 + dy1); Add (false, 202, 13 + dy2, 199, 19 + dy2);
      Add (false, 202, 13 + dy2, 195, 15 + dy2); Add (true,  259, 12 + dy2, 256, 14 + dy2);
      Add (true, 250, 61 + dy1, 230, 150);
      return dwg;

      // Header ............................................
      void Add (bool tofl, params double[] vals) {
         var pts = Point2.List (vals);
         dwg.PickPoly (pts[0], 3, out var p);
         var seg = p.Poly[p.Seg];
         pts[0] = seg.Center;
         dwg.Add (new E2DimRad (dwg.CurrentLayer, dwg.CurrentDimStyle, seg.Radius, tofl, pts));
      }
   }
}
