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
      Lux.OnReady = (() => Lux.UIScene = new DemoScene ());
   }

   void LeafDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LeafDemoScene ();
   void LineFontDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new LineFontScene ();
   void TrueTypeDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new TrueTypeScene ();
   void MeshDemo (object sender, RoutedEventArgs e) => Lux.UIScene = new MeshScene ();
}

class DemoScene : Scene2 {
   public DemoScene () {
      var tf = new TypeFace ("c:\\windows\\fonts\\consola.ttf",48);
      var gn = new GroupVN ([
         new SimpleVN (() => { Lux.Color = Color4.Gray (160); Lux.ZLevel = 0; },
                       () => Lux.Quads ([new (10, 10), new (50, 10), new (50, 25), new (10, 25)])),
         new SimpleVN (() => { Lux.Color = Color4.Black; Lux.TypeFace = tf; Lux.ZLevel = 1; },
                       () => Lux.Text2D ("Hello, World {}", new (10, 10), ETextAlign.BaseLeft)),
      ]);
      Bound = new (0, 0, 100, 50);
      Root = gn;
   }

   public override Color4 BgrdColor => Color4.Gray (240);
}
