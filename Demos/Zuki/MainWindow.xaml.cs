using System.Windows.Input;
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
      (Hub.MainWindow, Hub.Command, Hub.Status) = (this, mCommand, mStatus);
      Hub.Dwg = Make3PAngularDwg ();
   }

   Dwg2 Make3PAngularDwg () {
      Dwg2 dwg = new ();
      var layer = dwg.CurrentLayer;
      var style = dwg.CurrentDimStyle; var s = style;
      dwg.Add (Poly.Rectangle (10, 5, 225, 185));
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

      dwg.CurrentDimStyle = style3;
      Add (50, 150, 60, 150, 50, 160, 65, 165);
      Add (50, 150, 50, 160, 40, 150, 36, 164);
      Add (50, 150, 40, 150, 50, 140, 37, 137);
      Add (50, 150, 50, 140, 60, 150, 62, 138);
      dwg.CurrentDimStyle = style;
      Add (90, 150, 90, 140, 83, 157, 100, 160);
      Add (130, 150, 140, 160, 140, 140, 118, 150);
      Add (130, 150, 140, 160, 140, 140, 148, 150);

      void Add (params double[] vals)
         => dwg.Add (new E2Dim3PAngular (layer, dwg.CurrentDimStyle, Point2.List (vals)));
      return dwg; 
   }
}
