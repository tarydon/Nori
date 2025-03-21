// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Lux.cs
// ║║║║╬║╔╣║ The Lux class: public interface to the Lux rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
using System.Windows.Threading;
namespace Nori;

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   /// <summary>The scene that is currently being rendered (set only during a Render() call)</summary>
   public static Scene? Scene;

   /// <summary>The current scene that is bound to the visible viewport</summary>
   public static Scene? UIScene { 
      get => mUIScene; 
      set {
         mUIScene?.Detach ();
         mUIScene = value; Redraw (); 
      } 
   }
   static Scene? mUIScene;

   public static Vec2S Viewport => mViewport; 
   static Vec2S mViewport;

   internal static int Rung;

   /// <summary>Called when we start rendering a VNode (and it's subtree)</summary>
   /// The corresponding EndNode is called after the entire subtree under
   /// this VNode is completed rendering. Because of this, there could be multiple
   /// open 'BeginNode' calls whose EndNode is pending
   public static void BeginNode (VNode node) {
      mNodeStack.Push ((mVNode, mChanged));
      (mVNode, mChanged) = (node, ELuxAttr.None);
   }
   static Stack<(VNode?, ELuxAttr)> mNodeStack = [];

   static bool Get (ELuxAttr flags, ELuxAttr bit) => (flags & bit) != 0;
   static bool Set (ELuxAttr attr) {
      if ((mChanged & attr) != 0) return false;
      mChanged |= attr; return true; 
   }
   static ELuxAttr mChanged;

   public static bool PopAttr (ELuxAttr flags) {
      flags &= mChanged;
      if (flags != ELuxAttr.None) {
         if ((flags & ELuxAttr.Color) != 0) mColor = mColors.Pop ();
         if ((flags & ELuxAttr.LineType) != 0) mLineType = mLineTypes.Pop ();
         if ((flags & ELuxAttr.LineWidth) != 0) mLineWidth = mLineWidths.Pop ();
         if ((flags & ELuxAttr.LTScale) != 0) mLTScale = mLTScales.Pop ();
         if ((flags & ELuxAttr.PointSize) != 0) mPointSize = mPointSizes.Pop ();
         if ((flags & ELuxAttr.TypeFace) != 0) mTypeface = mTypefaces.Pop ();
         if ((flags & ELuxAttr.Xfm) != 0) mIDXfm = mIDXfms.Pop ();
         mChanged &= ~flags;
         return true;
      }
      return false;
   }

   public static void EndNode () {
      if (PopAttr (mChanged)) Rung++;
      (mVNode, mChanged) = mNodeStack.Pop ();
   }

   /// <summary>Creates the Lux rendering panel</summary>
   public static UIElement CreatePanel ()
      => Panel.It;

   public static Action? OnReady;
   static bool mReadyFired;

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      // Don't look at all this too closely - it is temporary code that will
      // later go away and be replaced by something more clean
      if (!mReadyFired) { mReadyFired = true; OnReady?.Invoke (); }

      mFrame++;
      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);
      StartFrame (panel.Size);
      GLState.StartFrame (panel.Size, mUIScene?.BgrdColor ?? Color4.Gray (96));
      RBatch.StartFrame ();
      Shader.StartFrame ();
      mUIScene?.Render (panel.Size);
      panel.EndRender ();
      Info.FrameOver ();
      FPS.FrameOver ();
      if (sRenderCompletes.Count > 0) Lib.Post (NextFrame);

      // Helpers ...........................................
      static void NextFrame () {
         var sFrametime = DateTime.Now;
         double elapsed = (sFrametime - sLastFrametime).TotalSeconds;
         for (int i = sRenderCompletes.Count - 1; i >= 0; i--)
            sRenderCompletes[i] (elapsed);
         sLastFrametime = sFrametime;
         Redraw ();
      }
   }
   static int mFrame;

   public static void Redraw ()
      => Panel.It.Redraw ();

   /// <summary>This is called to initiate 'continuous rendering'</summary>
   /// This function takes a 'callback' that will be invoked after each frame is rendered. Once
   /// this is started, Lux renders frames continuously, attempting to render at the monitor
   /// refresh rate (60 fps) if the hardware is fast enough. If Lux.VSync is turned off, then
   /// it renders at the maximum possible rate (regardless of monitor refresh rate). 
   /// 
   /// The 'elapsed-time' since the last time the callback was called (in seconds) is passed as 
   /// a parameter to the callback, which can use this parameter to adjust the positions
   /// of objects in the scene. Thus, it is possible to create simulation where the simulation
   /// speed is not dependent on the number of frames we render per second. 
   /// 
   /// It is possible to call StartContinuousRender any number of times, attaching different
   /// callbacks. The continuous-render goes on as long as at least one such callback is attached,
   /// and after each frame is rendered, all these callbacks are invoked. Once all these callbacks
   /// retire (by calling StopContinuousRender), we stop the render pump, and subsequent renders
   /// happen only on-demand (when the VNode tree changes, or the window size changes etc)
   public static void StartContinuousRender (Action<double> renderComplete) {
      sRenderCompletes.Add (renderComplete);
      if (sRenderCompletes.Count == 1) {
         // If this is the first render-complete function, start the backup timer running. 
         // We need this backup timer because the RenderComplete event is not always dependable.
         // Normally, if we are running at 60 fps, we should hit the render-complete each 16.66 ms,
         // and the timer would never fire.
         if (sTimer == null) {
            sTimer = new () { Interval = TimeSpan.FromMilliseconds (40), IsEnabled = true };
            sTimer.Tick += (s, e) => Redraw ();
         }
         // Issue one redraw to prime things off
         sTimer.Start ();
         sLastFrametime = DateTime.Now; 
         Redraw ();
      }
   }
   static DateTime sLastFrametime;
   static readonly List<Action<double>> sRenderCompletes = [];
   static DispatcherTimer? sTimer;

   /// <summary>This detaches a callback from the continous-render loop</summary>
   /// This is the opposite of StartContinuousRender above. Once all the callbacks have
   /// retired, we stop the loop. 
   public static void StopContinuousRender (Action<double> renderComplete) {
      sRenderCompletes.Remove (renderComplete);
      if (sRenderCompletes.Count == 0 && sTimer != null) 
         sTimer.Stop ();
   }

   /// <summary>This is called at the start of every frame to reset to known</summary>
   public static void StartFrame (Vec2S viewport) {
      mViewport = viewport;
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      mColors.Clear (); mColor = Color4.White;
      mLineWidths.Clear (); mLineWidth = 4f;
      mPointSizes.Clear (); mPointSize = 7f;
      mLineTypes.Clear (); mLineType = ELineType.Continuous;
      mLTScales.Clear (); mLTScale = 100f;
      mTypefaces.Clear (); mTypeface = null;
      mIDXfms.Clear (); mIDXfm = 0;
      mChanged = ELuxAttr.None;
      Rung++;
   }

    public class Stats {
      /// <summary>The current frame number</summary>
      public int NFrame => mFrame;
      /// <summary>How many times is a program change happening, per frame</summary>
      public int PgmChanges => GLState.mPgmChanges;
      /// <summary>How many times is a new VAO bound, per frame</summary>
      public int VAOChanges => GLState.mVAOChanges;
      /// <summary>How many times are we applying new uniforms per frame</summary>
      public int ApplyUniforms => Shader.mApplyUniforms;
      /// <summary>How many draw calls per frame</summary>
      public int DrawCalls => RBatch.mDrawCalls;
      /// <summary>Number of vertices drawn</summary>
      public int VertsDrawn => RBatch.mVertsDrawn;
   }
   static Stats sStats = new ();

   public class TInfo : IObservable<Stats> {
      public IDisposable Subscribe (IObserver<Stats> observer) => (mSubject ??= new ()).Subscribe (observer);

      public void FrameOver () => mSubject?.OnNext (sStats);
      Subject<Stats>? mSubject;
   }
   static public TInfo Info = new ();

   public class TFPS : IObservable<int> {
      public IDisposable Subscribe (IObserver<int> observer) => (mSubject ??= new ()).Subscribe (observer);

      public void FrameOver () {
         sFrames++;
         double elapsed = (DateTime.Now - sLastTime).TotalSeconds;
         if (elapsed >= 1) {
            int fps = (int)(sFrames / elapsed + 0.5);
            mSubject?.OnNext (fps);
            (sLastTime, sFrames) = (DateTime.Now, 0);
         }
      }
      Subject<int>? mSubject;

      static DateTime sLastTime;
      static int sFrames;
   }
   public static TFPS FPS = new ();
}
#endregion
