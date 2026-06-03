using Nori;
namespace GLFWDemo;

internal class Program {
   static void Main () {
      Lib.Init (); GLFWHost.Init (OnReady); Lux2.Init ();

      var (_, _, width, height) = Monitor.Primary.WorkArea;
      var w = new Window (width * 6 / 10, height * 6 / 10, "Welcome to GLFW");
      w.CenterOnScreen ();
      w.Run (true);
   }

   static void OnReady () {
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
      Vec2S cen = new Vec2S (dx / 2 + dy, dy / 2 + dy);
      var vn1 = new SimpleVN (
         () => (Lux.Color, Lux.TypeFace, Lux.ZLevel) = (new (255, 224, 226, 228), mFace, 1),
         () => Lux.Text (message, new Vec2S (cen.X - dx / 2, cen.Y + dy / 2))
      );

      var vn2 = new SimpleVN (
         () => Lux.UIRect (cen, new Vec2S (size.Width + dy, size.Height + dy), 16, 8, new (255, 64, 66, 68), new (255, 200, 202, 204))
      ) { Streaming = true };
      var gvn = new GroupVN ([vn1, vn2]);
      Root = gvn;
   }

   TypeFace mFace;
}