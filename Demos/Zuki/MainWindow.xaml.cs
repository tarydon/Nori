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
      else Hub.Dwg = new Dwg2 ();
   }
}
