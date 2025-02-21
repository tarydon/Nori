// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
using System.Reactive.Linq;
namespace WPFDemo;
using static Lux;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();

      Lux.UIScene = new TextScene ();
   }
}

class TextScene : Scene2 {
   public TextScene () => Bound = new Bound2 (0, 0, 100, 60);

   public override Color4 BgrdColor => Color4.Gray (200);

   public override void Draw () {
      Lux.TypeFace = mFace;
      Lux.DrawColor = Color4.Black;
      Lux.Text ("Hello, World!", new (100, Viewport.Y - 200));
   }
   TypeFace mFace = new ("C:/Windows/Fonts/constan.ttf", 96);
}