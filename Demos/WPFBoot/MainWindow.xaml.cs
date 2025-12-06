using System.Windows;
using System.Windows.Controls;
using Nori;
namespace WPFBoot;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();
      Lux.OnReady.Subscribe (_ => Lux.UIScene = new MyScene ());
   }
}

class MyScene : Scene2 {
   public MyScene () {
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (64, 96, 128);
      Root = new SimpleVN (
         () => { Lux.Color = new Color4 (128, 192, 255); Lux.LineWidth = 6f; },
         () => Lux.Poly (Poly.Parse ("M10,10 H50 V20 Q45,25,1 H10 Z"))
      );
   }
}