using Nori;
namespace GLFWDemo;

internal class Program {
   static void Main () {
      Lib.Init (); Host.Init (); Lux2.Init ();

      var (_, _, width, height) = Monitor.Primary.WorkArea;
      var w = new Window (width * 6 / 10, height * 6 / 10, "Welcome to GLFW");
      w.CenterOnScreen ();
      w.Run (true);
   }
}
