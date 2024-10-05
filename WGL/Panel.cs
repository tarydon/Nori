// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Panel.cs
// ║║║║╬║╔╣║ Implements Pix.Panel (WPF UserControl) and Pix.Surface (Windows Forms Control)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Forms.Integration;
using static System.Windows.Forms.ControlStyles;
namespace Nori;

#region class Panel --------------------------------------------------------------------------------
/// <summary>A WPF UserControl used that houses an OpenGL rendering surface (used to display all GL content)</summary>
class Panel : System.Windows.Controls.UserControl {
   // Interface ----------------------------------------------------------------
   // Called from Pix when we need to start rendering a frame.
   // Note that a viewport size is passed in, since when we are rendering to an Image, we 
   // could be rendering a size that is different from the panel size. What we do here depends
   // on the render target, but in all cases, we have to make our display surface the 'current'
   // GL context
   public void BeginRender ((int X, int Y) viewport, ETarget _)
      => mSurface?.BeginRender (viewport);

   // Called when the scene is finished rendering, and we need to 'show' it.
   // What this returns depends on the render target 
   public object? EndRender () {
      mSurface?.EndRender ();
      return null;
   }

   // The Panel singleton (only one GL context, so only one Panel, one Surface)
   public static Panel It => mIt ??= new ();
   static Panel? mIt;

   // Force-issue a WM_PAINT message (redraw)
   public void Redraw ()
      => mSurface?.Invalidate ();

   /// <summary>Size of the rendering panel, in pixels (needed to set up GL correctly)</summary>
   public (int X, int Y) Size {
      get {
         if (mSurface == null) return (64, 64);
         return (mSurface.Width, mSurface.Height);
      }
   }
   Surface? mSurface;

   // Implementation -----------------------------------------------------------
   // Construct a PixPanel (private, since this is a singleton)
   Panel () { Loaded += OnLoaded; Unloaded += OnUnloaded; }

   // Called when the panel is plugged into the display stack, we create
   // the PixSurface here at this late stage only since it needs a HDC to work
   void OnLoaded (object _, RoutedEventArgs __)
      => Content = new WindowsFormsHost { Child = mSurface = new (), Focusable = false };

   // When this panel is unloaded, we dispose the surface and the WindowsFormsHost
   // container that contains it
   void OnUnloaded (object _, RoutedEventArgs __) {
      mSurface?.Dispose (); mSurface = null;
      (Content as WindowsFormsHost)?.Dispose (); Content = null;
      mIt = null;
   }
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

   public void BeginRender ((int X, int Y) viewport) {
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
         if (glrc == HGLRC.Zero) {
            GL.MakeCurrent (HDC.Zero, HGLRC.Zero);
            GL.DeleteContext (mGLRC);
            GL.MakeCurrent (mDC, mGLRC = glrc);
            break;
         }
      }
   }

   // Override OnPaint to call back to PX.Render, where our actual paint code resides
   protected override void OnPaint (PaintEventArgs e)
      => Pix.Render ();

   // Private data -------------------------------------------------------------
   HDC mDC;             // Device contex handle used for rendering
   HGLRC mGLRC;         // OpenGL context (HGLRC) used for this control
   PixelFormatDescriptor mPFD = PixelFormatDescriptor.Default;
}
#endregion
