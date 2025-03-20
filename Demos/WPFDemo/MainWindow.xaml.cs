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
      Content = Lux.CreatePanel ();
      VNode.RegisterAssembly (typeof (BusVN).Assembly);

      Lux.UIScene = new RoadScene (mRoad);
      Lux.StartContinuousRender (mRoad.Tick);
      Lux.FPS.Subscribe (OnFPSChanged);
   }
   Road mRoad = new ();

   void OnFPSChanged (int fps)
      => Title = $"{fps} FPS";
}

class RoadScene : Scene2 {
   public RoadScene (Road road) : base (new RoadVN (road)) {
      var span = (mRoad = road).Span;
      double dy = span.Length * 0.4;
      Bound = new (span.Min, -10, span.Max, dy);
   }
   readonly Road mRoad;

   public override Color4 BgrdColor => Color4.Gray (225);
}
