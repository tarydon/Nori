// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Keyboard.cs
// ║║║║╬║╔╣║ Implements GLFWKeyboard : a GLFW-specific implementation of the IKeyboard interface
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static GLFW;

#region class GLFWKeyboard -------------------------------------------------------------------------
/// <summary>GLFWKeyboard is a GLFW specific implementation of IKeyboard</summary>
/// In GLFW, keyboard messages are handled by setting up callbacks on the GLFW main window. We use the
/// EventWrapper(T) helper from Nori to convert these callbacks into an IObservable stream. This is done
/// by the classes below like KeysWrap, which know how to use GLFW functions to sign up for / release
/// the corresponding GLFW callbacks. 
class GLFWKeyboard : IKeyboard {
   // Implementation -----------------------------------------------------------
   public IObservable<KeyInfo> Keys => mKeys ??= new (HWnd);
   public IObservable<char> Chars => mChars ??= new (HWnd);

   public EKeyModifier Modifiers {
      get {
         EKeyModifier mod = EKeyModifier.None;
         if (GetKey (HWnd, EKey.LShift) == EKeyState.Pressed) mod |= EKeyModifier.Shift;
         if (GetKey (HWnd, EKey.RShift) == EKeyState.Pressed) mod |= EKeyModifier.Shift;
         if (GetKey (HWnd, EKey.LCtrl) == EKeyState.Pressed) mod |= EKeyModifier.Control;
         if (GetKey (HWnd, EKey.RCtrl) == EKeyState.Pressed) mod |= EKeyModifier.Control;
         if (GetKey (HWnd, EKey.LAlt) == EKeyState.Pressed) mod |= EKeyModifier.Alt;
         if (GetKey (HWnd, EKey.RAlt) == EKeyState.Pressed) mod |= EKeyModifier.Alt;
         return mod;
      }
   }

   // Private data -------------------------------------------------------------
   internal static HWindow HWnd;
   CharsWrap? mChars;
   KeysWrap? mKeys;
}
#endregion

#region class KeyPressWrap -------------------------------------------------------------------------
/// <summary>Helper used to generate a stream of key-press, key-release events</summary>
class KeysWrap : EventWrapper<KeyInfo> {
   public KeysWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly KeyCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetKeyCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, EKey k, int code, EKeyState st, EKeyModifier m) => Push (new KeyInfo (k, m, st));
}
#endregion

#region class CharsWrap ----------------------------------------------------------------------------
/// <summary>Helper used to generate Unicode characters from key-presses</summary>
class CharsWrap : EventWrapper<char> {
   public CharsWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly CharCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetCharCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, uint code) { foreach (var ch in char.ConvertFromUtf32 ((int)code)) Push (ch); }
}
#endregion
