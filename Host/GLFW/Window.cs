// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Window.cs
// ║║║║╬║╔╣║ Wrapper around a GLFW window <ToDoc> 
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
namespace Nori;
using static GLFW;

#region class Window -------------------------------------------------------------------------------
/// <summary>A wrapper around a GLFW window</summary>
/// This is still evolving and the public interface of this will change. In particular, it is not
/// yet clear whether you will ever create a derived class from this, or just always use this type
/// and merely provide a different scene to render the contents. 
public class Window {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a Window with specified size, title and flags</summary>
   public Window (int cx, int cy, string title, EFlags flags = EFlags.Default) {
      SetWindowHints (flags);
      mDispatcher = (GLFWDispatcher)Hub.Dispatcher;
      byte[] bTitle = Encoding.UTF8.GetBytes (title);
      Array.Resize (ref bTitle, bTitle.Length + 1);
      mHWnd = CreateWindow (cx, cy, bTitle, HMonitor.None, HWindow.None);
      MakeContextCurrent (mHWnd);
      GLFWMouse.HWnd = GLFWKeyboard.HWnd = mHWnd;
      SwapInterval (1);
      GLFWHost.Win = this; 
      GLFWHost.OnReady?.Invoke ();
   }
   HWindow mHWnd;
   GLFWDispatcher mDispatcher;

   // Properties ---------------------------------------------------------------
   /// <summary>Gets the DPI scaling</summary>
   public float DPIScale {
      get {
         GetWindowContentScale (mHWnd, out float x, out float y);
         return (x + y) / 2;
      }
   }

   /// <summary>Frame-buffer size</summary>
   public (int DX, int DY) FramebufferSize {
      get {
         GetFramebufferSize (mHWnd, out var width, out var height);
         return (width, height);
      }
   }

   /// <summary>Get / set mazimized state of the window</summary>
   public bool Maximized {
      get => GetWindowAttribute (mHWnd, EWindowAttr.Maximized);
      set { if (value) MaximizeWindow (mHWnd); else RestoreWindow (mHWnd); }
   }

   /// <summary>Specifies the monitor that the window is full-screen on</summary>
   public Monitor Monitor => new (GetWindowMonitor (mHWnd));

   /// <summary>Gets / sets the position of the top-left corner of the window (including decorations)</summary>
   public (int X, int Y) Position {
      get {
         GetWindowPosition (mHWnd, out var x, out var y);
         GetWindowFrameSize (mHWnd, out var left, out var top, out var _, out var _);
         return new (x - left, y - top);
      }
      set {
         GetWindowFrameSize (mHWnd, out var left, out var top, out var _, out var _);
         SetWindowPosition (mHWnd, value.X + left, value.Y + top);
      }
   }

   /// <summary>Returns true if the window should be closed</summary>
   public bool ShouldClose => WindowShouldClose (mHWnd);

   /// <summary>Gets / Sets the size of the window (including decorations)</summary>
   public (int DX, int DY) Size {
      get {
         GetWindowSize (mHWnd, out var width, out var height);
         GetWindowFrameSize (mHWnd, out var left, out var top, out var right, out var bottom);
         return (width + left + right, height + top + bottom);
      }
      set {
         GetWindowFrameSize (mHWnd, out var left, out var top, out var right, out var bottom);
         SetWindowSize (mHWnd, value.DX - left - right, value.DY - top - bottom);
      }
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Center the window on its monitor (only for non-full-screen monitor)</summary>
   public void CenterOnScreen () {
      if (Maximized) return;
      var monitor = Monitor; if (monitor.IsNone) monitor = Monitor.Primary;
      var (screen, size) = (monitor.VideoMode, Size);
      Position = ((screen.Width - size.DX) / 2, (screen.Height - size.DY) / 2);
   }

   /// <summary>Runs the message loop</summary>
   /// <param name="wait">If set, waits for events or repaint request after each frame
   /// Otherwise, runs a continuous render loop</param>
   public void Run (bool wait) {
      while (!ShouldClose) {
         var (dx, dy) = FramebufferSize;
         mDispatcher.ProcessWorkQueue ();
         Draw (dx, dy);
         Swap (wait);
      }
   }   

   public virtual void Draw (int cx, int dy) 
      => GLFWHost.OnPaint?.Invoke (cx, dy);

   // Nested types -------------------------------------------------------------
   /// <summary>Window creation flags</summary>
   [Flags]
   public enum EFlags {
      None = 0,
      /// <summary>Is the window visible?</summary>
      Visible = 1 << 0,
      /// <summary>Can the window be resized by dragging the corners?</summary>
      Resizeable = 1 << 1,
      /// <summary>Is the window 'decorated' with platform-specific caption, min-max buttons, system-menu etc</summary>
      Decorated = 1 << 2,
      /// <summary>Is this an always-on-top window (remains on top of all applications)</summary>
      AlwaysOnTop = 1 << 3,
      /// <summary>Is the window created maximized at startup?</summary>
      Maximized = 1 << 4,
      /// <summary>Is the window background potentially transparent</summary>
      /// For this to actually exhibit transparency, you need to set the scene's clear color
      /// to have an alpha value less than 255
      Transparent = 1 << 5,

      /// <summary>The default window creation flags: Visible, Resizable, Decorated</summary>
      Default = Visible | Resizeable | Decorated
   }

   // Implementation -----------------------------------------------------------
   void SetWindowHints (EFlags flags) {
      // Set some common hints for the OpenGL profile creation
      WindowHint (Hint.ClientApi, ClientApi.OpenGL);
      WindowHint (Hint.ContextVersionMajor, 3);
      WindowHint (Hint.ContextVersionMinor, 3);
      WindowHint (Hint.OpenglProfile, GLProfile.Compatibility);
      WindowHint (Hint.Doublebuffer, true);

      WindowHint (Hint.Visible, (flags & EFlags.Visible) > 0);
      WindowHint (Hint.Resizable, (flags & EFlags.Resizeable) > 0);
      WindowHint (Hint.Decorated, (flags & EFlags.Decorated) > 0);
      WindowHint (Hint.Floating, (flags & EFlags.AlwaysOnTop) > 0);
      WindowHint (Hint.Maximized, (flags & EFlags.Maximized) > 0);
      WindowHint (Hint.TransparentFramebuffer, (flags & EFlags.Transparent) > 0);
   }

   // Swap contents after render is complete.
   // If wait is true, then we wait for an event before returning (so we don't do
   // continuous rendering). If wait is false, we return immediately so we are rendering
   // continuously
   void Swap (bool wait) {
      SwapBuffers (mHWnd);
      if (wait) WaitEvents (); else PollEvents ();
   }
}
#endregion
