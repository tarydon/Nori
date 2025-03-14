// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();

      Lux.UIScene = new RoadScene (new Road ());
      Lux.OnReady = (() => new SceneManipulator ());
   }
}

class RoadScene : Scene2 {
   public RoadScene (Road road) : base (new RoadVN (road)) {
      var span = (mRoad = road).Span;
      double dy = span.Length * 0.6;
      Bound = new (span.Min, -dy * 0.2, span.Max, dy * 0.8);
   }
   readonly Road mRoad;

   public override Color4 BgrdColor => Color4.Gray (225);
}
