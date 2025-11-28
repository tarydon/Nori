using System.Text;
using Nori.Internal;
namespace Nori;
using static GLFW;

public class Window {
   // Constructors -------------------------------------------------------------
   public Window (int cx, int cy, string title) {
      SetWindowHints ();
      byte[] bTitle = Encoding.UTF8.GetBytes (title);
      Array.Resize (ref bTitle, bTitle.Length + 1);
      mHWnd = CreateWindow (cx, cy, bTitle, HMonitor.None, HWindow.None);
      MakeContextCurrent (mHWnd);
      SwapInterval (1);
   }
   HWindow mHWnd;

   // Properties ---------------------------------------------------------------
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

   /// <summary>
   /// Runs the message loop
   /// </summary>
   /// <param name="wait">If set, waits for events or repaint request after each frame
   /// Otherwise, runs a continuous render loop</param>
   public void Run (bool wait) {
      while (!ShouldClose) {
         var (dx, dy) = FramebufferSize;
         Draw (dx, dy);
         Swap (wait);
      }
   }

   public virtual void Draw (int cx, int dy) {
      GL.ClearColor (0.3f, 0.6f, 0.9f, 1);
      GL.Clear (EBuffer.Color | EBuffer.Depth | EBuffer.Stencil);
   }

   // Implementation -----------------------------------------------------------
   void SetWindowHints () {
      // Set some common hints for the OpenGL profile creation
      WindowHint (Hint.ClientApi, ClientApi.OpenGL);
      WindowHint (Hint.ContextVersionMajor, 3);
      WindowHint (Hint.ContextVersionMinor, 3);
      WindowHint (Hint.OpenglProfile, GLProfile.Compatibility);
      WindowHint (Hint.Doublebuffer, true);
      WindowHint (Hint.Decorated, true);
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
