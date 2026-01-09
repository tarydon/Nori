// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MouseDragger.cs
// ║║║║╬║╔╣║ Implements the MouseDragger base class and some widgets derived from that
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
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
   protected MouseDragger (Vec2S anchor) {
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
   readonly Vec2S mAnchor;

   /// <summary>The location of the previous Move event</summary>
   public Vec2S LastPt => mLast;
   Vec2S mLast, mPt;

   // Implementation -----------------------------------------------------------
   void Finish (bool completed) {
      if (completed) End (); else Cancel ();
      mObservers?.Dispose ();
   }
   static MultiDispose? mObservers;
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

   public void ZoomExtents (bool animate) {
      if (mZoomExtentsAnimation == null && Lux.UIScene != null) {
         if (!animate) { Lux.UIScene.ZoomExtents (); return; }

         // If there is a wheel animation running, stop it
         mWheelAnimation?.Stop (this);

         // Snapshot the current scene, thread context, zoom, pan and start a stopwatch
         Scene scene = Lux.UIScene;
         SynchronizationContext context = SynchronizationContext.Current!;
         double startZoomFactor = scene.ZoomFactor; Vector2 startPan = scene.PanVector;
         Stopwatch sw = Stopwatch.StartNew ();

         // Every 16ms (approx 60fps), update the zoom and pan based on elapsed time.
         mZoomExtentsAnimation = Observable.Interval (TimeSpan.FromMilliseconds (16))
            .ObserveOn (context)
            .TakeWhile (_ => sw.ElapsedMilliseconds < AnimationTime && Lux.UIScene == scene)
            .Subscribe (_ => { // On tick interpolate the zoom and pan based on elapsed time
               double f = (sw.ElapsedMilliseconds / AnimationTime);
               scene.ZoomFactor = startZoomFactor + f * (1.0 - startZoomFactor);
               scene.PanVector = startPan + (Vector2.Zero - startPan) * f;
            }, () => { // On completion, ensure we are exactly at zoom-extents
               Lux.UIScene?.ZoomExtents ();
               mZoomExtentsAnimation = null;
            });
      }
   }
   IDisposable? mZoomExtentsAnimation = null;

   // Implementation -----------------------------------------------------------
   // When Ctrl+E is pressed, do a zoom-extents
   void OnKey (KeyInfo ki) {
      if (ki.Key == EKey.E && ki.Modifier == EKeyModifier.Control)
         ZoomExtents (true);
   }

   // Start rotating when the left mouse button is clicked (if the current scene is 3D)
   // Start panning when the middle mouse button is clicked
   void OnMouseClick (MouseClickInfo mi) {
      if (Lux.UIScene is { } sc) {
         if (mi.Button == EMouseButton.Left && sc is Scene3 sc3) {
            if (Lux.Pick (mi.Position) is { } vnode) {
               if (vnode.Obj != null) sc.Picked (vnode.Obj);
            } else
               new SceneRotator (sc3, mi.Position);
         }
         if (mi.Button == EMouseButton.Middle) 
            new ScenePanner (sc, mi.Position);
      }
   }

   // Zoom in/out when the mouse wheel is rotated
   void OnMouseWheel (MouseWheelInfo mw) {
      if (Lux.UIScene != null && mZoomExtentsAnimation == null) {
         if (mWheelAnimation != null) mWheelAnimation.Continue (mw);// If there is a running animation, append to it.
         else mWheelAnimation = new ZoomAnim (this, mw); // Create a new animation.
      }
   }
   ZoomAnim? mWheelAnimation = null; // Any running zoom animation. Will be null if there is no active animation.

   // Creates the storyboard for a zoom in/out animation using mousewheel
   class ZoomAnim {
      /// <summary>Creates the animation object and starts the animation on the scene.</summary>
      /// <param name="owner">The owner scene manipulator who is starting this zoom animation.</param>
      public ZoomAnim (SceneManipulator owner, MouseWheelInfo mw) {
         // Snapshot the current scene, mouse position, zoom factors and start a stopwatch
         (mScene, mPosition) = (Lux.UIScene!, mw.Position);
         mStartZoomFactor = mScene.ZoomFactor;
         mTargetZoomFactor = mStartZoomFactor * (mw.Delta < 0 ? 0.8 : 1.0 / 0.8);
         mSW = Stopwatch.StartNew ();

         SynchronizationContext context = SynchronizationContext.Current!;
         mAction = Observable.Interval (TimeSpan.FromMilliseconds (16))
            .ObserveOn (context) // Switch to UI thread
            .TakeWhile (_ => mSW.ElapsedMilliseconds < AnimationTime && Lux.UIScene == mScene) // Take only while elapsed is less than animation duration.
            .Subscribe ((_) => { // On tick
               double target = mStartZoomFactor + (mSW.ElapsedMilliseconds / AnimationTime) * (mTargetZoomFactor - mStartZoomFactor);
               mScene.Zoom (mPosition, target / mScene.ZoomFactor);
            }, () => { // On completion
               if (Lux.UIScene == mScene) {
                  mScene.Zoom (mPosition, mTargetZoomFactor / mScene.ZoomFactor);
                  owner.mWheelAnimation = null; // Set it to null on owner.
               }
            });
      }

      /// <summary>Call this to continue zoom animation when user zoomed even while the previous zoom
      /// operation was still animating.</summary>
      /// <param name="position">The new mouse position.</param>
      /// <param name="zoomIn">Whether we are zooming in or out</param>
      public void Continue (MouseWheelInfo mw) {
         mPosition = mw.Position;
         mStartZoomFactor = mScene.ZoomFactor;
         mTargetZoomFactor *= (mw.Delta < 0 ? 0.9 : 1.0 / 0.9);
         mSW.Restart ();
      }

      /// <summary>Stops the animation in its tracks</summary>
      public void Stop (SceneManipulator owner) { mAction.Dispose (); owner.mWheelAnimation = null; }

      readonly Scene mScene;
      readonly Stopwatch mSW;
      readonly IDisposable mAction;
      double mStartZoomFactor, mTargetZoomFactor;
      Vec2S mPosition;
   }
   const double AnimationTime = 200; // milliseconds
}
#endregion

#region class SceneRotator -------------------------------------------------------------------------
/// <summary>MouseDragger widget used to rotate a 3D mScene</summary>
class SceneRotator (Scene3 mScene, Vec2S anchor) : MouseDragger (anchor) {
   // Overrides ----------------------------------------------------------------
   // At start, capture the initial viewpoint of the Scene
   protected override void Start () => (mx0, mz0) = mScene.Viewpoint;
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
/// <summary>MouseDragger widget used to pan the mScene</summary>
class ScenePanner (Scene mScene, Vec2S anchor) : MouseDragger (anchor) {
   // Overrides ----------------------------------------------------------------
   // At start, capture the initial pan-vector of the mScene
   protected override void Start () => mPan0 = mScene.PanVector;
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
