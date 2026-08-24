// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using Nori;
namespace UXDemo;

class Program {
   static void Main () {
      Lib.Init ();
      GLFWHost.Init (OnReady);
      MainWin = new Window (1600, 1200, "I N L A Y Demo", Window.EFlags.Default | Window.EFlags.Maximized);
      MainWin.Run (false);
   }
   static public Window MainWin = null!;

   static void OnReady () {
      Lux.UIScene = new DemoScene ();
      Lib.Tracer = TraceVN.Print; TraceVN.TextColor = Color4.Yellow;
      // Lux.FPS.Subscribe (n => MainWin.Title = $"FPS: {n}");
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Escape))
                       .Subscribe (_ => MainWin?.ShouldClose = true);
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      BgrdColor = Color4.Gray (64);
      TypeFace tf1 = new ("C:/Windows/Fonts/SegoeUI.ttf", (int)(10 * Lux.DPIScale + 0.5));
      TypeFace tf2 = new ("C:/Windows/Fonts/SegUISym.ttf", (int)(12 * Lux.DPIScale + 0.5));
      Root = new GroupVN ([new UXRootVN ("N:/UXDemo/Res", [tf1, tf2]), TraceVN.It /*, StatsVN.It*/]);
   }
}
