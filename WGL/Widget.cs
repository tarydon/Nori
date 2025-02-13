// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Widget.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class MouseDragger -------------------------------------------------------------------------
/// <summary>MouseDragger is a base class for all click and drag operations</summary>
public abstract class MouseDragger {
   // Constructor --------------------------------------------------------------
   /// <summary>MouseDragger constructor</summary>
   /// Create a MouseDragger when you notice a mouse-down event. 
   /// - At that point, the Start() method is called. 
   /// - Then, for every drag of the mouse, Move() is called.
   /// - Finally, if the mouse button is released, End() is called (completion!).
   /// - If the ESC key is pressed, or capture is lost, Cancel() is called (cancellation!).
   public MouseDragger (Vec2S anchor) {
      mAnchor = mLast = mPt = anchor;
      // If we can't capture the mouse, we're done (no overrides like Start/End etc will be fired)
      if (!HW.CaptureMouse (true)) return;
      mObservers = new (
         // Forward mouse-moves to the Move override
         HW.MouseMoves.Subscribe (pt => { mLast = mPt; Move (mPt = pt); }),
         // When the mouse button is released, stop dragging (completed)
         HW.MouseClicks.Where (a => a.IsRelease).Subscribe (_ => Finish (true)),
         // When the ESC key is pressed, stop dragging (cancelled)
         HW.Keys.Where (a => a.IsPress (EKey.Escape)).Subscribe (_ => Finish (false)),
         // Whem mouse-capture is lost, stop dragging (cancelled)
         HW.MouseLost.Subscribe (_ => Finish (false)));

      // All captures set up, call the Start and Move event to initiate the drag cycle
      // NOTE: Do this asynchronously because this is still inside the constructor
      Lib.Post (() => { Start (); Move (mPt); });
   }

   // Overrides ----------------------------------------------------------------
   /// <summary>This is called when the drag operation starts (the start point is in Anchor)</summary>
   protected virtual void Start () { }
   /// <summary>This is called each time the mouse is moved (the previous move position is in LastPt)</summary>
   protected abstract void Move (Vec2S pt);
   /// <summary>This is called when the drag is successfully completed (mouse button released)</summary>
   protected virtual void End () { }
   /// <summary>This is called when the drag is cancelled (by Esc, or by capture being lost)</summary>
   protected virtual void Cancel () { }

   // Properties ---------------------------------------------------------------
   /// <summary>The anchor point is where the mouse was originally clicked (starting the drag)</summary>
   public Vec2S Anchor => mAnchor;
   Vec2S mAnchor;

   /// <summary>The location of the previous Move event</summary>
   public Vec2S LastPt => mLast;
   Vec2S mLast, mPt;

   // Implementation -----------------------------------------------------------
   void Finish (bool completed) {
      if (completed) End (); else Cancel ();
      mObservers?.Dispose ();
   }

   MultiDispose? mObservers;
}
#endregion

// class SceneManipulator --------------------------------------------------------------------------
public class SceneManipulator {
   public SceneManipulator () {
      HW.MouseClicks.Where (a => a.IsPress).Subscribe (OnMouseClick);
   }

   void OnMouseClick (MouseClickInfo mi) {
      if (Lux.UIScene is Scene sc) {
         if (mi.Button == EMouseButton.Left && sc is Scene3 sc3) new SceneRotator (sc3, mi.Position);
         if (mi.Button == EMouseButton.Middle) new ScenePanner (sc, mi.Position);
      }
   }
}

// class SceneRotator ------------------------------------------------------------------------------
class SceneRotator (Scene3 scene, Vec2S anchor) : MouseDragger (anchor) {
   protected override void Start () => (mx0, mz0) = mScene.Viewpoint;
   readonly Scene3 mScene = scene;
   double mx0, mz0;

   protected override void Move (Vec2S pt) {
      double x = mx0 + (pt.Y - Anchor.Y), z = mz0 + (pt.X - Anchor.X);
      mScene.Viewpoint = (x, z);
   }
}

// clsas ScenePanner -------------------------------------------------------------------------------
class ScenePanner (Scene scene, Vec2S anchor) : MouseDragger (anchor) {
   protected override void Start () => mPan0 = mScene.PanVector;
   readonly Scene mScene = scene;
   Vector2 mPan0;

   protected override void Move (Vec2S pt) {
      // Compute the new pan vector in OpenGL clip coordinates (where we 
      // assume the window extents goes from (-1,-1) to (+1,+1) with (-1,-1) at
      // bottom left
      double dx = 2.0 * (pt.X - Anchor.X) / Lux.Viewport.X, dy = 2.0 * (Anchor.Y - pt.Y) / Lux.Viewport.Y;
      mScene.PanVector = mPan0 + new Vector2 (dx, dy);
   }
}
