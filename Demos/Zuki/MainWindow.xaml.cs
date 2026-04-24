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

      Dwg2 dwg = new ();
      var layer = dwg.CurrentLayer;
      var style = dwg.CurrentDimStyle;
      dwg.Add (Poly.Rectangle (10, 5, 430, 200));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (20, 20, 40, 20, 34, 34, 40, 32)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (50, 20, 80, 20, 70, 40, 70, 30)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (80, 20, 105, 20, 100, 40, 97, 28)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (110, 20, 130, 20, 125, 35, 121, 22)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (140, 20, 160, 20, 155, 35, 149, 25)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (170, 20, 185, 20, 180, 30, 177, 24)));
      dwg.Add (new E2Dim3PAngular (layer, style, Point2.List (200, 20, 215, 20, 210, 30, 206, 21)));

      var s = style;
      DimStyle2 style2 = new ("HORZ", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TxtSize, s.DimCen,
         s.DimGap, true, true, s.TOFL, (int)s.TxtPos, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style2); dwg.CurrentDimStyle = style2;
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (220, 20, 240, 20, 234, 34, 240, 32)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (250, 20, 280, 20, 270, 40, 270, 30)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (280, 20, 305, 20, 300, 40, 297, 28)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (310, 20, 330, 20, 325, 35, 321, 22)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (340, 20, 360, 20, 355, 35, 349, 25)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (370, 20, 385, 20, 380, 30, 377, 24)));
      dwg.Add (new E2Dim3PAngular (layer, style2, Point2.List (400, 20, 415, 20, 410, 30, 406, 21)));

      DimStyle2 style3 = new ("ABOVE", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TxtSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 1, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style3); dwg.CurrentDimStyle = style3;
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (20, 120, 40, 120, 34, 134, 40, 132)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (50, 120, 80, 120, 70, 140, 70, 130)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (80, 120, 105, 120, 100, 140, 97, 128)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (110, 120, 130, 120, 125, 135, 121, 122)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (140, 120, 160, 120, 155, 135, 149, 125)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (170, 120, 185, 120, 180, 130, 177, 124)));
      dwg.Add (new E2Dim3PAngular (layer, style3, Point2.List (200, 120, 215, 120, 210, 130, 206, 121)));

      DimStyle2 style4 = new ("BELOW", 1, s.ArrowSize, s.ExtOffset, s.ExtExtend, s.TxtSize, s.DimCen,
         s.DimGap, s.TIHorz, s.TOHorz, s.TOFL, 4, s.LinDecimal, s.AngDecimal, s.Style);
      dwg.Add (style4); dwg.CurrentDimStyle = style4;
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (20,  70, 40,  70, 34,  84, 40,  82)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (50,  70, 80,  70, 70,  90, 70,  80)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (80,  70, 105, 70, 100, 90, 97,  78)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (110, 70, 130, 70, 125, 85, 121, 72)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (140, 70, 160, 70, 155, 85, 149, 75)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (170, 70, 185, 70, 180, 80, 177, 74)));
      dwg.Add (new E2Dim3PAngular (layer, style4, Point2.List (200, 70, 215, 70, 210, 80, 206, 71)));

      Hub.Dwg = dwg;
   }
}
