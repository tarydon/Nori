// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MouseDragger.cs
// ║║║║╬║╔╣║ Implements the MouseDragger base class and some widgets derived from that
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

#region class SceneManipulator ---------------------------------------------------------------------
/// <summary>Provides standard mouse handling for Lux Scenes (both 2D and 3D)</summary>
/// The following interaction is provided:
/// - When the left mouse button is clicked and dragged, the scene is rotated (only for 3D)
/// - When the middle mouse button is clicked and dragged, the scene is panned
/// - When the mouse-wheel is rolled up / down, the scene is zoomed in / out about that point
/// - When Ctrl+E is pressed on the keyboard, a 'Zoom-Extents' operation is done
public class SceneManipulator {
   // Constructor --------------------------------------------------------------
   public SceneManipulator () {
      HW.MouseClicks.Where (a => a.IsPress).Subscribe (OnMouseClick);
      HW.MouseWheel.Subscribe (OnMouseWheel);
      HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey);
   }

   // Implementation -----------------------------------------------------------
   // When Ctrl+E is pressed, do a zoom-extents
   void OnKey (KeyInfo ki) {
      if (ki.Key == EKey.E && ki.Modifier == EKeyModifier.Control)
         Lux.UIScene?.ZoomExtents ();
   }

   // Start rotating when the left mouse button is clicked (if the current scene is 3D)
   // Start panning when the middle mouse button is clicked 
   void OnMouseClick (MouseClickInfo mi) {
      if (Lux.UIScene is Scene sc) {
         if (mi.Button == EMouseButton.Left && sc is Scene3 sc3) new SceneRotator (sc3, mi.Position);
         if (mi.Button == EMouseButton.Middle) new ScenePanner (sc, mi.Position);
      }
   }

   // Zoom in/out when the mouse wheel is rotated
   void OnMouseWheel (MouseWheelInfo mw) 
      => Lux.UIScene?.Zoom (mw.Position, mw.Delta < 0 ? 0.95 : (1 / 0.95));
}
#endregion

#region class SceneRotator -------------------------------------------------------------------------
/// <summary>MouseDragger widget used to rotate a 3D scene</summary>
class SceneRotator (Scene3 scene, Vec2S anchor) : MouseDragger (anchor) {
   // Overrides ----------------------------------------------------------------
   // At start, capture the initial viewpoint of the Scene
   protected override void Start () => (mx0, mz0) = mScene.Viewpoint;
   readonly Scene3 mScene = scene;
   double mx0, mz0;

   // Subsequently, adjust the viewpoint based on the vector from AnchorPt to
   // the current mouse Pt
   protected override void Move (Vec2S pt) {
      double x = mx0 + (pt.Y - Anchor.Y), z = mz0 + (pt.X - Anchor.X);
      // If we are close to any of the orthogonal views (view along left-view, right-view or top-view),
      // snap to that view precisely. This will enable us to rotate the view to a required 'side-view'.
      var (xSnap, zSnap) = (x.Round (90.0), z.Round (90.0));
      if (x.EQ (xSnap, 5) && z.EQ (zSnap, 5)) { x = xSnap; z = zSnap; }
      mScene.Viewpoint = (x, z);
   }
}
#endregion

#region class ScenePanner --------------------------------------------------------------------------
/// <summary>MouseDragger widget used to pan the scene</summary>
class ScenePanner (Scene scene, Vec2S anchor) : MouseDragger (anchor) {
   // Overrides ----------------------------------------------------------------
   // At start, capture the initial pan-vector of the scene
   protected override void Start () => mPan0 = mScene.PanVector;
   readonly Scene mScene = scene;
   Vector2 mPan0;

   // Subsequently, on Move, adjust the pan vector in OpenGL clip space coordinates
   // where we assume the window extents goes from (-1,-1) at the bottom left to
   // (+1,+1) at top right
   protected override void Move (Vec2S pt) {
      double dx = 2.0 * (pt.X - Anchor.X) / Lux.Viewport.X, dy = 2.0 * (Anchor.Y - pt.Y) / Lux.Viewport.Y;
      mScene.PanVector = mPan0 + new Vector2 (dx, dy);
   }
}
#endregion
