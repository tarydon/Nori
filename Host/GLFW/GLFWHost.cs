// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GLFWHost.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Ptr = nint;

public static class GLFWHost {
   public static void Init (Action onReady) {
      Hub.OpenGL = new GLFWOpenGL ();
      OnReady = onReady;
   }

   internal static Action? OnReady;
   internal static Action<int, int>? OnPaint;
   internal static Window? Win;
}

class GLFWOpenGL : IOpenGL {
   public Action<int, int> OnPaint { set => GLFWHost.OnPaint = value; }

   public Ptr GetGLProcAddress (string name) {
      var szName = Marshal.StringToHGlobalAnsi (name);
      Ptr proc = GLFW.GetProcAddress (szName);
      Marshal.FreeHGlobal (szName);
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc;
   }

   public void Redraw () => GLFW.PostEmptyEvent ();

   public float DPIScale {
      get {
         if (mDPIScale == 0) {
            if (GLFWHost.Win is { } win) mDPIScale = win.DPIScale;
            else return 1; 
         }
         return mDPIScale;
      }
   }
   float mDPIScale = 0;
}
