// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      mPanel.Child = Lux.CreatePanel ();

      Lux.UIScene = new RoadScene (mRoad);
      Lux.OnReady = (() => new SceneManipulator ());
   }
   Road mRoad = new ();

   void Recolor (object sender, RoutedEventArgs e) 
      => GetBus ().Color = Color4.Random;

   void Reposition (object sender, RoutedEventArgs e) {
      var bus = GetBus ();
      int xPos = mRand.Next (5, (int)(mRoad.Span.Max - bus.Size.X - 5));
      bus.Pos = new (xPos, 0);
   }

   void Resize (object sender, RoutedEventArgs e) {
      var bus = GetBus ();
      int dx = mRand.Next (10, 20), dy = mRand.Next (5, 10);
      bus.Size = new (dx, dy);
   }

   Random mRand = new ();
   Bus GetBus () => mRoad.Buses[mRand.Next (mRoad.Buses.Count)];
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
