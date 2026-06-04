// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GLFWHost.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Reactive;
using System.Runtime.InteropServices;
using Ptr = nint;

public static class GLFWHost {
   public static void Init (Action onReady) {
      Hub.OpenGL = new GLFWOpenGL ();
      Hub.Dispatcher = new GLFWDispatcher ();
      Hub.Keyboard = new GLFWKeyboard ();
      Hub.Mouse = new GLFWMouse ();
      SynchronizationContext.SetSynchronizationContext (new GLFWSyncContext (Hub.Dispatcher));
      OnReady = onReady;
   }

   internal static Action? OnReady;
   internal static Action<int, int>? OnPaint;
   internal static Window? Win;
}

class GLFWKeyboard : IKeyboard {
   public IObservable<KeyInfo> Keys => throw new NotImplementedException ();
   public EModifier Modifiers => throw new NotImplementedException ();
   public IObservable<string> Text => throw new NotImplementedException ();
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
