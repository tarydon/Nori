// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window of WPF demo application (various scenes implemented)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      mContent.Child = Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      new SceneManipulator ();
   }

   void LeafDemo (object sender, RoutedEventArgs e) => Display (new LeafDemoScene ());
   void LineFontDemo (object sender, RoutedEventArgs e) => Display (new LineFontScene ());
   void TrueTypeDemo (object sender, RoutedEventArgs e) => Display (new TrueTypeScene ());
   void MeshDemo (object sender, RoutedEventArgs e) => Display (new MeshScene ());
   void TessDemo (object sender, RoutedEventArgs e) => Display (new MeshScene (true));
   void BooleanDemo (object sender, RoutedEventArgs e) => Display (new BooleanScene ());
   void DwgDemo (object sender, RoutedEventArgs e) => Display (new DwgScene ());
   void RobotDemo (object sender, RoutedEventArgs e) => Display (new RobotScene ());
   void STPDemo (object sender, RoutedEventArgs e) => Display (new STPScene ());

   void Display (Scene scene) {
      mSettings.Children.Clear ();
      Lux.UIScene = scene;
      if (scene is RobotScene rs) rs.CreateUI (mSettings.Children);
      if (scene is STPScene ss) Lux.BackFacesPink = true; 
   }
}
