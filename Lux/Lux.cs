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
   /// This gets reset any time a new UIScene is set
   public static bool BackFacesPink;

   /// <summary>Sets whether the cursor is visible or not when it is over the panel</summary>
   /// If this is set to false, then the current scene must 'paint' a cursor that follows
   /// the mouse movement
   public static bool CursorVisible { set => HW.CursorVisible = value; }

   /// <summary>Subscribe to this to get a FPS (frames-per-second) report each second</summary>
   public static IObservable<int> FPS => mFPS;
   static readonly Subject<int> mFPS = new ();

   /// <summary>Subscribe to this to get statistics after each frame is rendered</summary>
   public static IObservable<Stats> Info => mInfo;
   static readonly Subject<Stats> mInfo = new ();

   /// <summary>If set, we are redering a frame for 'picking'</summary>
   public static bool IsPicking => mIsPicking;
   static bool mIsPicking;

   /// <summary>Subscribe to this to know when Lux is ready (event raised only once)</summary>
   public static IObservable<int> OnReady => mOnReady;
   internal static Subject<int> mOnReady = new ();

   /// <summary>The panel size of the Lux rendering panel</summary>
   public static Vec2S PanelSize => mPanelSize;
   static Vec2S mPanelSize;

   /// <summary>This is set after a Pick operation, and returns the 3D pick position</summary>
   public static Point3 PickPos => mPickPos;
   static Point3 mPickPos;

   /// <summary>How many world units does one pixel correspond to (for the current scene)</summary>
   [Obsolete ("Use Scene.PixelScale")]
   public static double PixelScale {
      get {
         var scene = UIScene;
         if (scene == null || mPanelSize.X == 0) return 1;
         return scene.PixelScale;
      }
   }

   /// <summary>Returns true if Lux is ready to use</summary>
   public static bool Ready => mReady;
   static bool mReady;

   /// <summary>Enumerates all the sub-scenes (use Scene.Rect to get the pixel-area it uses)</summary>
   public static IEnumerable<Scene> SubScenes => mScenes.Select (a => a.Scene).Skip (1);
   static readonly List<(Scene Scene, Bound2 Bound)> mScenes = [];

   /// <summary>The current scene that is bound to the visible viewport</summary>
   public static Scene? UIScene {
      get => mScenes.Count > 0 ? mScenes[0].Scene : null;
      set {
         BackFacesPink = false;
         mScenes.ForEach (a => a.Scene.Detach ());
         mScenes.Clear ();
         if (value != null) {
            value.Attach ();
            mViewBound.OnNext (0);
            HW.CursorVisible = value.CursorVisible;
            value.Rect = new (0, 0, mPanelSize.X, mPanelSize.Y);
            mScenes.Add ((value, new (0, 0, 1, 1)));
         } else
            HW.CursorVisible = true;
         Redraw ();
      }
   }

   /// <summary>Subscribe to this to know when the 'View-Bound' changes (view is zoomed, panned or rotated)</summary>
   public static IObservable<int> ViewBound => mViewBound;
   internal static Subject<int> mViewBound = new ();

   [Obsolete ("Use Lux.PanelSize instead")]
   public static Vec2S Viewport => PanelSize;

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a sub-scene to the current render set</summary>
   /// <param name="scene">The scene to add</param>
   /// <param name="bound">The bound occupied by the scene, in normalized coordinates where
   /// (0,0) is the top left corner, and (1,1) the bottom right.</param>
   /// Note that you cannot add the same scene multiple times (nor can you add the UIScene
   /// again as a SubScene). If you want to display the same content in multiple viewports,
   /// (for example, with different view-points), create multiple scenes that all share the 
   /// same Root VNode. 
   /// Mounting a new UIScene will remove all the sub-scenes. 
   public static void AddSubScene (Scene scene, Bound2 bound) {
      Lib.Check (mScenes.None (a => a.Scene == scene), "Duplicate scene");
      scene.Attach ();
      mScenes.Add ((scene, bound));
      Redraw ();
   }

   /// <summary>Creates the Lux rendering panel</summary>
   public static object CreatePanel (bool createHost = false) {
      return WinGL.Create (OnReady, OnPaint, createHost);

      static void OnReady () { mReady = true; mOnReady.OnNext (0); }
      static void OnPaint (int x, int y) => Render (UIScene, new Vec2S (x, y), ETarget.Screen, DIBitmap.EFormat.Unknown);
   }

   public static void DumpStats () {
      Debug.Print ("Buffers:");
      foreach (var buf in RetainBuffer.All.GetSnapshot ()) Debug.Print (buf.ToString ());
   }

   /// <summary>Called when entities are redrawn, or when the transform changes</summary>
   /// At these times, the pick buffer must be flushed so we don't pick on a stale
   /// pick buffer
   public static void FlushPickBuffer () => mPickBufferValid = false;
   static bool mPickBufferValid;

   /// <summary>This does a 'pick' operation on the current UIScene</summary>
   /// This effectively returns the VNode that lies underneat the current mouse position.
   public static VNode? Pick (Vec2S pos) {
      // If we're doign any simulation, return null
      if (!(mReady || Lib.Testing) || mRendering) return null;
      var scene = PickScene (pos);
      if (scene == null || sRenderCompletes.Any (a => a.Scene == scene)) return null;
      var viewport = scene.Rect.Size;
      if (!mPickBufferValid) {
         mPickBufferValid = true;
         var tup = ((byte[], float[]))Render (scene, viewport, ETarget.Pick, DIBitmap.EFormat.Unknown)!;
         mPickPixel = tup.Item1; mPickDepth = tup.Item2;
      }

      Vec2S local = new (pos.X - scene.Rect.Left, pos.Y - scene.Rect.Top);
      int index = (viewport.Y - local.Y - 1) * viewport.X + local.X;
      if (index < 0 || index >= mPickDepth.Length) return null;
      float fDepth = mPickDepth[index];

      // Now, abandon the LSB 2 bits of r, g and b leaving only 6 bits each (this is to
      // avoid round off errors in low-bit depth color buffers
      index *= 4;
      int b = mPickPixel[index] >> 2, g = mPickPixel[index + 1] >> 2, r = mPickPixel[index + 2] >> 2;
      int vnodeId = r + (g << 6) + (b << 12);
      VNode? node = VNode.SafeGet (vnodeId);
      if (node != null) mPickPos = scene.Unproject (pos, fDepth);
      return node;
   }

   /// <summary>Picks the scene that lies at the given pixel coordinates</summary>
   /// The pixel coordinates start at (0,0) at the top left of the screen and have an
   /// extent of Lux.PanelSize. If there are multiple scenes overlapping at the given
   /// pixel position, the last one is returned (last one added by AddSubScene). 
   public static Scene? PickScene (Vec2S pix) {
      for (int i = mScenes.Count - 1; i >= 1; i--) {
         var scene = mScenes[i].Scene;
         if (scene.Rect.Contains (pix)) return scene;
      }
      return UIScene;
   }

   /// <summary>Converts a pixel coordinate to world coordinates</summary>
   [Obsolete ("Use Scene.PixelToWorld instead")]
   public static Point3 PixelToWorld (Vec2S pix) {
      if (UIScene is not Scene scene) return new (pix.X, pix.Y, 0);
      return scene.PixelToWorld (pix);
   }

   /// <summary>Render a Scene to an image (for example, to generate a thumbnail)</summary>
   [Obsolete ("Use Scene.RenderToImage")]
   public static DIBitmap RenderToImage (Scene scene, Vec2S size, DIBitmap.EFormat fmt) {
      if (size.X % 4 != 0) throw new ArgumentException ("Lux.RenderToImage: image width must be a multiple of 4");
      bool unAttached = mScenes.None (a => a.Scene == scene);
      if (unAttached) scene.Attach ();
      var dib = (DIBitmap)Render (scene, size, ETarget.Image, fmt)!;
      if (unAttached) scene.Detach ();
      return dib;
   }

   /// <summary>Removes a SubScene from the list of scenes</summary>
   /// Note that mounting a new UIScene will automatically remove _all_ subscenes
   public static void RemoveSubScene (Scene scene) {
      for (int i = mScenes.Count - 1; i > 0; i--)
         if (mScenes[i].Scene == scene) {
            scene.Detach (); mScenes.RemoveAt (i);
            Redraw (); 
         }
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   internal static object? Render (Scene? scene, Vec2S viewport, ETarget target, DIBitmap.EFormat fmt) {
      mcFrames++; mcFPSFrames++;
      mIsPicking = target == ETarget.Pick;
      if (mRendering) throw new InvalidOperationException ();
      mRendering = true;

      var vp = new RectS (0, 0, viewport.X, viewport.Y);
      BeginRender (viewport, target);
      mPanelSize = viewport;  // Set only when rendering the root scene
      StartFrame (viewport);
      Color4 bgrdColor = mIsPicking ? Color4.White : (scene?.BgrdColor ?? Color4.Gray (96));
      GLState.StartFrame (Vec2S.Zero, viewport, bgrdColor);
      RBatch.StartFrame ();
      Shader.StartFrame ();
      if (scene != null) {
         int yMax = mPanelSize.Y - 1;
         if (target == ETarget.Screen) scene.Rect = vp;
         scene.Render (viewport);
         if (target == ETarget.Screen) {
            for (int i = 1; i < mScenes.Count; i++) {
               var (scene2, bound2) = mScenes[i];
               var (cx, cy, DX, DY) = (mPanelSize.X, mPanelSize.Y, bound2.X, bound2.Y);
               int x0 = (int)(DX.Min * cx + 0.5), x1 = (int)(DX.Max * cx + 0.5);
               int y0 = (int)(DY.Min * cy + 0.5), y1 = (int)(DY.Max * cy + 0.5);
               var rect = new RectS (x0, y0, x1, y1);
               if (target == ETarget.Screen)
                  scene2.Rect = new (x0, yMax - y1, x1, yMax - y0);
               var vport = rect.Size;
               BeginRender (vport, target);  // Don't worry about viewport - it
               StartFrame (vport);
               GLState.StartFrame (new Vec2S (rect.Left, rect.Top), vport, scene2.BgrdColor);
               RBatch.StartFrame ();
               Shader.StartFrame ();
               scene2.Render (vport);
            }
         }
      }
      object? obj = EndRender (target, fmt);

      // Various post-processing after frame render
      // Issue stats, and keep 'continuous render' loop going
      mInfo.OnNext (sStats);
      var frameTS = DateTime.Now;
      mLastFrameTime = (DateTime.Now - sLastFrametime).TotalSeconds;
      if (sRenderCompletes.Count > 0 && target == ETarget.Screen) {
         Lib.Post (NextFrame);
         double elapsed = (frameTS - mFPSReportTS).TotalSeconds;
         if (elapsed >= 1.0) {
            // Every 1 second, issue an FPS (frames-per-second) report
            int fps = (int)(mcFPSFrames / elapsed + 0.5);
            mFPS.OnNext (fps);
            (mcFPSFrames, mFPSReportTS) = (0, frameTS);
         }
      }
      sLastFrametime = frameTS;
      mRendering = mIsPicking = false;
      return obj;

      // Helpers ...........................................
      static void NextFrame () {
         for (int i = sRenderCompletes.Count - 1; i >= 0; i--)
            sRenderCompletes[i].Tick (mLastFrameTime);
         Redraw ();
      }
   }
   static int mcFrames;             // Frames rendered totally
   static double mLastFrameTime;    // How many seconds did the last frame take to render
   static DateTime mFPSReportTS;    // When did we last issue an FPS report
   static int mcFPSFrames;          // Frames rendered since that time
   static bool mRendering;          // Currently rendering a frame

   /// <summary>Prompts the Lux system to redraw the screen (asynchronous)</summary>
   public static void Redraw () => HW.Redraw ();

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
   /// 
   /// If you are animating a 'subscene' that was activated using AddSubScene, use the variant
   /// of StartContinousRender that takes a Scene parameter. This is important since 'pick' 
   /// functionality is disabled on that scene while the animation is running. 
   public static void StartContinuousRender (Action<double> renderComplete) {
      if (UIScene is { } scene) StartContinuousRender (scene, renderComplete);
   }
   static DateTime sLastFrametime;
   static readonly List<(Scene Scene, Action<double> Tick)> sRenderCompletes = [];
   static DispatcherTimer? sTimer;

   /// <summary>A variant of StartContinuousRender used to start animation on a sub-scene</summary>
   /// The default version of StartContinuousRender assumes that the animation is happening
   /// on the UIScene (main scene). Sometimes, if you want to run an animation loop on a different
   /// subscene (or even on multiple sub-scenes), use this variant. 
   /// 
   /// During each frame of the animation the compelete screen is redrawn, of course (as is the
   /// case with all OpenGL rendering). However, all 'pick' functionality is disabled on the 
   /// scene associated with the animation until the animation is complete. So to ensure that 
   /// pick is disabled only on the target scene, use this variant. 
   public static void StartContinuousRender (Scene subScene, Action<double> renderComplete) {
      sRenderCompletes.Add ((subScene, renderComplete));
      if (sRenderCompletes.Count == 1) {
         // If this is the first render-complete function, start the backup timer running.
         // We need this backup timer because the RenderComplete event is not always dependable.
         // Normally, if we are running at 60 fps, we should hit the render-complete each 16.66 ms,
         // and the timer would never fire.
         if (sTimer == null) {
            sTimer = new () { Interval = TimeSpan.FromMilliseconds (40), IsEnabled = true };
            sTimer.Tick += (_, _) => Redraw ();
         }
         // Issue one redraw to prime things off
         sTimer.Start ();
         sLastFrametime = DateTime.Now;
         Redraw ();
      }
   }

   /// <summary>This detaches a callback from the continous-render loop</summary>
   /// This is the opposite of StartContinuousRender above. Once all the callbacks have
   /// retired, we stop the loop.
   public static void StopContinuousRender (Action<double> renderComplete) {
      sRenderCompletes.RemoveIf (a => a.Tick == renderComplete);
      if (sRenderCompletes.Count == 0 && sTimer != null)
         sTimer.Stop ();
   }

   // Internal properties ------------------------------------------------------
   /// <summary>Bumped up whenever any Lux draw property is changed (used for shader optimizations)</summary>
   internal static int Rung;

   /// <summary>The scene that is currently being rendered (set only during a Render() call)</summary>
   internal static Scene? Scene;

   // Internal methods ---------------------------------------------------------
   // Begins a render operation
   static void BeginRender (Vec2S viewport, ETarget target) {
      if (target is ETarget.Image or ETarget.Pick) {
         mFBViewport = viewport;
         if (mFrameBuffer == 0) {
            mFrameBuffer = GL.GenFrameBuffer ();
            mColorBuffer = GL.GenRenderBuffer (); mDepthBuffer = GL.GenRenderBuffer ();
         }
         GL.BindFrameBuffer (EFrameBufferTarget.DrawAndRead, mFrameBuffer);
         if (viewport.X > mFBSize.X || viewport.Y > mFBSize.Y) {
            mFBSize = viewport;
            GL.BindRenderBuffer (ERenderBufferTarget.RenderBuffer, mColorBuffer);
            GL.RenderBufferStorage (ERenderBufferFormat.RGBA8, viewport.X, viewport.Y);
            GL.BindRenderBuffer (ERenderBufferTarget.RenderBuffer, mDepthBuffer);
            GL.RenderBufferStorage (ERenderBufferFormat.Depth24Stencil8, viewport.X, viewport.Y);
            GL.FrameBufferRenderBuffer (EFrameBufferTarget.DrawAndRead, EFrameBufferAttachment.Color0, mColorBuffer);
            GL.FrameBufferRenderBuffer (EFrameBufferTarget.DrawAndRead, EFrameBufferAttachment.DepthStencil, mDepthBuffer);
            if (GL.CheckFrameBufferStatus (EFrameBufferTarget.Draw) != EFrameBufferStatus.Complete)
               throw new NotImplementedException ();
         }
      } else
         GL.BindFrameBuffer (EFrameBufferTarget.DrawAndRead, 0);
   }
   static Vec2S mFBViewport;            // Viewport size, when rendering to a frame-buffer
   static HFrameBuffer mFrameBuffer;    // Frame-buffer for image rendering
   static HRenderBuffer mColorBuffer, mDepthBuffer;    // Render buffers for the same
   static Vec2S mFBSize;                // The size of the frame-buffer
   static float[] mPickDepth = [];      // The depth buffer, obtained during a Pick render
   // This buffer contains the raw pixel-data obtained from a pick operation.
   // Since the models are drawn in 'false-color' mode during a pick operation, this buffer
   // effectively contains indices into the VModels list. Some finagling is required, such
   // as discarding the least signifcant bits of each color component etc (see the code in
   // Lux.Pick which reads and interprets these buffers)
   static byte[] mPickPixel = [];

   /// <summary>Called when we start rendering a VNode (and it's subtree)</summary>
   /// The corresponding EndNode is called after the entire subtree under
   /// this VNode is completed rendering. Because of this, there could be multiple
   /// open 'BeginNode' calls whose EndNode is pending
   internal static void BeginNode (VNode node) {
      mNodeStack.Push ((mVNode, mChanged));
      (mVNode, mChanged) = (node, ELuxAttr.None);
   }
   static readonly Stack<(VNode?, ELuxAttr)> mNodeStack = [];

   /// <summary>Called when a node is finished drawing</summary>
   internal static void EndNode () {
      if (PopAttr (mChanged)) Rung++;
      (mVNode, mChanged) = mNodeStack.Pop ();
   }

   // Ends the current render operation
   static object? EndRender (ETarget target, DIBitmap.EFormat fmt) {
      switch (target) {
         case ETarget.Image:
            GL.Finish ();
            int x = mFBViewport.X, y = mFBViewport.Y, bpp = fmt.BytesPerPixel ();
            var pxfmt = fmt switch {
               DIBitmap.EFormat.RGBA8 => EPixelFormat.RGBA,
               DIBitmap.EFormat.RGB8 => EPixelFormat.RGB,
               DIBitmap.EFormat.Gray8 => EPixelFormat.Red,
               _ => throw new BadCaseException (fmt)
            };
            GL.PixelStore (EPixelStoreParam.PackAlignment, 4);
            byte[] data = new byte[bpp * x * y];
            GL.ReadPixels (0, 0, x, y, pxfmt, EPixelType.UByte, data);
            return new DIBitmap (x, y, fmt, data);
         case ETarget.Pick:
            GL.Finish ();
            int size = (x = mFBViewport.X) * (y = mFBViewport.Y);
            if (size > mPickDepth.Length)
               (mPickPixel, mPickDepth) = (new byte[size * 4], new float[size]);
            GL.PixelStore (EPixelStoreParam.PackAlignment, 4);
            GL.ReadPixels (0, 0, x, y, EPixelFormat.BGRA, EPixelType.UByte, mPickPixel);
            GL.ReadPixels (0, 0, x, y, EPixelFormat.DepthComponent, EPixelType.Float, mPickDepth);
            return (mPickPixel, mPickDepth);
      }
      return null;
   }

   /// <summary>Used internally to reset some set of attributes to the previous values</summary>
   /// This is called after a node (and it's subtree) are drawn, so that we can reset
   /// all attributes like Color, LineType etc to their previous values.
   internal static bool PopAttr (ELuxAttr flags) {
      flags &= mChanged;
      if (flags != ELuxAttr.None) {
         if ((flags & ELuxAttr.SColor) != 0) mColor = mColors.Pop ();
         if ((flags & ELuxAttr.BorderColor) != 0) mBorderColor = mBorderColors.Pop ();
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
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      mColors.Clear (); mColor = Color4.White;
      mBorderColors.Clear (); mBorderColor = Color4.Yellow;
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
   static readonly Stats sStats = new ();
}
#endregion
