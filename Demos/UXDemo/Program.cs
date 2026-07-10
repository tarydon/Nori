using System.Net;
using System.Reactive.Linq;
using Nori;
namespace UXDemo;

class Program {
   static void Main () {
      Lib.Init ();
      GLFWHost.Init (OnReady);
      Window = new Window (1024, 768, "U X D e m o", Window.EFlags.Default | Window.EFlags.Maximized);
      Window.Run (true);
   }
   public static Window? Window;

   static void OnReady () {
      Lib.Tracer = TraceVN.Print;
      TraceVN.HoldTime = 15;
      TraceVN.TextColor = Color4.White;
      Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Escape)).Subscribe (_ => Window?.ShouldClose = true);
      Lux.UIScene = new UXScene ();
   }
}

class UXScene : Scene2 {
   public UXScene () {
      BgrdColor = Color4.Gray (96);
      List<VNode> nodes = [new UXDemoVN (), TraceVN.It];
      Root = new GroupVN (nodes);
   }
}
