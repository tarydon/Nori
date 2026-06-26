using System.Net;
using System.Reactive.Linq;
using Nori;
namespace UXDemo;

class Program {
   static void Main () {
      Lib.Init ();
      GLFWHost.Init (OnReady);
      mWin = new Window (1024, 768, "U X D e m o", Window.EFlags.Default | Window.EFlags.Maximized);
      mWin.Run (true);
   }
   static Window? mWin;

   static void OnReady () {
      Lib.Tracer = TraceVN.Print;
      TraceVN.HoldTime = 15;
      TraceVN.TextColor = Color4.Black;
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Escape)).Subscribe (_ => mWin?.ShouldClose = true);
      Lux.UIScene = new UXScene ();
   }
}

class UXScene : Scene2 {
   public UXScene () {
      BgrdColor = new Color4 (243, 232, 227);
      List<VNode> nodes = [new UXVNode (), TraceVN.It];
      Root = new GroupVN (nodes);
   }
}
