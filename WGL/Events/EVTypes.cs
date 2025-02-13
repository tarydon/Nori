// ────── ╔╗
// ╔═╦╦═╦╦╬╣ EVTypes.cs
// ║║║║╬║╔╣║ Implements some types used by the HW events interface (key-info, mouse-click-info etc)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region enum EKey ----------------------------------------------------------------------------------
/// <summary>Enumeration used in the KeyInfo struct to describe which key on the keyboard was pressed</summary>
public enum EKey : short {
   D0 = (short)'0', D1, D2, D3, D4, D5, D6, D7, D8, D9,
   A = (short)'A', B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

   NPad0 = 128, NPad1, NPad2, NPad3, NPad4, NPad5, NPad6, NPad7, NPad8, NPad9,

   Escape = 27, Backspace = 8, Tilde = (short)'~', Hyphen = (short)'-', Equals = (short)'=',
   Tab = 9, OpenBracket = (short)'[', CloseBracket = (short)']', Backslash = (short)'\\',
   Semicolon = (short)';', Quote = (short)'\'', Enter = 13, Space = 32,

   F1 = 140, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

   Scroll = 168, CapsLock, Windows, Pause, Shift, Control, Alt, Menu, Insert, Home, PageUp,
   Delete, End, PageDown, Up, Left, Down, Right, NumLock, NDivide, NMultiply, NSubtract, NAdd,
   NEnter, NPeriod,
};
#endregion

#region enum EKeyModifier --------------------------------------------------------------------------
/// <summary>Used in KeyInfo to specify which modifiers (Shift / Control / Alt) are being held down</summary>
[Flags]
public enum EKeyModifier : byte { None = 0, Shift = 1, Control = 2, Alt = 4, }
#endregion

#region enum EKeyState -----------------------------------------------------------------------------
/// <summary>Used in KeyInfo to distinguish key presses from key releases</summary>
public enum EKeyState : byte { Pressed = 1, Released = 2 }
#endregion

#region struct KeyInfo -----------------------------------------------------------------------------
/// <summary>Data about a key being pressed or released</summary>
public readonly struct KeyInfo {
   public KeyInfo (EKey key, EKeyModifier modifier, EKeyState state) => (Key, Modifier, State) = (key, modifier, state);

   /// <summary>Which key on the keyboard was pressed or released</summary>
   public readonly EKey Key;
   /// <summary>Which modifiers (like Shift, Ctrl, Alt) are being held down</summary>
   public readonly EKeyModifier Modifier;
   /// <summary>Is the key being pressed or released</summary>
   public readonly EKeyState State;
}
#endregion
