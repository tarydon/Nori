// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Keyboard.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static GLFW;

class GLFWKeyboard : IKeyboard {
   public IObservable<KeyInfo> Keys => mKeys ??= new (HWnd);
   KeysWrap? mKeys;

   public EModifier Modifiers {
      get {
         EModifier mod = EModifier.None;
         if (GetKey (HWnd, EKey.LShift) == EKeyState.Pressed) mod |= EModifier.Shift;
         if (GetKey (HWnd, EKey.RShift) == EKeyState.Pressed) mod |= EModifier.Shift;
         if (GetKey (HWnd, EKey.LCtrl) == EKeyState.Pressed) mod |= EModifier.Ctrl;
         if (GetKey (HWnd, EKey.RCtrl) == EKeyState.Pressed) mod |= EModifier.Ctrl;
         if (GetKey (HWnd, EKey.LAlt) == EKeyState.Pressed) mod |= EModifier.Alt;
         if (GetKey (HWnd, EKey.RAlt) == EKeyState.Pressed) mod |= EModifier.Alt;
         return mod;
      }
   }

   public IObservable<string> Text => mChars ??= new (HWnd);
   CharsWrap? mChars;
   internal static HWindow HWnd;
}

#region class KeyPressWrap -------------------------------------------------------------------------
/// <summary>Helper used to generate a stream of key-press, key-release events</summary>
class KeysWrap : EventWrapper<KeyInfo> {
   public KeysWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly KeyCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetKeyCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, EKey k, int code, EKeyState st, EModifier m) => Push (new KeyInfo (k, m, st));
}
#endregion

#region class CharsWrap ----------------------------------------------------------------------------
/// <summary>Helper used to generate Unicode characters from key-presses</summary>
class CharsWrap : EventWrapper<string> {
   public CharsWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly CharCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetCharCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, uint code) => Push (char.ConvertFromUtf32 ((int)code));
}
#endregion
