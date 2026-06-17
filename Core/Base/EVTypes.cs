// ────── ╔╗
// ╔═╦╦═╦╦╬╣ EVTypes.cs
// ║║║║╬║╔╣║ Implements some types used by the HW events interface (key-info, mouse-click-info etc)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region enum EKey ----------------------------------------------------------------------------------
/// <summary>Enumeration used in the KeyInfo struct to describe which key on the keyboard was pressed</summary>
public enum EKey {
   Unknown = -1,
   /// <summary>The '0' .. '9' digits on the top row</summary>
   D0 = '0', D1, D2, D3, D4, D5, D6, D7, D8, D9,
   /// <summary>Alphabet keys 'A' .. 'Z'</summary>
   A = 'A', B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
   /// <summary>Function keys 'F1' .. 'F12'</summary>
   F1 = 290, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
   /// <summary>Keys '0' .. '9' on the numeric keypad</summary>
   N0 = 320, N1, N2, N3, N4, N5, N6, N7, N8, N9,

   /// <summary>Punctuation and symbols</summary>
   Space = ' ', Apostrophe = '\'', Comma = ',', Minus = '-', Period = '.', Slash = '/',
   SemiColon = ';', Equal = '=', LeftBracket = '[', Backslash = '\\', RightBracket = ']',
   GraveAccent = '`',

   /// <summary>Navigation and other keys</summary>
   Escape = 256, Enter = 257, Tab = 258, Backspace = 259, Insert = 260, Delete = 261,
   Right = 262, Left = 263, Down = 264, Up = 265, PageUp = 266, PageDown = 267, Home = 268,
   End = 269, PrintScreen = 283, Pause = 284,

   /// <summary>Operators on numeric keypad</summary>
   NDecimal = 330, NDivide = 331, NMultiply = 332, NSubtract = 333, NAdd = 334, NEnter = 335,
   NEqual = 336,

   /// <summary>Modifier keys</summary>
   LShift = 340, LCtrl = 341, LAlt = 342, LSuper = 343, RShift = 344, RCtrl = 345, RAlt = 346,
   RSuper = 347, Menu = 348, CapsLock = 280, ScrollLock = 281, NumLock = 282,
}
#endregion

#region enum EKeyModifier --------------------------------------------------------------------------
/// <summary>Used in KeyInfo to specify which modifiers (Shift / Control / Alt) are being held down</summary>
[Flags]
public enum EKeyModifier : byte { None = 0, Shift = 1, Control = 2, Alt = 4 }
#endregion

#region enum EKeyState -----------------------------------------------------------------------------
/// <summary>Used in KeyInfo to distinguish key presses from key releases</summary>
public enum EKeyState : byte { Released = 0, Pressed = 1, Repeat = 2 }
#endregion

#region struct KeyInfo -----------------------------------------------------------------------------
/// <summary>Data about a key being pressed or released</summary>
public readonly struct KeyInfo (EKey key, EKeyModifier modifier, EKeyState state) {
   public bool IsPress () => State == EKeyState.Pressed;
   public bool IsPress (EKey key) => key == Key && State == EKeyState.Pressed;

   /// <summary>Which key on the keyboard was pressed or released</summary>
   public readonly EKey Key = key;
   /// <summary>Which modifiers (like Shift, Ctrl, Alt) are being held down</summary>
   public readonly EKeyModifier Modifier = modifier;
   /// <summary>Is the key being pressed or released</summary>
   public readonly EKeyState State = state;

   public override string ToString () => $"{State} {Key} ({Modifier})";
}
#endregion

#region enum EMouseButton --------------------------------------------------------------------------
/// <summary>Used by MouseInfo to specify which mouse button is clicked</summary>
public enum EMouseButton : byte { Left, Right, Middle }
#endregion

#region struct MouseClickInfo ----------------------------------------------------------------------
/// <summary>Data about a mouse button being pressed or released</summary>
public readonly struct MouseClickInfo (EMouseButton button, Vec2S position, EKeyModifier modifier, EKeyState state) {
   public bool IsPress => State == EKeyState.Pressed;
   public bool IsLeftPress => State == EKeyState.Pressed && Button == EMouseButton.Left;
   public bool IsRelease => State == EKeyState.Released;

   /// <summary>Which mouse button is pressed or released</summary>
   public readonly EMouseButton Button = button;
   /// <summary>The position where the mouse was clicked</summary>
   public readonly Vec2S Position = position;
   /// <summary>Which modifiers (like Shift, Control, Alt) are being held down</summary>
   public readonly EKeyModifier Modifier = modifier;
   /// <summary>Is the mouse button being pressed, or being released</summary>
   public readonly EKeyState State = state;

   public override string ToString () => $"{Button} {State} @ {Position} ({Modifier})";
}
#endregion

#region struct MouseWheelInfo ----------------------------------------------------------------------
/// <summary>Data about a mouse-wheel being rotated up or down</summary>
public readonly struct MouseWheelInfo (int delta, Vec2S position) {
   /// <summary>How much has the mouse-wheel been rotated (+ or - value)</summary>
   public readonly int Delta = delta;
   /// <summary>The position where the mouse wheel was rotated</summary>
   public readonly Vec2S Position = position;

   public override string ToString () => $"{Delta} @ {Position}";
}
#endregion
