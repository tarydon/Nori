// ────── ╔╗
// ╔═╦╦═╦╦╬╣ HWEvent.cs
// ║║║║╬║╔╣║ Implements hardware events (keyboard, mouse) in a platform independent manner
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using GLPanel = UserControl;

#region class EventWrapper<T> ----------------------------------------------------------------------
/// <summary>The base class for various classes that convert events to IObservables</summary>
/// This class provides the basis implementation of IObservable(T), where T is typically
/// some kind of event related information like KeyInfo, MouseInfo etc. Derived classes must
/// implement Connect to connect and disconnect from the underlying event source (typically
/// by attaching or detaching an event handler). Even time the derived class detects the event
/// being fired, it can simply call Push(T) on this type and that will take care of the
/// IObservable dispatch. This class also handles subscribe and implements an internal type
/// (EventWrapper.Disposer) that handles the disconnection correctly.
abstract class EventWrapper<T> : IObservable<T> {
   // Methods ------------------------------------------------------------------
   /// <summary>Implements the IObservable contract</summary>
   /// When the first subscriber connects, this calls Connect(true) on its derived
   /// class, which in turn will actually connect an event handler to the underlying
   /// event. This returns an instance of the Disposer (see below) that when disposed
   /// disconnects the observer from our list of observers.
   public IDisposable Subscribe (IObserver<T> observer) {
      (mObservers ??= []).Add (observer);
      if (mObservers.Count == 1) Connect (true);
      return new Disposer (this, observer);
   }
   List<IObserver<T>>? mObservers;

   // Implementation -----------------------------------------------------------
   // Must be implemented by derived class to actually connect / disconnect from the event
   protected abstract void Connect (bool connect);

   // Used internally by derived clases to push an item (KeyInfo / MouseInfo etc)
   // to all observers. Note that even when we have multiple observers connected, there is
   // only event handler that is signed up (since we call Connect only when the first observer
   // signs up). This push method will then distribute the event to all observers that have
   // signed up.
   // NOTE: This is done in a last-come, first-served method. The most recent observer to
   // sign up will get the first look at the event.
   protected void Push (T item) {
      if (mObservers == null) return;
      for (int i = mObservers.Count - 1; i >= 0; i--)
         mObservers[i].OnNext (item);
   }

   // Called by the Disposer type (see below) to remove this particular observer from
   // the list of observers this class maintains. Once the last observer is gone, it
   // calls Connect(false) to disconnect the event handler
   void Remove (IObserver<T> observer) {
      if (mObservers?.Count > 0) {
         mObservers.Remove (observer);
         if (mObservers.Count == 0) Connect (false);
      }
   }

   // Nested types -------------------------------------------------------------
   // An implementation of IDisposable that removes this observer from its owner
   class Disposer (EventWrapper<T> owner, IObserver<T> observer) : IDisposable {
      public void Dispose () => owner.Remove (observer);
   }
}
#endregion

#region class HW -----------------------------------------------------------------------------------
/// <summary>This class represents the low-level hardware and provides a number of event streams</summary>
public static class HW {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Is the cursor visible when over the OpenGL surface?
   /// </summary>
   public static bool CursorVisible { set => Nori.Panel.CursorVisible = value; }

   /// <summary>Is the ALT key currently pressed?</summary>
   public static bool IsAltDown => (GetKeyState (VK_ALT) & PRESSED) != 0;
   /// <summary>Is the CONTROL key currently pressed?</summary>
   public static bool IsCtrlDown => (GetKeyState (VK_CONTROL) & PRESSED) != 0;
   /// <summary>Is the SHIFT key currently pressed?</summary>
   public static bool IsShiftDown => (GetKeyState (VK_SHIFT) & PRESSED) != 0;

   // Methods ------------------------------------------------------------------
   public static bool CaptureMouse (bool capture) {
      var panel = Panel; if (panel == null) return false;
      panel.Capture = capture;
      return panel.Capture;
   }

   /// <summary>
   /// Force a redraw of the OpenGL panel
   /// </summary>
   public static void Redraw () => Nori.Panel.It?.Redraw ();

   // Observable points --------------------------------------------------------
   public static IObservable<int> MouseLost => mLost ??= new ();
   static CaptureLostWrap? mLost;

   /// <summary>Subscribe to this to be notified when the mouse leaves the client area</summary>
   public static IObservable<int> MouseLeave => mLeave ??= new ();
   static MouseLeaveWrap? mLeave;

   /// <summary>Subscribe to this to watch key-press and key-release events</summary>
   public static IObservable<KeyInfo> Keys => mKeys ??= new ();
   static KeysWrap? mKeys;

   /// <summary>Subscribe to this to watch for mouse button click and release events</summary>
   public static IObservable<MouseClickInfo> MouseClicks => mMouseClicks ??= new ();
   static MouseClicksWrap? mMouseClicks;

   /// <summary>Subscribe to this to watch for all mouse move events</summary>
   public static IObservable<Vec2S> MouseMoves => mMouseMoves ??= new ();
   static MouseMovesWrap? mMouseMoves;

   /// <summary>Subscribe to this to watch for mouse-wheel events</summary>
   public static IObservable<MouseWheelInfo> MouseWheel => mMouseWheel ??= new ();
   static MouseWheelWrap? mMouseWheel;

   // Implementation -----------------------------------------------------------
   const int PRESSED = 0x8000;
   const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_ALT = 0x12;
   [DllImport ("user32.dll")]
   static extern ushort GetKeyState (int key);

   internal static GLPanel? Panel { get; set; }
}
#endregion

#region class CaptureLostWrap ----------------------------------------------------------------------
/// <summary>EventWrapper implementation to handle the mouse-capture-lost event</summary>
class CaptureLostWrap : EventWrapper<int> {
   protected override void Connect (bool connect) {
      var panel = HW.Panel; if (panel == null) return;
      if (connect) panel.MouseCaptureChanged += OnCaptureLost;
      else panel.MouseCaptureChanged -= OnCaptureLost;
   }

   void OnCaptureLost (object? sender, EventArgs e)
      => Push (0);
}
#endregion

#region MouseLeaveWrap -----------------------------------------------------------------------------
/// <summary>EventWrapper implementation to handle the mouse-leave event</summary>
class MouseLeaveWrap : EventWrapper<int> {
   protected override void Connect (bool connect) {
      var panel = HW.Panel; if (panel == null) return;
      if (connect) panel.MouseLeave += OnMouseLeave;
      else panel.MouseLeave -= OnMouseLeave;
   }

   void OnMouseLeave (object? sender, EventArgs e)
      => Push (0);
}
#endregion

#region class KeysWrap -----------------------------------------------------------------------------
/// <summary>EventWrapper implementation that handles keydown and keyup events (used by HW.Keys)</summary>
class KeysWrap : EventWrapper<KeyInfo> {
   // Overrides ----------------------------------------------------------------
   /// <summary>Connect sets up / takes down event handlers for KeyDown and KeyUp events on the GL surface</summary>
   /// <param name="connect"></param>
   protected override void Connect (bool connect) {
      var panel = HW.Panel; if (panel == null) return;
      if (connect) { panel.KeyDown += OnKeyDown; panel.KeyUp += OnKeyUp; }
      else { panel.KeyDown -= OnKeyDown; panel.KeyUp -= OnKeyUp; }
   }

   // Implementation -----------------------------------------------------------
   void OnKeyDown (object? _, KeyEventArgs e) => Process (e, EKeyState.Pressed);
   void OnKeyUp (object? _, KeyEventArgs e) => Process (e, EKeyState.Released);

   // We convert the Windows Keys enumeration to our own EKey enum (since we want to standardize
   // this across platforms), and push the KeyInfo structs that we construct from that
   void Process (KeyEventArgs e, EKeyState state) {
      if (!mMap.TryGetValue (e.KeyCode, out EKey key)) key = (EKey)e.KeyCode;
      var mods = EKeyModifier.None;
      if ((e.Modifiers & Keys.Shift) > 0) mods |= EKeyModifier.Shift;
      if ((e.Modifiers & Keys.Control) > 0) mods |= EKeyModifier.Control;
      if ((e.Modifiers & Keys.Alt) > 0) mods |= EKeyModifier.Alt;
      Push (new (key, mods, state));
      // REFINE: Use e.Modifiers to distinguish between Enter and Numpad-Enter etc
   }
   // Internal dictionary used to map Windows.Keys enumeration values to our EKey values.
   // If any entries are missing in this dictionary, then the numerical values of the Windows.Keys
   // and Nori.EKey enumerations for those are identical (for example, all the alphabet keys).
   static readonly Dictionary<Keys, EKey> mMap = new () {
      [Keys.Escape] = EKey.Escape, [Keys.F1] = EKey.F1, [Keys.F2] = EKey.F2, [Keys.F3] = EKey.F3,
      [Keys.F4] = EKey.F4, [Keys.F5] = EKey.F5, [Keys.F6] = EKey.F6, [Keys.F7] = EKey.F7,
      [Keys.F8] = EKey.F8, [Keys.F9] = EKey.F9, [Keys.F10] = EKey.F10, [Keys.F11] = EKey.F11,
      [Keys.F12] = EKey.F12, [Keys.Scroll] = EKey.Scroll, [Keys.Oemtilde] = EKey.Tilde,
      [Keys.OemMinus] = EKey.Hyphen, [Keys.Oemplus] = EKey.Equals, [Keys.OemOpenBrackets] = EKey.OpenBracket,
      [Keys.OemCloseBrackets] = EKey.CloseBracket, [Keys.OemPipe] = EKey.Backslash, [Keys.LWin] = EKey.Windows,
      [Keys.RWin] = EKey.Windows, [Keys.ControlKey] = EKey.Ctrl, [Keys.ShiftKey] = EKey.Shift,
      [Keys.Menu] = EKey.Alt, [Keys.Capital] = EKey.CapsLock, [Keys.Apps] = EKey.Menu, [Keys.Pause] = EKey.Pause,
      [Keys.Insert] = EKey.Insert, [Keys.Home] = EKey.Home, [Keys.PageUp] = EKey.PageUp,
      [Keys.PageDown] = EKey.PageDown, [Keys.Delete] = EKey.Delete, [Keys.End] = EKey.End,
      [Keys.Up] = EKey.Up, [Keys.Down] = EKey.Down, [Keys.Left] = EKey.Left, [Keys.Right] = EKey.Right,
      [Keys.NumLock] = EKey.NumLock, [Keys.Divide] = EKey.NDivide, [Keys.Multiply] = EKey.NMultiply,
      [Keys.Subtract] = EKey.NSubtract, [Keys.Add] = EKey.NAdd, [Keys.Decimal] = EKey.NPeriod,
      [Keys.NumPad0] = EKey.NPad0, [Keys.NumPad1] = EKey.NPad1, [Keys.NumPad2] = EKey.NPad2,
      [Keys.NumPad3] = EKey.NPad3, [Keys.NumPad4] = EKey.NPad4, [Keys.Clear] = EKey.NPad5,
      [Keys.NumPad6] = EKey.NPad6, [Keys.NumPad7] = EKey.NPad7, [Keys.NumPad8] = EKey.NPad8,
      [Keys.NumPad9] = EKey.NPad9, [Keys.Space] = EKey.Space
   };
}
#endregion

#region class MouseClicksWrap ----------------------------------------------------------------------
/// <summary>Handles mouse click events (used by HW.MouseClicks)</summary>
class MouseClicksWrap : EventWrapper<MouseClickInfo> {
   protected override void Connect (bool connect) {
      var panel = HW.Panel; if (panel == null) return;
      if (connect) { panel.MouseDown += OnMouseDown; panel.MouseUp += OnMouseUp; }
      else { panel.MouseDown -= OnMouseDown; panel.MouseUp -= OnMouseUp; }
   }

   void OnMouseDown (object? sender, MouseEventArgs e) => Process (e, EKeyState.Pressed);
   void OnMouseUp (object? sender, MouseEventArgs e) => Process (e, EKeyState.Released);

   void Process (MouseEventArgs e, EKeyState state) {
      if (!mMap.TryGetValue (e.Button, out EMouseButton btn)) return;
      var mods = EKeyModifier.None;
      if (HW.IsCtrlDown) mods |= EKeyModifier.Control;
      if (HW.IsShiftDown) mods |= EKeyModifier.Shift;
      if (HW.IsAltDown) mods |= EKeyModifier.Alt;
      Vec2S position = new (e.X, e.Y);
      Push (new (btn, position, mods, state));
   }

   static readonly Dictionary<MouseButtons, EMouseButton> mMap = new () {
      [MouseButtons.Left] = EMouseButton.Left, [MouseButtons.Middle] = EMouseButton.Middle,
      [MouseButtons.Right ] = EMouseButton.Right
   };
}
#endregion

#region class MouseMovesWrap -----------------------------------------------------------------------
/// <summary>Handles mouse-move events (used by HW.MouseMoves)</summary>
class MouseMovesWrap : EventWrapper<Vec2S> {
   protected override void Connect (bool connect) {
      var panel = HW.Panel; Debug.Assert (panel != null);
      if (connect) panel.MouseMove += OnMouseMove;
      else panel.MouseMove -= OnMouseMove;
   }

   void OnMouseMove (object? sender, MouseEventArgs e)
      => Push (new (e.X, e.Y));
}
#endregion

#region class MouseWheelWrap -----------------------------------------------------------------------
/// <summary>Handles mouse-wheel events (used by HW.MouseWheel)</summary>
class MouseWheelWrap : EventWrapper<MouseWheelInfo> {
   protected override void Connect (bool connect) {
      var panel = HW.Panel; if (panel == null) return;
      if (connect) panel.MouseWheel += OnMouseWheel;
      else panel.MouseWheel -= OnMouseWheel;
   }

   void OnMouseWheel (object? sender, MouseEventArgs e)
      => Push (new (e.Delta, new (e.X, e.Y)));
}
#endregion
