// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Mouse.cs
// ║║║║╬║╔╣║ Implements GLFWMouse : a GLFW-specific implementation of IMouse
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static GLFW;

#region class GLFWMouse ----------------------------------------------------------------------------
/// <summary>GLFW-specific implementation of the IMouse interface</summary>
/// In GLFW, mouse messages are handled by setting up callbacks on the GLFW main window. We use the
/// EventWrapper(T) helper from Nori to convert these callbacks into an IObservable stream. For each
/// of the different IObservables, we implement specific classes like MouseClickWrap, MouseMovesWrap
/// etc to handle the details of how to subscribe/unsubscribe for that particular callback (and
/// these classes use the GLFW functions to do this)
class GLFWMouse : IMouse {
   // Interface ----------------------------------------------------------------
   public IObservable<MouseClickInfo> Clicks => mClicks ??= new (HWnd);
   public IObservable<bool> Enter => mEnter ??= new (HWnd);
   public IObservable<MouseWheelInfo> Wheel => mWheel ??= new (HWnd);
   public Vec2S Pos => GetCursorPosition (HWnd);
   public IObservable<Vec2S> Moves => mMoves ??= new (HWnd);

   // Private data -------------------------------------------------------------
   // When the GLFWMouse is created, we don't yet have this main window handle. However, 
   // very soon after that, the main window is created by the client code and at that point the
   // window handle is set into this variable. Subsequently, the observables can be invoked and used. 
   internal static HWindow HWnd;
   MouseClickWrap? mClicks;
   MouseEnterWrap? mEnter;
   MouseWheelWrap? mWheel;
   MouseMoveWrap? mMoves;
}
#endregion

#region class MouseClickWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-button-press, mouse-button-release</summary>
class MouseClickWrap : EventWrapper<MouseClickInfo> {
   public MouseClickWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly MouseButtonCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetMouseButtonCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, EMouseButton b, EKeyState s, EKeyModifier m) => Push (new MouseClickInfo (b, GetCursorPosition (mWindow), m, s));
}
#endregion

#region class MouseEnterWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events when the mouse enters / leaves the window</summary>
class MouseEnterWrap : EventWrapper<bool> {
   public MouseEnterWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly BoolCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetCursorEnterCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, bool enter) => Push (enter);
}
#endregion

#region class MouseMoveWrap ------------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-moves</summary>
class MouseMoveWrap : EventWrapper<Vec2S> {
   public MouseMoveWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly Vec2FCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetCursorPosCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, double x, double y) => Push (new ((short)(x + 0.5), (short)(y + 0.5)));
}
#endregion

#region class MouseWheelWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-wheel rotations</summary>
class MouseWheelWrap : EventWrapper<MouseWheelInfo> {
   public MouseWheelWrap (HWindow w) => (mWindow, mCallback) = (w, Callback);
   readonly Vec2FCallback mCallback;
   readonly HWindow mWindow;

   protected override void Connect (bool connect) => SetScrollCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, double __, double yWheel) => Push (new MouseWheelInfo ((int)Math.Round (yWheel), GetCursorPosition (mWindow)));
}
#endregion
