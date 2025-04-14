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
      new SceneManipulator ();
      var tf = new TypeFace (Lib.ReadBytes ("wad:GL/Fonts/Roboto-Regular.ttf"), 40);
      var gn = new GroupVN ([
         new SimpleVN (() => { Lux.Color = Color4.Red; Lux.ZLevel = -1; }, DrawLines),
         new SimpleVN (() => { Lux.Color = Color4.Black; Lux.TypeFace = tf; }, DrawText),
         new SimpleVN (() => { Lux.Color = Color4.Cyan; }, () => Lux.Quads ([new (23, 23), new (36, 23), new (36, 32), new (23, 32)])),
         new SimpleVN (() => { Lux.Color = Color4.Black; Lux.TypeFace = tf; Lux.ZLevel = -1; }, () => Lux.Text2D ("Hello, World!", new (13.25, 25), ETextAlign.BaseLeft)),
         new SimpleVN (() => { Lux.Color = Color4.Black; Lux.TypeFace = tf; Lux.ZLevel = 1; }, () => Lux.Text2D ("Hello, World!", new (13.25, 28), ETextAlign.BaseLeft)),
      ]);
      Bound = new (0, 0, 100, 50);
      Root = gn;
   }

   void DrawLines () {
      Lux.Lines ([new (10, 5), new (36, 5), new (10, 10), new (36, 10), new (10, 15), new (36, 15), new (10, 20), new (36, 20),
                  new (10, 5), new (10, 20), new (23, 5), new (23, 20), new (36, 5), new (36, 20)]);
   }

   void DrawText () {
      Lux.Text2D ("BsL{}", new (10, 10), ETextAlign.BaseLeft);
      Lux.Text2D ("BsC{}", new (23, 10), ETextAlign.BaseCenter);
      Lux.Text2D ("BsR{}", new (36, 10), ETextAlign.BaseRight);

      Lux.Text2D ("MdL{}", new (10, 15), ETextAlign.MidLeft);
      Lux.Text2D ("MdC{}", new (23, 15), ETextAlign.MidCenter);
      Lux.Text2D ("MdR{}", new (36, 15), ETextAlign.MidRight);

      Lux.Text2D ("TpL{}", new (10, 20), ETextAlign.TopLeft);
      Lux.Text2D ("TpC{}", new (23, 20), ETextAlign.TopCenter);
      Lux.Text2D ("TpR{}", new (36, 20), ETextAlign.TopRight);

      Lux.Text2D ("BtL{}", new (10, 5), ETextAlign.BotLeft);
      Lux.Text2D ("BtC{}", new (23, 5), ETextAlign.BotCenter);
      Lux.Text2D ("BtR{}", new (36, 5), ETextAlign.BotRight);
   }

   public override Color4 BgrdColor => Color4.Gray (240);
}
