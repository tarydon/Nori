// ────── ╔╗
// ╔═╦╦═╦╦╬╣ StreamScene.cs
// ║║║║╬║╔╣║ Load and display a STEP file, select entities, connected entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using System.Reactive.Linq;
using System.Windows.Threading;
using Nori;

// class StreamDemoScene ---------------------------------------------------------------------------
class StreamDemoScene : Scene2 {
   public StreamDemoScene () {
      BgrdColor = Color4.Gray (216);
      Bound = new (0, 0, 500, 300);

      var lines1 = new Lines ([new (30, 30), new (100, 30), new (50, 50), new (200, 200), new (400, 250), new (400, 30)], Color4.DarkGreen);
      var lines2 = new Lines ([new (40, 40), new (440, 40), new (440, 40), new (250, 280)], Color4.Red);
      var quads1 = new Quads ([new (10, 10), new (50, 10), new (70, 50), new (20, 60), new (200, 30), new (150, 70), new (120, 50), new (180, 30)], Color4.Yellow);
      var quads2 = new Quads ([new (350, 100), new (430, 110), new (420, 130), new (350, 120)], new Color4 (128, 200, 128));
      var quads3 = new Quads ([new (10, 100), new (20, 100), new (40, 200), new (20, 200)], Color4.Yellow);
      var under = new Underlay ();
      var over = new Overlay ();

      Root = new GroupVN ([under, lines1, lines2, quads1, quads2, over, quads3]);
      //Root = new GroupVN ([quads1, lines1, quads2, quads3]);

      mKeys = HW.Keys.Where (a => a.Key == EKey.Space && a.State == EKeyState.Pressed).Subscribe (k => Lux.DumpStats ());
   }
   IDisposable mKeys;

   public override void Detached () => mKeys.Dispose ();
}

class Underlay : VNode {
   public Underlay () {
      mTimer = new DispatcherTimer () { Interval = TimeSpan.FromSeconds (0.5) };
      mTimer.Tick += (s, e) => { mPts.Clear (); Redraw (); };
      Streaming = true;
   }
   List<Vec2F> mPts = [];
   DispatcherTimer mTimer;

   public override void OnAttach () => mTimer.IsEnabled = true;

   public override void OnDetach () => mTimer.IsEnabled = false;

   public override void SetAttributes () {
      Lux.ZLevel = 8; Lux.LineWidth = 5f; Lux.Color = Color4.Gray (192);
   }
   Random mRand = new ();

   public override void Draw () {
      if (mPts.Count == 0) {
         for (int i = 0; i < 100; i++) {
            int x = mRand.Next (10, 490), y1 = mRand.Next (10, 290), y2 = mRand.Next (10, 290);
            mPts.Add (new (x, y1)); mPts.Add (new (x, y2));
         }
      }
      Lux.Lines (mPts.AsSpan ());
   }
}

class Lines : VNode {
   public Lines (List<Vec2F> pts, Color4 color) => (mPts, mColor) = (pts, color);
   Color4 mColor;
   List<Vec2F> mPts;

   public override void SetAttributes () { Lux.ZLevel = 10; Lux.Color = mColor; }
   public override void Draw () => Lux.Lines (mPts.AsSpan ());
}

class Quads : VNode {
   public Quads (List<Vec2F> pts, Color4 color)
      => (mPts, mColor, Streaming) = (pts, color, false);
   Color4 mColor;
   List<Vec2F> mPts;

   public override void SetAttributes () { Lux.ZLevel = 8; Lux.Color = mColor; }
   public override void Draw () => Lux.Quads (mPts.AsSpan ());
}

class Overlay : VNode {
   public Overlay () {
      mFace = new TypeFace ("N:/Wad/GL/Fonts/RobotoMono-Regular.ttf", 36);
      Streaming = true;
   }
   TypeFace mFace;
   IDisposable? mMouse;

   void OnMouseMove (Vec2S pt) {
      mPt = (Point2)Lux.PixelToWorld (pt);
      Redraw ();
   }
   Point2 mPt = new Point2 (-1000, 0);

   public override void SetAttributes () {
      Lux.Color = Color4.DarkGreen; Lux.ZLevel = 20;
      Lux.TypeFace = mFace;
   }

   public override void Draw () {
      string s = $"{(mPt.X * 10).Round (0)},{(mPt.Y * 10).Round (0)}";
      Lux.Text2D (s, (Vec2F)mPt, ETextAlign.MidCenter, new Vec2S (0, 0));
   }

   public override void OnAttach () => mMouse = HW.MouseMoves.Subscribe (OnMouseMove);
   public override void OnDetach () => mMouse?.Dispose ();
}
