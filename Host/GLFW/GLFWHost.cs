// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GLFWHost.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Reactive;
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
   public EKeyModifier Modifiers => throw new NotImplementedException ();
   public IObservable<string> Text => throw new NotImplementedException ();
}

class GLFWMouse : IMouse {
   public IObservable<MouseClickInfo> Clicks => throw new NotImplementedException ();
   public IObservable<Unit> Leave => throw new NotImplementedException ();
   public IObservable<Unit> Lost => throw new NotImplementedException ();
   public IObservable<Vec2S> Moves => throw new NotImplementedException ();
   public IObservable<MouseWheelInfo> Wheel => throw new NotImplementedException ();

   public Vec2S Pos => throw new NotImplementedException ();

   public bool TryCapture () => throw new NotImplementedException ();
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
