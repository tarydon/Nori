using System.Windows;
using Nori;

namespace Zuki;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); Lux2.Init ();
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.DarkGreen; TraceVN.HoldTime = 8;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();
      Hub.LoadDXF ("C:/DropBox/Nori/Dimension/Dimensions.dxf");
   }
}
