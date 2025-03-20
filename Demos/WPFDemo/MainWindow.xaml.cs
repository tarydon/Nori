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
      Lib.Tracer = AddLog;
   }
   Road mRoad = new ();

   void AddLog (string message) {
      mLog.Text = $"{mLog.Text}{message}";
      mLog.ScrollToEnd ();
   }

   // Perform various operations on buses. Other than Add, the other routines
   // pick a random bus on which to operate
   void Add (object sender, RoutedEventArgs e) {
      var size = new Vector2 (mRand.Next (10, 20), mRand.Next (5, 10));
      var pos = new Point2 (mRand.Next (5, (int)(mRoad.Span.Max - size.X - 5)), 0);
      mRoad.Buses.Add (new Bus (pos, size, Color4.RandomDark));
   }

   void Blank (object sender, RoutedEventArgs e) {
      Lux.UIScene = new BlankScene ();
   }

   void Recolor (object sender, RoutedEventArgs e) 
      => GetBus ().Color = Color4.Random;

   void Remove (object sender, RoutedEventArgs e) {
      var buses = mRoad.Buses;
      if (buses.Count > 0)  buses.RemoveAt (mRand.Next (buses.Count));
   }

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

   Random mRand = new (1);
   Bus GetBus () => mRoad.Buses[mRand.Next (mRoad.Buses.Count)];
}

class RoadScene : Scene2 {
   public RoadScene (Road road) : base (new RoadVN (road)) {
      var span = (mRoad = road).Span;
      double dy = span.Length * 0.4;
      Bound = new (span.Min, -dy * 0.2, span.Max, dy * 0.8);
   }
   readonly Road mRoad;

   public override Color4 BgrdColor => Color4.Gray (225);
}
