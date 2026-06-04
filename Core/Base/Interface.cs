// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Interfaces.cs
// ║║║║╬║╔╣║ Various interface definitions used (and exported) by Nore.Core
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

using System.Reactive;
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

public interface IOpenGL {
   public Ptr GetGLProcAddress (string name);
   public Action<int, int> OnPaint { set; }
   public float DPIScale { get; }
   public void Redraw ();
}

public interface IMouse {
   /// <summary>Observe this to know when the mouse is clicked</summary>
   public IObservable<MouseClickInfo> Clicks { get; }
   /// <summary>Observe this to know when the mouse enters/leaves the client area</summary>
   public IObservable<bool> Enter { get; }
   /// <summary>Observe this to know when mouse-capture is lost</summary>
   public IObservable<Unit> Lost { get; }
   /// <summary>Observe this to know when the mouse is moved</summary>
   public IObservable<Vec2S> Moves { get; }
   /// <summary>Observe this to know when mouse-wheel is rotated</summary>
   public IObservable<MouseWheelInfo> Wheel { get; }
   /// <summary>
   /// The current position of the mouse
   /// </summary>
   public Vec2S Pos { get; }
}

public interface IKeyboard {
   /// <summary>Observe this to know when a key is pressed or released</summary>
   public IObservable<KeyInfo> Keys { get; }
   /// <summary>Tells us which modifiers (SHIFT/CTRL/ALT) are being held down now</summary>
   public EModifier Modifiers { get; }
   /// <summary>Observe this to get the text that was typed</summary>
   public IObservable<string> Text { get; }
}

public interface IDispatcher {
   public bool CheckAccess ();
   public void Post (Action act);
   public Task InvokeAsync (Action act);
   public Task<T> InvokeAsync<T> (Func<T> func);

   public IDisposable Timer (TimeSpan interval, bool repeat, Action callback) {
      var t = new Timer (_ => Post (callback), null, interval, interval);
      return new TimerDisposer (t);
   }

   public void VerifyAccess () {
      if (!CheckAccess ()) 
         throw new InvalidOperationException ("Code should be on the UI thread");
   }
   
   public void Send (Action act) {
      if (CheckAccess ()) { act (); return; }
      InvokeAsync (act).GetAwaiter ().GetResult ();
   }

   public T Send<T> (Func<T> act) {
      if (CheckAccess ()) return act ();
      return InvokeAsync (act).GetAwaiter ().GetResult ();
   }

   public Task Yield () {
      return InvokeAsync (() => { });
   }

   class TimerDisposer : IDisposable {
      public TimerDisposer (Timer t) { mTimer = t; mList.Add (this); }
      public void Dispose () { if (mList.Remove (this)) mTimer.Dispose (); }
      readonly Timer mTimer;
      static List<TimerDisposer> mList = [];
   }
}
