using System.Reactive.Linq;
using System.Windows;
using Nori;
namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); 
      InitializeComponent ();
      Content = WPFHost.Create (this, OnReady);
      Lux2.Init ();
   }

   void OnReady () {
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(48 * Lux.DPIScale));
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = new Color4 (128, 96, 64);

      string message = "Welcome to Nori.";
      var size = mFace.Measure (message, true);
      int dx = size.Width, dy = size.Height;
      Vec2S cen = new (dx / 2 + dy, dy / 2 + dy);
      var vn1 = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace, Lux.ZLevel) = (new (255, 224, 226, 228), mFace, 1),
         () => Lux.Text (message, new Vec2S (cen.X - dx / 2, cen.Y + dy / 2))
      );

      var vn2 = new SimpleVN (
         () => Lux.UIRect (cen, new Vec2S (size.Width + dy, size.Height + dy), 16, 8, new (255, 64, 66, 68), new (255, 200, 202, 204))
      ) { Streaming = true };
      var gvn = new GroupVN ([vn1, vn2, mDemo, mMouse]);
      Root = gvn;
   }

   DemoVN mDemo = new ();
   MouseVN mMouse = new ();
   TypeFace mFace;
}

class DemoVN : VNode {
   public DemoVN () => Hub.Dispatcher.Timer (System.TimeSpan.FromSeconds (1), true, Redraw);

   public override void Draw () {
      Lux.TypeFace = TypeFace.Default;
      Vec2S pos = Hub.Mouse.Pos;
      EModifier mods = Hub.Keyboard.Modifiers;
      Lux.Text ($"Step {mN++} {pos} ({mods})", new Vec2S (100, 400));
   }

   static int mN;
}

class MouseVN : VNode {
   public MouseVN () {
      Hub.Mouse.Moves.Subscribe (p => { mPos = p; Redraw (); });
      Hub.Mouse.Clicks.Subscribe (p => { mClick = p; Redraw (); });
      Hub.Mouse.Wheel.Subscribe (p => { mWheel = p; mWheelPos += p.Delta; Redraw (); });
      Hub.Mouse.Enter.Subscribe (p => { mEnter = p; Redraw (); });

      Hub.Keyboard.Keys.Subscribe (p => { mKey = p; Redraw (); });
      Hub.Keyboard.Text.Subscribe (p => { mChars = p; Redraw (); });
   }

   public override void SetAttributes () => Lux.TypeFace = TypeFace.Default;

   public override void Draw () {
      Lux.Text ($"MousePos: {mPos}", new Vec2S (100, 430));
      Lux.Text ($"Click: {mClick}", new Vec2S (100, 460));
      Lux.Text ($"Wheel: {mWheel.Position} / {mWheelPos}", new Vec2S (100, 490));
      Lux.Text ($"Enter: {mEnter}", new Vec2S (100, 520));
      Lux.Text ($"Key: {mKey}", new Vec2S (100, 580));
      Lux.Text ($"Chars: {mChars}", new Vec2S (100, 610));
   }

   Vec2S mPos;
   MouseClickInfo mClick;
   MouseWheelInfo mWheel;
   int mWheelPos;
   bool mEnter;
   KeyInfo mKey;
   string mChars = "";
}
