// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Net;
using System.Reactive.Linq;
using Nori;
namespace UXDemo;

class Program {
   static void Main (string[] args) {
      Lib.Init ();
      GLFWHost.Init (OnReady);
      mWin = new Window (1600, 1200, "Welcome to GLFW", Window.EFlags.Default);
      mWin.Run (true);
   }
   static Window? mWin;

   static void OnReady () {
      Lux.UIScene = new DemoScene ();
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Escape))
                       .Subscribe (_ => mWin?.ShouldClose = true);
   }
}

class DemoScene : Scene2 {
   public DemoScene () {
      BgrdColor = Color4.Gray (0);
   }
}
