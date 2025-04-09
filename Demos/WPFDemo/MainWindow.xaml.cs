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
      Lux.OnReady = Ready;
   }

   void Ready () {
      new SceneManipulator ();
      Lux.UIScene = new LeafDemoScene ();
      HW.MouseMoves.Subscribe (OnMove);
   }

   void OnMove (Vec2S vec) {
      Point2 pt = (Point2)Lux.PixelToWorld (vec);
      mStatus.Text = pt.ToString ();
   }

   void LeafDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LeafDemoScene ();
   void LineFontDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LineFontScene ();
   void TrueTypeDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new TrueTypeScene ();
   void MeshDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new MeshScene ();
}
