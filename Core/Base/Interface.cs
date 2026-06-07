// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Interfaces.cs
// ║║║║╬║╔╣║ Various interface definitions used (and exported) by Nore.Core
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Threading.Tasks;
using System.Threading;
using Ptr = nint;

#region interface IEQuable<T> ----------------------------------------------------------------------
/// <summary>Interface implemented by classes / structs that have an EQ comparision method</summary>
public interface IEQuable<in T> {
   public bool EQ (T other);
}
#endregion

#region interface IIndexed -------------------------------------------------------------------------
/// <summary>IIndexed implements a class that has a 32-bit index</summary>
public interface IIndexed {
   public int Idx { get; set; }
}
#endregion

#region interface IStmLocator ----------------------------------------------------------------------
/// <summary>The IStmLocator interface provides the basis for the Lib.OpenRead and related functions</summary>
/// It allows us to open a stream using an abstract filename like "nori:GL/Shader/Pixel.frag",
/// without having to worry about where that file is stored. It could be different on developer
/// machines, and different on installations on different operating systems. In general, we will
/// never try to open any standard resource files using raw filenames, but should always use a
/// stream-locator to open the file. In this example, the _prefix_ "nori:" routes this call to
/// a specific stream locator for that virtual drive, and that would have been registered earlier
/// using Lib.Register(IStmLocator).
public interface IStmLocator {
   public string Prefix { get; }
   public Stream? Open (string name);
}
#endregion

#region interface IOpenGL --------------------------------------------------------------------------
/// <summary>IOpenGL provides platform-independent access to OpenGL</summary>
/// When a Nori.Host is initialized, it creates an appropriate implementation of this interface
/// and plugs it into Hub.OpenGL. 
public interface IOpenGL {
   /// <summary>Obtains the DPI scaling (for a typical 96 dpi display, this is 1.0)</summary>
   public float DPIScale { get; }
   /// <summary>Get a pointer to an OpenGL function</summary>
   /// All pointers to OpenGL functions are obtained using this, and this includes classic
   /// 'old fashioned' functions like "glEnable", "glBegin" etc or more 'modern' functions 
   /// such as "glCreateShader". 
   public Ptr GetGLProcAddress (string name);
   /// <summary>A rendering engine will set this to point to the 'paint' function</summary>
   /// For the Lux rendering engine, this points to a function that draws the current scene.
   /// The two parameters passed to the function are the width & height of the frame-buffer,
   /// in pixels (the glViewport)
   public Action<int, int> OnPaint { set; }
   /// <summary>Issues a redraw</summary>
   public void Redraw ();
   /// <summary>Sets whether the cursor is visible over the rendering surface or not</summary>
   public bool CursorVisible { set; }
}
#endregion

#region interface IMouse ---------------------------------------------------------------------------
/// <summary>IMouse provides platform-independent access to the mouse</summary>
/// When a Nori.Host is initialized, it creates an appropriate implementation of this interface
/// and plugs it into Hub.Mouse. Subsequently, client code for any UI application can subscribe
/// to Hub.Mouse.Clicks to be informed of mouse clicks.
public interface IMouse {
   /// <summary>Observe this to know when the mouse is clicked</summary>
   public IObservable<MouseClickInfo> Clicks { get; }
   /// <summary>Observe this to know when the mouse enters/leaves the client area</summary>
   public IObservable<bool> Enter { get; }
   /// <summary>Observe this to know when the mouse is moved</summary>
   /// See the notes below on IMouse.Pos for details on the coordinate system. 
   public IObservable<Vec2S> Moves { get; }
   /// <summary>Observe this to know when mouse-wheel is rotated</summary>
   public IObservable<MouseWheelInfo> Wheel { get; }
   /// <summary>The current position of the mouse</summary>
   /// All mouse positions (on all platforms) are in pixels with the top left corner pixel of the
   /// rendering surface as (0,0). +X towards the right, +Y towards the bottom of the screen. 
   /// Note that Y is opposite to the OpenGL coordinate direction, and is more aligned for use
   /// with UI. 
   public Vec2S Pos { get; }
}
#endregion

#region interface IKeyboard ------------------------------------------------------------------------
/// <summary>IKeyboard provides platform-independent access to the keyboard</summary>
/// When a Nori.Host is initialized, it creates an appropriate implementation of this interface
/// and plugs it into Hub.Keyboard. Subsequently, client code for any UI application can subscribe
/// to Hub.Keyboard.Keys to watch for key presses/releases. 
public interface IKeyboard {
   /// <summary>Observe this to know when a key is pressed or released</summary>
   /// This includes all keys (whether they generate a character code or not), for example like
   /// the CTRL or CAPSLOCK keys.
   public IObservable<KeyInfo> Keys { get; }
   /// <summary>Tells us which modifiers (SHIFT/CTRL/ALT) are being held down now</summary>
   public EKeyModifier Modifiers { get; }
   /// <summary>Observe this to get the characters that were typed</summary>
   /// This is called only when the key being pressed/released generates a character, and the
   /// character being generated depends on the current state of some modifiers like SHIFT, 
   /// CAPSLOCK etc. 
   public IObservable<char> Chars { get; }

   /// <summary>Is the Shift modifier key currently pressed?</summary>
   public bool IsShiftDown => (Modifiers & EKeyModifier.Shift) > 0;
   /// <summary>Is the Ctrl modifier key currently pressed?</summary>
   public bool IsCtrlDown => (Modifiers & EKeyModifier.Control) > 0;
   /// <summary>Is the Alt modifier key currently pressed?</summary>
   public bool IsAltDown => (Modifiers & EKeyModifier.Alt) > 0;
}
#endregion

#region interface IDispatcher ----------------------------------------------------------------------
/// <summary>IDispatcher provides platform-independent dispatcher functionality (similar to WPF.Dispatcher)</summary>
/// When a Nori.Host is initialized, it creates an appropriate implementation of this interface
/// and plugs it into Hub.Dispatcher. This is rarely needed by client code directly, but is used 
/// internally by methods like Lib.Post() to execute some code on the UI thread. 
/// 
/// In addition to this, a Nori.Host will also create a SynchronizationContext on top of this 
/// dispatcher and mount it as the current sync-context. This is needed for async/await to work
/// correctly. 
public interface IDispatcher {
   /// <summary>Returns true if code is currently running on the UI thread</summary>
   public bool CheckAccess ();

   /// <summary>Schedules an action asynchronously on the UI thread (returns immediately with a Task)</summary>
   /// This returns a task that can be waited upon to know when that action is complete. 
   /// This is rarely used directly, but is the underpinning of Send(Action). 
   public Task InvokeAsync (Action act);

   /// <summary>Invokes a function asynchronously on the UI thread (returns immediately with a waitable Task)</summary>
   /// This returns a task that can be waited on to know when that function is complete and to
   /// fetch the result. This is rarely used directly, but is the underpinning of Send(Func(T))
   public Task<T> InvokeAsync<T> (Func<T> func);

   /// <summary>Posts an action to be performed on the UI thread, and returns immediately</summary>
   /// The action will be performed shortly, typically when the next frame is drawn
   /// (within about 1/60 of a second)
   public void Post (Action act);

   /// <summary>Executes an action on the UI thread, blocks until the action is complete</summary>
   public void Send (Action act) {
      if (CheckAccess ()) { act (); return; }
      InvokeAsync (act).GetAwaiter ().GetResult ();
   }

   /// <summary>Runs a function on the UI thead, blocks until it completes, and returns the result</summary>
   public T Send<T> (Func<T> act) {
      if (CheckAccess ()) return act ();
      return InvokeAsync (act).GetAwaiter ().GetResult ();
   }

   /// <summary>Starts a timer that executes the provided action after a specified time interval</summary>
   /// If the 'repeat' parameter is set true, the action is called repeatedly after each
   /// Interval. 
   public IDisposable Timer (TimeSpan interval, bool repeat, Action callback) 
      => new RepeatTimer (() => Post (callback), interval, repeat);

   /// <summary>Verifies if we are on the UI thread, and throws an exception if not</summary>
   /// This is useful when implementing UI code that should execute only on the UI thread
   public void VerifyAccess () {
      if (!CheckAccess ()) 
         throw new InvalidOperationException ("Code should be on the UI thread");
   }

   /// <summary>Provides a way to temporarily release execution to the current dispatcher</summary>
   /// Rarely used: only needed if there is some long-running code on the UI thread
   /// (for example a prolonged update of a control)
   public Task Yield () => InvokeAsync (() => { });

   // Implementation helper ................................
   // This is used to keep Timer objects alive until they are actually disposed. 
   class RepeatTimer : IDisposable {
      public RepeatTimer (Action callback, TimeSpan interval, bool repeat) {
         mRepeat = repeat; mForward = callback;
         mTimer = new (_ => Callback (), null, interval, repeat ? interval : Timeout.InfiniteTimeSpan);
         sActive.Add (this);
      }

      void Callback () { mForward (); if (!mRepeat) Dispose (); }
      public void Dispose () { if (sActive.Remove (this)) mTimer.Dispose (); }

      static readonly HashSet<RepeatTimer> sActive = [];
      readonly Action mForward;
      readonly bool mRepeat;
      readonly Timer mTimer;
   }
}
#endregion
