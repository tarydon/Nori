// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Keyboard.cs
// ║║║║╬║╔╣║ WPFKeyboard is an implementation of the IKeyboard interface for WPF
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.InteropServices;
namespace Nori;

#region class WPFKeyboard --------------------------------------------------------------------------
/// <summary>WPF-specific implementation of the IKeyboard interface</summary>
/// Internally, the WPF OpenGL host Nori uses creates a Windows-Forms control to host the GL
/// context (since we need an actual HWND). When we want to observe keyboard events, we set up
/// event-handlers on that underlying Windows Forms control (stored here as Panel). 
/// 
/// We use the Nori EventWrapper(T) helper class to convert those events into IObservable streams.
/// This is done by actual implementations of EventWrapper such as KeysWrap which need to just 
/// override the Connect method to set up / take down the event handlers. In addition, we also 
/// use the GetAsyncKeyState Win32 API function (from USER32.dll) to get the instantaneous state
/// of the modifier keys. 
class WPFKeyboard : IKeyboard {
   // Interface ----------------------------------------------------------------
   public IObservable<KeyInfo> Keys => mKeys ??= new (Panel);

   public EKeyModifier Modifiers {
      get {
         EKeyModifier mods = EKeyModifier.None;
         if (Check (LMENU) || Check (RMENU)) mods |= EKeyModifier.Alt;
         if (Check (LSHIFT) || Check (RSHIFT)) mods |= EKeyModifier.Shift;
         if (Check (LCONTROL) || Check (RCONTROL)) mods |= EKeyModifier.Control;
         return mods;
      }
   }

   public IObservable<char> Chars => mChars ??= new (Panel);

   // Implementation -----------------------------------------------------------
   const int PRESSED = 0x8000;
   const int LMENU = 0xA4, RMENU = 0xA5, LSHIFT = 0xA0, RSHIFT = 0xA1,
      LCONTROL = 0xA2, RCONTROL = 0xA3, NUMPAD0 = 0x60, NUMPAD1 = 0x61, NUMPAD2 = 0x62,
      NUMPAD3 = 0x63, NUMPAD4 = 0x64, NUMPAD5 = 0x65, NUMPAD6 = 0x66, NUMPAD7 = 0x67,
      NUMPAD8 = 0x68, NUMPAD9 = 0x69;
   [DllImport ("user32.dll")] static extern short GetAsyncKeyState (int key);

   static bool Check (int code) => (GetAsyncKeyState (code) & PRESSED) != 0;
   static internal UserControl Panel = null!;
   CharsWrap? mChars;
   KeysWrap? mKeys;
}
#endregion

#region class CharsWrap ----------------------------------------------------------------------------
/// <summary>EventWrapper implementation that handles KeyPress events and converts them to a stream of strings</summary>
class CharsWrap : EventWrapper<char> {
   public CharsWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   protected override void Connect (bool connect) {
      if (connect) mPanel.KeyPress += OnKeyPress;
      else mPanel.KeyPress -= OnKeyPress;
   }

   void OnKeyPress (object? _, KeyPressEventArgs e) => Push (e.KeyChar);
}
#endregion

#region class KeysWrap -----------------------------------------------------------------------------
/// <summary>EventWrapper implementation that handles keydown and keyup events (used by HW.Keys)</summary>
class KeysWrap : EventWrapper<KeyInfo> {
   public KeysWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   // Overrides ----------------------------------------------------------------
   /// <summary>Connect sets up / takes down event handlers for KeyDown and KeyUp events on the GL surface</summary>
   protected override void Connect (bool connect) {
      if (connect) { mPanel.KeyDown += OnKeyDown; mPanel.KeyUp += OnKeyUp; } 
      else { mPanel.KeyDown -= OnKeyDown; mPanel.KeyUp -= OnKeyUp; }
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
   }
   // Internal dictionary used to map Windows.Keys enumeration values to our EKey values.
   // If any entries are missing in this dictionary, then the numerical values of the Windows.Keys
   // and Nori.EKey enumerations for those are identical (for example, all the alphabet keys).
   static readonly Dictionary<Keys, EKey> mMap = new () {
      [Keys.Escape] = EKey.Escape, [Keys.F1] = EKey.F1, [Keys.F2] = EKey.F2, [Keys.F3] = EKey.F3,
      [Keys.F4] = EKey.F4, [Keys.F5] = EKey.F5, [Keys.F6] = EKey.F6, [Keys.F7] = EKey.F7,
      [Keys.F8] = EKey.F8, [Keys.F9] = EKey.F9, [Keys.F10] = EKey.F10, [Keys.F11] = EKey.F11,
      [Keys.F12] = EKey.F12, [Keys.OemMinus] = EKey.Minus, [Keys.Oemplus] = EKey.Equal, 
      [Keys.OemPipe] = EKey.Backslash, [Keys.Capital] = EKey.CapsLock, [Keys.Pause] = EKey.Pause, 
      [Keys.Insert] = EKey.Insert, [Keys.Home] = EKey.Home, [Keys.PageUp] = EKey.PageUp, 
      [Keys.PageDown] = EKey.PageDown, [Keys.Delete] = EKey.Delete, [Keys.End] = EKey.End, 
      [Keys.Up] = EKey.Up, [Keys.Down] = EKey.Down, [Keys.Left] = EKey.Left, [Keys.Right] = EKey.Right, 
      [Keys.NumLock] = EKey.NumLock, [Keys.Divide] = EKey.NDivide, [Keys.Multiply] = EKey.NMultiply, 
      [Keys.Subtract] = EKey.NSubtract, [Keys.Add] = EKey.NAdd, [Keys.NumPad0] = EKey.N0, 
      [Keys.NumPad1] = EKey.N1, [Keys.NumPad2] = EKey.N2, [Keys.NumPad3] = EKey.N3, [Keys.NumPad4] = EKey.N4, 
      [Keys.NumPad5] = EKey.N5, [Keys.NumPad6] = EKey.N6, [Keys.NumPad7] = EKey.N7, [Keys.NumPad8] = EKey.N8, 
      [Keys.NumPad9] = EKey.N9, [Keys.Space] = EKey.Space, [Keys.PrintScreen] = EKey.PrintScreen, 
      [Keys.Scroll] = EKey.ScrollLock, [Keys.Tab] = EKey.Tab, [Keys.Oemtilde] = EKey.GraveAccent, 
      [Keys.Back] = EKey.Backspace, [Keys.Oem4] = EKey.LeftBracket, [Keys.Oem6] = EKey.RightBracket, 
      [Keys.OemSemicolon] = EKey.SemiColon, [Keys.OemQuotes] = EKey.Apostrophe, [Keys.Enter] = EKey.Enter,
      [Keys.ShiftKey] = EKey.LShift, [Keys.Menu] = EKey.LAlt, 
      [Keys.Oemcomma] = EKey.Comma, [Keys.OemPeriod] = EKey.Period, [Keys.Oem2] = EKey.Slash,
      [Keys.ControlKey] = EKey.LCtrl, [Keys.LWin] = EKey.LSuper, [Keys.RWin] = EKey.RSuper, 
      [Keys.Apps] = EKey.Menu, [Keys.Clear] = EKey.N5
   };
}
#endregion
