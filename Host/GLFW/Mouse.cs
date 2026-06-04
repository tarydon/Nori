using System.Reactive;
namespace Nori;
using static GLFW;

class GLFWMouse : IMouse {
   public IObservable<MouseClickInfo> Clicks => mClicks ??= new (HWnd);
   MouseClickWrap? mClicks;

   public IObservable<Unit> Leave => throw new NotImplementedException ();
   public IObservable<Unit> Lost => throw new NotImplementedException ();

   public IObservable<MouseWheelInfo> Wheel => mWheel ??= new (HWnd);
   MouseWheelWrap? mWheel;

   public Vec2S Pos => throw new NotImplementedException ();

   public bool TryCapture () => throw new NotImplementedException ();

   public IObservable<Vec2S> Moves => mMoves ??= new (HWnd);
   MouseMoveWrap? mMoves;

   internal static HWindow HWnd;
}

#region class MouseClickWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-button-press, mouse-button-release</summary>
class MouseClickWrap : EventWrapper<MouseClickInfo> {
   public MouseClickWrap (HWindow w) : base (w) => mCallback = Callback;
   protected override void Connect (bool connect) => SetMouseButtonCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, EMouseButton b, EKeyState s, EModifier m) {
      GetCursorPosition (mWindow, out double x, out double y);
      Vec2S pos = new ((int)Math.Round (x), (int)Math.Round (y));
      Push (new MouseClickInfo (b, pos, m, s));
   }
   readonly MouseButtonCallback mCallback;
}
#endregion

#region class MouseEnterWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events when the mouse enters / leaves the window</summary>
class MouseEnterWrap : EventWrapper<bool> {
   public MouseEnterWrap (HWindow w) : base (w) => mCallback = Callback;
   protected override void Connect (bool connect) => SetCursorEnterCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, bool enter) => Push (enter);
   BoolCallback mCallback;
}
#endregion

#region class MouseMoveWrap ------------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-moves</summary>
class MouseMoveWrap : EventWrapper<Vec2S> {
   public MouseMoveWrap (HWindow w) : base (w) => mCallback = Callback;
   protected override void Connect (bool connect) => SetCursorPosCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, double x, double y) => Push (new ((short)(x + 0.5), (short)(y + 0.5)));
   readonly Vec2FCallback mCallback;
}
#endregion

#region class MouseWheelWrap -----------------------------------------------------------------------
/// <summary>Helper used to generate events for mouse-wheel rotations</summary>
class MouseWheelWrap : EventWrapper<MouseWheelInfo> {
   public MouseWheelWrap (HWindow w) : base (w) => mCallback = Callback;
   protected override void Connect (bool connect) => SetScrollCallback (mWindow, connect ? mCallback : null);
   void Callback (HWindow _, double __, double yWheel) {
      GetCursorPosition (mWindow, out double cx, out double cy);
      Vec2S pos = new ((int)Math.Round (cx), (int)Math.Round (cy));
      Push (new MouseWheelInfo ((int)Math.Round (yWheel), pos));
   }
   readonly Vec2FCallback mCallback;
}
#endregion
