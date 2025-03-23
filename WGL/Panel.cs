// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Panel.cs
// ║║║║╬║╔╣║ Implements Lux.Panel (WPF UserControl) and Lux.Surface (Windows Forms Control)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using static System.Windows.Forms.ControlStyles;
namespace Nori;

#region class Panel --------------------------------------------------------------------------------
/// <summary>A WPF UserControl used that houses an OpenGL rendering surface (used to display all GL content)</summary>
class Panel : System.Windows.Controls.UserControl {
   // Interface ----------------------------------------------------------------
   // Called from Lux when we need to start rendering a frame.
   // Note that a viewport size is passed in, since when we are rendering to an Image, we 
   // could be rendering a size that is different from the panel size. What we do here depends
   // on the render target, but in all cases, we have to make our display surface the 'current'
   // GL context
   public void BeginRender (Vec2S viewport, ETarget target) {
      if (mSurface == null) return; 
      mSurface.BeginRender (viewport);
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

   // Called when the scene is finished rendering, and we need to 'show' it.
   // What this returns depends on the render target 
   public object? EndRender (ETarget target) {
      switch (target) {
         case ETarget.Screen:
            mSurface?.EndRender ();
            break;
         case ETarget.Image: case ETarget.Pick:
            GL.Finish ();
            int x = mFBViewport.X, y = mFBViewport.Y, stride = x * 3;
            GL.PixelStore (EPixelStoreParam.PackAlignment, 4);
            if (target == ETarget.Image) {
               byte[] data = new byte[stride * y];
               GL.ReadPixels (0, 0, x, y, EPixelFormat.RGB, EPixelType.UByte, data);
               //Figure fig = new Figure (Figure.EFmt.PixelRGBA, data, x, y, stride);
               //fig.FlipY ();
               return data;
            } else {
               throw new NotImplementedException ();
            }
      }
      return null;
   }

   // The Panel singleton (only one GL context, so only one Panel, one Surface)
   public static Panel It => mIt ??= new ();
   static Panel? mIt;

   // Force-issue a WM_PAINT message (redraw)
   public void Redraw ()
      => mSurface?.Invalidate ();

   /// <summary>Size of the rendering panel, in pixels (needed to set up GL correctly)</summary>
   public Vec2S Size {
      get {
         if (mSurface == null) return new (64, 64);
         return new (mSurface.Width, mSurface.Height);
      }
   }
   Surface? mSurface;

   // Implementation -----------------------------------------------------------
   // Construct a PixPanel (private, since this is a singleton)
   Panel () { Loaded += OnLoaded; Unloaded += OnUnloaded; }

   // Called when the panel is plugged into the display stack, we create
   // the PixSurface here at this late stage only since it needs a HDC to work
   void OnLoaded (object _, RoutedEventArgs __) {
      Content = new WindowsFormsHost { Child = mSurface = new (), Focusable = false };
      HW.Panel = mSurface;
      var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds (0.1), IsEnabled = true };
      timer.Tick += (s, e) => { mSurface.Focus (); timer.IsEnabled = false; };
   }

   // When this panel is unloaded, we dispose the surface and the WindowsFormsHost
   // container that contains it
   void OnUnloaded (object _, RoutedEventArgs __) {
      mSurface?.Dispose (); mSurface = null;
      (Content as WindowsFormsHost)?.Dispose (); Content = null;
      mIt = null; HW.Panel = null;
   }

   // Private data -------------------------------------------------------------
   Vec2S mFBViewport;            // Viewport size, when rendering to a frame-buffer
   HFrameBuffer mFrameBuffer;    // Frame-buffer for image rendering
   HRenderBuffer mColorBuffer, mDepthBuffer;    // Render buffers for the same
   Vec2S mFBSize;                // The size of the frame-buffer
}
#endregion

#region class Surface ------------------------------------------------------------------------------
/// <summary>Windows.Forms control that provides the HWND and HDC needed to create an OpenGL rendering context</summary>
class Surface : System.Windows.Forms.UserControl {
   // Interface ----------------------------------------------------------------
   public Surface () {
      // Set up some style bits for this Surface to ensure OpenGL works correctly
      (DoubleBuffered, Name, AutoScaleMode) = (false, "PixSurface", AutoScaleMode.None);
      foreach (var style in new[] { Opaque, UserPaint, AllPaintingInWmPaint }) SetStyle (style, true);
      foreach (var style in new[] { OptimizedDoubleBuffer, Selectable }) SetStyle (style, false);
   }

   public void BeginRender (Vec2S viewport) {
      GL.MakeCurrent (mDC, mGLRC);
      GL.Viewport (0, 0, viewport.X, viewport.Y);
   }

   public void EndRender () {
      GL.SwapBuffers (mDC);
   }

   // Overrides ----------------------------------------------------------------
   // Override CreateParams to specify a custom set of class-style bits to be used
   // before this control handle is created. We are particular that:
   // 1. The control is redrawn completely whenever it is resized (whole client area
   //    must be invalidated
   // 2. The control must have its own private DC (device context), no sharing of
   //    DC with other controls in this application
   protected override CreateParams CreateParams {
      get {
         var cp = base.CreateParams;
         const int CS_VREDRAW = 0x1, CS_HREDRAW = 0x2, CS_OWNDC = 0x20;
         cp.ClassStyle |= CS_HREDRAW | CS_VREDRAW | CS_OWNDC;
         return cp;
      }
   }

   // This is called when the GLPanel's HWND handle is created, and at this
   // point, we construct the HGLRC (OpenGL context handle)
   protected override void OnHandleCreated (EventArgs e) {
      base.OnHandleCreated (e);
      mDC = GL.GetDC ((HWindow)Handle);
      PixelFormatDescriptor pfd = mPFD;
      int iPixelFormat = GL.ChoosePixelFormat (mDC, ref pfd);
      GL.SetPixelFormat (mDC, iPixelFormat, ref pfd);
      mGLRC = GL.CreateContext (mDC);
      GL.MakeCurrent (mDC, mGLRC);

      int[] versions = [40, 33, 32, 31, 30];
      foreach (int version in versions) {
         int major = version / 10, minor = version % 10;
         HGLRC glrc = GL.CreateContextAttribsARB (mDC, HGLRC.Zero, major, minor, debug: false, core: false);
         if (glrc != HGLRC.Zero) {
            GL.MakeCurrent (HDC.Zero, HGLRC.Zero);
            GL.DeleteContext (mGLRC);
            GL.MakeCurrent (mDC, mGLRC = glrc);
            break;
         }
      }
   }

   // Override OnPaint to call back to PX.Render, where our actual paint code resides
   protected override void OnPaint (PaintEventArgs e)
      => Lux.Render (Lux.UIScene, new (Width, Height), ETarget.Screen);

   // Private data -------------------------------------------------------------
   HDC mDC;             // Device contex handle used for rendering
   HGLRC mGLRC;         // OpenGL context (HGLRC) used for this control
   PixelFormatDescriptor mPFD = PixelFormatDescriptor.Default;
}
#endregion
