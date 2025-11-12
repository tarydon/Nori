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
   // Properties ---------------------------------------------------------------
   /// <summary>If set, back faces are colored pink (useful for debugging) when using the Phong shader</summary>
   public static bool BackFacesPink;

   /// <summary>Subscribe to this to get a FPS (frames-per-second) report each second</summary>
   public static IObservable<int> FPS => mFPS;
   static Subject<int> mFPS = new ();

   /// <summary>Subscribe to this to get statistics after each frame is rendered</summary>
   public static IObservable<Stats> Info => mInfo;
   static Subject<Stats> mInfo = new ();

   /// <summary>If set, we are redering a frame for 'picking'</summary>
   public static bool IsPicking => mIsPicking;
   static bool mIsPicking;

   /// <summary>Subscribe to this to know when Lux is ready (event raised only once)</summary>
   public static IObservable<int> OnReady => mOnReady;
   internal static Subject<int> mOnReady = new ();

   /// Sets whether the cursor is visible or not when it is over the panel
   /// </summary>
   /// If this is set to false, then the current scene must 'paint' a cursor that follows
   /// the mouse movement
   public static bool CursorVisible { set => Panel.CursorVisible = value; }

   /// <summary>Read this property to know if Lux is ready (panel created, GL surface initialized)</summary>
   public static bool Ready => mReady;
   internal static bool mReady;

   /// <summary>The current scene that is bound to the visible viewport</summary>
   public static Scene? UIScene {
      get => mUIScene;
      set {
         mUIScene?.Detach ();
         BackFacesPink = false;
         mUIScene = value; mViewBound.OnNext (0); Redraw ();
         Panel.CursorVisible = mUIScene?.CursorVisible ?? true;
      }
   }
   static Scene? mUIScene;

   /// <summary>How many world units does one pixel correspond to (for the current scene)</summary>
   public static double PixelScale {
      get {
         if (mUIScene == null || mViewport.X == 0) return 1;
         var xfm = mUIScene.Xfms[0].InvXfm;
         double dx = 2.0 / mViewport.X;   //
         Point3 pa = Point3.Zero * xfm, pb = new Point3 (dx, 0, 0) * xfm;
         return pa.DistTo (pb);
      }
   }

   /// <summary>Subscribe to this to know when the 'View-Bound' changes (view is zoomed, panned or rotated)</summary>
   static public IObservable<int> ViewBound => mViewBound;
   internal static Subject<int> mViewBound = new ();

   /// <summary>The viewport size (in pixels) of the Lux rendering panel</summary>
   public static Vec2S Viewport => mViewport;
   static Vec2S mViewport;

   // Methods ------------------------------------------------------------------
   /// <summary>Creates the Lux rendering panel</summary>
   public static UIElement CreatePanel (bool createHost = false) {
      if (createHost) {
         // If this is specified, we must also create a floating 'host window' to house
         // the LuxPanel. This is used when Flux is running in some console mode, but still
         // has to produce OpenGL images (for NC code, thumbnails etc)
         if (sHost == null) {
            sHost = new Window {
               Title = "Snapshot", ShowInTaskbar = false, ShowActivated = false,
               WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
               Width = 500, Height = 500, Top = 0, Left = -5000
            };
            sHost.Show ();
            sHost.Content = Panel.It;
            while (!mReady) sHost.Dispatcher.Invoke (DispatcherPriority.Background, () => { });
         }
      }
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
      mFPSReportTS = mLastFrameTS = DateTime.Now;
      return Panel.It;
   }
   static Window? sHost;

   /// <summary>Called when entities are redrawn, or when the transform changes</summary>
   /// At these times, the pick buffer must be flushed so we don't pick on a stale
   /// pick buffer
   public static void FlushPickBuffer () => mPickBufferValid = false;
   static bool mPickBufferValid;

   /// <summary>Render a Scene to an image (for example, to generate a thumbnail)</summary>
   /// The 'keepAlive' parameter controls whether the scene you pass in is disposed of
   /// after rendering the image, or continues to remain connected to the Lux engine
   /// for continued rendering. For example, if you are rendering the UIScene to a
   /// thumbnail, you will keep it alive. In most other case, you will ask for the scene
   /// to be 'detached' after use.
   public static DIBitmap RenderToImage (Scene scene, Vec2S size, DIBitmap.EFormat fmt, bool keepAlive = false) {
      if (size.X % 4 != 0) throw new ArgumentException ($"Lux.RenderToImage: image width must be a multiple of 4");
      var dib =  (DIBitmap)Render (scene, size, ETarget.Image, fmt)!;
      if (!keepAlive) scene.Detach ();
      return dib;
   }

   /// <summary>This does a 'pick' operation on the current UIScene</summary>
   /// This effectively returns the VNode that lies underneat the current mouse position.
   public static VNode? Pick (Vec2S pos) {
      // If we're doign any simulation, return null
      if (sRenderCompletes.Count > 0 || mRendering || !mReady || mUIScene == null) return null;
      if (!mPickBufferValid) {
         mPickBufferValid = true;
         var tup = ((byte[], float[]))Render (mUIScene, mViewport, ETarget.Pick, DIBitmap.EFormat.Unknown)!;
         mPickPixel = tup.Item1; mPickDepth = tup.Item2;
      }
      int index = (mViewport.Y - pos.Y - 1) * mViewport.X + pos.X;
      if (index < 0 || index >= mPickDepth.Length) return null;

      // Now, abandon the LSB 2 bits of r, g and b leaving only 6 bits each (this is to
      // avoid round off errors in low-bit depth color buffers
      index *= 4;
      int b = mPickPixel[index] >> 2, g = mPickPixel[index + 1] >> 2, r = mPickPixel[index + 2] >> 2;
      int vnodeId = r + (g << 6) + (b << 12);
      return VNode.SafeGet (vnodeId);
   }
   static byte[] mPickPixel = [];
   static float[] mPickDepth = [];

   /// <summary>Converts a pixel coordinate to world coordinates</summary>
   public static Point3 PixelToWorld (Vec2S pix) {
      if (mUIScene == null) return new (pix.X, pix.Y, 0);
      // Convert pixel coordinate to OpenGL clip space coordinates.
      Vec2S vp = mViewport;
      Point3 clip = new (2.0 * pix.X / vp.X - 1, 1.0 - 2.0 * pix.Y / vp.Y, 0);
      clip *= mUIScene.Xfms[0].InvXfm;
      int d = Lux.PixelScale switch { > 1 => 0, > 0.1 => 1, > 0.01 => 2, > 0.001 => 3, _ => 4 };
      clip = new (Math.Round (clip.X, d), Math.Round (clip.Y, d), Math.Round (clip.Z, d));
      return clip;
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   internal static object? Render (Scene? scene, Vec2S viewport, ETarget target, DIBitmap.EFormat fmt) {
      mcFrames++; mcFPSFrames++;
      var panel = Panel.It;
      mIsPicking = target == ETarget.Pick;
      mRendering = true;
      panel.BeginRender (viewport, target);
      StartFrame (viewport);
      Color4 bgrdColor = mIsPicking ? Color4.White : (scene?.BgrdColor ?? Color4.Gray (96));
      GLState.StartFrame (viewport, bgrdColor);
      RBatch.StartFrame ();
      Shader.StartFrame ();
      scene?.Render (viewport);
      object? obj = panel.EndRender (target, fmt);

      // Various post-processing after frame render
      // Issue stats, and keep 'continuous render' loop going
      mInfo.OnNext (sStats);
      var frameTS = DateTime.Now;
      mLastFrameTime = (DateTime.Now - frameTS).TotalSeconds;
      if (sRenderCompletes.Count > 0 && target == ETarget.Screen) {
         Lib.Post (NextFrame);
         double elapsed = (frameTS - mFPSReportTS).TotalSeconds;
         if (elapsed >= 1.0) {
            // Every 1 second, issue an FPS (frames-per-second) report
            int fps = (int)(mcFPSFrames / elapsed + 0.5);
            mFPS?.OnNext (fps);
            (mcFPSFrames, mFPSReportTS) = (0, frameTS);
         }
      }
      mLastFrameTS = frameTS;
      mRendering = mIsPicking = false;
      return obj;

      // Helpers ...........................................
      static void NextFrame () {
         for (int i = sRenderCompletes.Count - 1; i >= 0; i--)
            sRenderCompletes[i] (mLastFrameTime);
         Redraw ();
      }
   }
   static int mcFrames;             // Frames rendered totally
   static double mLastFrameTime;    // How many seconds did the last frame take to render
   static DateTime mLastFrameTS;    // What was the timestamp at which we rendered the last frame
   static DateTime mFPSReportTS;    // When did we last issue an FPS report
   static int mcFPSFrames;          // Frames rendered since that time
   static bool mRendering;          // Currently rendering a frame

   /// <summary>Prompts the Lux system to redraw the screen (asynchronous)</summary>
   public static void Redraw () => Panel.mIt?.Redraw ();

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

   // Internal properties ------------------------------------------------------
   /// <summary>Bumped up whenever any Lux draw property is changed (used for shader optimizations)</summary>
   internal static int Rung;

   /// <summary>The scene that is currently being rendered (set only during a Render() call)</summary>
   internal static Scene? Scene;

   // Internal methods ---------------------------------------------------------
   /// <summary>Called when we start rendering a VNode (and it's subtree)</summary>
   /// The corresponding EndNode is called after the entire subtree under
   /// this VNode is completed rendering. Because of this, there could be multiple
   /// open 'BeginNode' calls whose EndNode is pending
   internal static void BeginNode (VNode node) {
      mNodeStack.Push ((mVNode, mChanged));
      (mVNode, mChanged) = (node, ELuxAttr.None);
   }
   static Stack<(VNode?, ELuxAttr)> mNodeStack = [];

   /// <summary>Called when a node is finished drawing</summary>
   internal static void EndNode () {
      if (PopAttr (mChanged)) Rung++;
      (mVNode, mChanged) = mNodeStack.Pop ();
   }

   /// <summary>Used internally to reset some set of attributes to the previous values</summary>
   /// This is called after a node (and it's subtree) are drawn, so that we can reset
   /// all attributes like Color, LineType etc to their previous values.
   internal static bool PopAttr (ELuxAttr flags) {
      flags &= mChanged;
      if (flags != ELuxAttr.None) {
         if ((flags & ELuxAttr.Color) != 0) mColor = mColors.Pop ();
         if ((flags & ELuxAttr.LineType) != 0) mLineType = mLineTypes.Pop ();
         if ((flags & ELuxAttr.LineWidth) != 0) mLineWidth = mLineWidths.Pop ();
         if ((flags & ELuxAttr.LTScale) != 0) mLTScale = mLTScales.Pop ();
         if ((flags & ELuxAttr.PointSize) != 0) mPointSize = mPointSizes.Pop ();
         if ((flags & ELuxAttr.TypeFace) != 0) mTypeface = mTypefaces.Pop ();
         if ((flags & ELuxAttr.Xfm) != 0) mIDXfm = mIDXfms.Pop ();
         if ((flags & ELuxAttr.ZLevel) != 0) mZLevel = mZLevels.Pop ();
         mChanged &= ~flags;
         return true;
      }
      return false;
   }

   // Implementation -----------------------------------------------------------
   static bool Get (ELuxAttr flags, ELuxAttr bit) => (flags & bit) != 0;
   static bool Set (ELuxAttr attr) {
      if ((mChanged & attr) != 0) return false;
      mChanged |= attr; return true;
   }
   static ELuxAttr mChanged;

   /// <summary>This is called at the start of every frame to reset to known</summary>
   static void StartFrame (Vec2S viewport) {
      mcFillPaths = 0;
      mViewport = viewport;
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      mColors.Clear (); mColor = Color4.White;
      mLineWidths.Clear (); mLineWidth = 2;     // Multiplied by DPIScale before it is used
      mPointSizes.Clear (); mPointSize = 4;     // Multiplied by DPIScale before it is used
      mLineTypes.Clear (); mLineType = ELineType.Continuous;
      mLTScales.Clear (); mLTScale = 30f;
      mTypefaces.Clear (); mTypeface = null;
      mIDXfms.Clear (); mIDXfm = 0;
      mZLevels.Clear (); mZLevel = 0;
      mChanged = ELuxAttr.None;
      Rung++;
   }

   // Nested types -------------------------------------------------------------
   /// <summary>Stats provides information on number of draw calls, verts drawn, pgm-changes made etc</summary>
   public class Stats {
      /// <summary>The current frame number</summary>
      public int NFrame => mcFrames;
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
}
#endregion
