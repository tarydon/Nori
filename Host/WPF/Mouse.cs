using System.Reactive;
using System.Windows.Controls.Ribbon.Primitives;
namespace Nori;

class WPFMouse : IMouse {
   public IObservable<MouseClickInfo> Clicks => mClicks ??= new (Panel);
   MouseClicksWrap? mClicks;

   public IObservable<Vec2S> Moves => mMoves ??= new (Panel);
   MouseMovesWrap? mMoves;

   public IObservable<MouseWheelInfo> Wheel => mWheel ??= new (Panel);
   MouseWheelWrap? mWheel;

   public Vec2S Pos {
      get {
         var pt = Panel.PointToClient (Cursor.Position);
         return new (pt.X, pt.Y);
      }
   }

   public IObservable<bool> Enter => mEnter ??= new (Panel);
   MouseEnterWrap? mEnter;

   internal static UserControl Panel = null!;
}

#region class MouseMovesWrap -----------------------------------------------------------------------
/// <summary>Handles mouse-move events (used by IMouse.Moves)</summary>
class MouseMovesWrap : EventWrapper<Vec2S> {
   public MouseMovesWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   protected override void Connect (bool connect) {
      if (connect) mPanel.MouseMove += OnMouseMove;
      else mPanel.MouseMove -= OnMouseMove;
   }

   void OnMouseMove (object? sender, MouseEventArgs e) => Push (new (e.X, e.Y));
}
#endregion

#region class MouseClicksWrap ----------------------------------------------------------------------
/// <summary>Handles mouse click events (used by IMouse.Clicks)</summary>
class MouseClicksWrap : EventWrapper<MouseClickInfo> {
   public MouseClicksWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   protected override void Connect (bool connect) {
      if (connect) { mPanel.MouseDown += OnMouseDown; mPanel.MouseUp += OnMouseUp; } 
      else { mPanel.MouseDown -= OnMouseDown; mPanel.MouseUp -= OnMouseUp; }
   }

   void OnMouseDown (object? sender, MouseEventArgs e) => Process (e, EKeyState.Pressed);
   void OnMouseUp (object? sender, MouseEventArgs e) => Process (e, EKeyState.Released);

   void Process (MouseEventArgs e, EKeyState state) {
      if (!mMap.TryGetValue (e.Button, out EMouseButton btn)) return;
      var mods = Hub.Keyboard.Modifiers;
      Vec2S position = new (e.X, e.Y);
      Push (new (btn, position, mods, state));
   }

   static readonly Dictionary<MouseButtons, EMouseButton> mMap = new () {
      [MouseButtons.Left] = EMouseButton.Left, [MouseButtons.Middle] = EMouseButton.Middle,
      [MouseButtons.Right] = EMouseButton.Right
   };
}
#endregion

#region class MouseWheelWrap -----------------------------------------------------------------------
/// <summary>Handles mouse-wheel events (used by IMouse.Wheel)</summary>
class MouseWheelWrap : EventWrapper<MouseWheelInfo> {
   public MouseWheelWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   protected override void Connect (bool connect) {
      if (connect) mPanel.MouseWheel += OnMouseWheel;
      else mPanel.MouseWheel -= OnMouseWheel;
   }

   void OnMouseWheel (object? sender, MouseEventArgs e)
      => Push (new (Math.Sign (e.Delta), new (e.X, e.Y)));
}
#endregion

#region MouseEnterWrap -----------------------------------------------------------------------------
/// <summary>EventWrapper implementation to handle the mouse-leave event</summary>
class MouseEnterWrap : EventWrapper<bool> {
   public MouseEnterWrap (UserControl panel) => mPanel = panel;
   readonly UserControl mPanel;

   protected override void Connect (bool connect) {
      if (connect) { mPanel.MouseLeave += OnMouseLeave; mPanel.MouseEnter += OnMouseEnter; }
      else { mPanel.MouseLeave -= OnMouseLeave; mPanel.MouseEnter -= OnMouseEnter; }
   }

   void OnMouseLeave (object? sender, EventArgs e) => Push (false);
   void OnMouseEnter (object? sender, EventArgs e) => Push (true);
}
#endregion

