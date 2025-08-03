// ────── ╔╗
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
      Lux.OnReady.Subscribe (a => new SceneManipulator ());
   }

   void LeafDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LeafDemoScene ();
   void LineFontDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LineFontScene ();
   void TrueTypeDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new TrueTypeScene ();
   void MeshDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new MeshScene ();
   void TessDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new MeshScene (true);
   void BooleanDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new BooleanScene ();
   void DwgDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new DwgScene ();
}
