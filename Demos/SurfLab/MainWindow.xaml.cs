using System.IO;
using System.Windows;
using Nori;
using SurfLab;
namespace Surfer;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();

      string file = "C:/Etc/T3/5x-051.t3x";
      Lux.UIScene = new SurfScene (file, -1);
   }
}
