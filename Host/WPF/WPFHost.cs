// ────── ╔╗
// ╔═╦╦═╦╦╬╣ WPFHost.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace Nori;
using Ptr = nint;

public static class WPFHost {
   public static UIElement Create (Window main, Action onReady) {
      Hub.OpenGL = new WPFOpenGL ();
      Hub.Keyboard = new WPFKeyboard ();
      Hub.Mouse = new WPFMouse ();
      Hub.Dispatcher = new WPFDispatcher (main.Dispatcher);
      Main = main;
      OnReady = onReady;
      return GLPanel = Panel.It;
   }

   static internal Window? Main;
   static internal Action? OnReady;
   static internal Action<int, int>? OnPaint;
   static internal Panel? GLPanel;
}

class WPFDispatcher : IDispatcher {
   public WPFDispatcher (Dispatcher disp) => mDispatcher = disp;
   readonly Dispatcher mDispatcher;

   public bool CheckAccess () => mDispatcher.CheckAccess ();
   public Task InvokeAsync (Action act) => mDispatcher.InvokeAsync (act).Task;
   public Task<T> InvokeAsync<T> (Func<T> func) => mDispatcher.InvokeAsync (func).Task;
   public void Post (Action act) => mDispatcher.BeginInvoke (act);
}

class WPFOpenGL : IOpenGL {
   public WPFOpenGL () => Lib = NativeLibrary.Load ("opengl32.dll");
   readonly Ptr Lib;

   public Ptr GetGLProcAddress (string name) {
      Ptr proc = WGLGetProcAddress (name);
      if (proc == 0)
         try { proc = NativeLibrary.GetExport (Lib, name); } catch { }
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc; 
   }

   public Action<int, int> OnPaint { set => WPFHost.OnPaint = value; }

   public void Redraw () => WPFHost.GLPanel?.Redraw ();

   public float DPIScale {
      get {
         if (mDPIScale == 0) {
            if (PresentationSource.FromVisual (WPFHost.Main) is { } source) {
               var xfm = source.CompositionTarget.TransformToDevice;
               mDPIScale = (float)(xfm.M11 + xfm.M22) / 2;
            } else return 1;
         }
         return mDPIScale;
      }
   }
   float mDPIScale = 0;

   const string OPENGL32 = "opengl32.dll";
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] public static extern Ptr WGLGetProcAddress (string name);
}
