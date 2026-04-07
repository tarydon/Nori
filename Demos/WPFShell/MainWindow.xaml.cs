using System.Reactive.Linq;
using System.Windows;
using Nori;
namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); Lux2.Init ();
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);
      TraceVN.TextColor = Color4.Yellow; Lib.Tracer = TraceVN.Print;
      Root = new GroupVN([new DemoVN (Pts), TraceVN.It]);

      HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (OnClick);
      HW.MouseClicks.Where (a => a.Button == EMouseButton.Left && a.IsRelease).Subscribe (OnRelease);
      HW.MouseMoves.Subscribe (OnMove);
   }
   AList<Vec2S> Pts = [];

   void OnClick (MouseClickInfo mi) {
      if (HW.CaptureMouse (true)) {
         Pts.Add (mi.Position); Pts.Add (mi.Position);
         mDragging = true;
      }
   }
   bool mDragging;

   void OnMove (Vec2S pt) {
      if (mDragging) { Pts[^1] = pt; }
   }

   void OnRelease (MouseClickInfo mi) {
      if (mDragging) { mDragging = false; HW.CaptureMouse (false); }
   }
}

class DemoVN : VNode {
   public DemoVN (AList<Vec2S> pts) {
      (mPts = pts).Subscribe (a => Redraw ());
      Streaming = true; 
   }

   public override void SetAttributes () => Lux.Color = Color4.White;

   public override void Draw () {
      var pts = mPts.Select (a => new Vec2F (a.X, a.Y)).ToList ();
      Lux.PxLines (pts.AsSpan ());

      if (mPts.Count > 0) {
         TypeFace tf = TypeFace.Default;
         Vec2S a = mPts[^2], b = mPts[^1];
         string text = $"({b.X},{b.Y})";
         RectS rect = tf.Measure (text, true);
         if (b.X >= a.X) Lux.TextPx (text, new Vec2S (b.X + rect.Height / 2, b.Y + rect.Height / 2));
         else Lux.TextPx (text, new Vec2S (b.X - rect.Width - rect.Height / 2, b.Y + rect.Height / 2));
      }
   }

   AList<Vec2S> mPts;
}