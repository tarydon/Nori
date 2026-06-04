namespace Nori;

using System.Reflection;
using static GLFW;

class GLFWKeyboard : IKeyboard {
   public IObservable<KeyInfo> Keys => mKeys ??= new (HWnd);
   KeysWrap? mKeys;

   public EModifier Modifiers => throw new NotImplementedException ();
   public IObservable<string> Text => throw new NotImplementedException ();

   internal static HWindow HWnd;
}

#region class KeyPressWrap -------------------------------------------------------------------------
/// <summary>Helper used to generate a stream of key-press, key-release events</summary>
class KeysWrap : EventWrapper<KeyInfo> {
   public KeysWrap (HWindow w) : base (w) {
      mCallback = Callback;

      var bf = BindingFlags.Static | BindingFlags.Public;
      Type t1 = typeof (EKey), t2 = typeof (EGLFWKey);
      Dictionary<string, EKey> dict = [];
      foreach (var f in t1.GetFields (bf))
         dict[f.Name] = (EKey)f.GetValue (null)!;
      foreach (var f1 in typeof (EGLFWKey).GetFields (bf)) {
         if (dict.TryGetValue (f1.Name, out var f2)) {
            var key = (EGLFWKey)f1.GetValue (null)!;
            mMap[key] = f2;
         }
      }
   }
   readonly KeyCallback mCallback;
   readonly Dictionary<EGLFWKey, EKey> mMap = [];

   protected override void Connect (bool connect) => SetKeyCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, EGLFWKey glk, int code, EKeyState st, EModifier m) {
      if (!mMap.TryGetValue (glk, out var k)) k = (EKey)(glk + 1000);
      Push (new KeyInfo (k, m, st));
   }
}
#endregion

public enum EGLFWKey : short {
   D0 = (short)'0', D1, D2, D3, D4, D5, D6, D7, D8, D9,
   A = (short)'A', B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
   N0 = 320, N1, N2, N3, N4, N5, N6, N7, N8, N9,
   F1 = 290, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

   Escape = 256, Space = (short)' ', Hyphen = 45, Equals = 61, Backspace = 259, Tab = 258,
   Apostrophe = 39, Grave = 96, Comma = 44, Minus = 45, Period = 46, Slash = 47,
   
   //Apostrophe = 39, Comma = 44, Minus = 45, Period = 46, Slash = 47, 
   //SemiColon = 59, Equal = 61, 
   //LeftBracket = 91, Backslash = 92, RightBracket = 93, GraveAccent = 96, World1 = 161,
   //World2 = 162, Enter = 257, Tab = 258, Insert = 260,
   //Delete = 261, Right = 262, Left = 263, Down = 264, Up = 265, PageUp = 266, PageDown = 267,
   //Home = 268, End = 269, CapsLock = 280, ScrollLock = 281, NumLock = 282, PrintScreen = 283,
   //Pause = 284,  
   //NumpadDecimal = 330, NumpadDivide = 331, NumpadMultiply = 332,
   //NumpadSubtract = 333, NumpadAdd = 334, NumpadEnter = 335, NumpadEqual = 336,
   //LeftShift = 340, LeftControl = 341, LeftAlt = 342, LeftSuper = 343, RightShift = 344,
   //RightControl = 345, RightAlt = 346, RightSuper = 347, Menu = 348,

   //Tilde
};
