// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Panel.cs
// ║║║║╬║╔╣║ Implements Panel (a WPF UserControl with a GL context embedded in it)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using static System.Windows.Forms.ControlStyles;
using FCursor = System.Windows.Forms.Cursor;
using WControl = System.Windows.Controls.UserControl;
using Ptr = nint;

#region class Panel --------------------------------------------------------------------------------
/// <summary>A WPF UserControl used that houses an OpenGL rendering surface (used to display all GL content)</summary>
/// A WPF UserControl does not have a windows handle, so cannot actually contain an 
/// OpenGL context directly. So we have a WinForms control as a child (using a 
/// WindowsFormsHost as intermediary) and create the GL surface on that
class Panel : WControl {
   // Interface ----------------------------------------------------------------
   // The Panel singleton (only one GL context, so only one Panel, one Surface)
   public static Panel It => mIt ??= new ();
   internal static Panel? mIt;

   // Sets the cursor to be visible or hidden
   // (this is just the implementation of the Lux.Panel.CursorVisible property)
   public static bool CursorVisible {
      set {
         if (mIt?.mSurface is not { } surface) return;
         surface.Cursor = value ? null : Surface.EmptyCursor;
      }
   }

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
      // HW.Panel = mSurface;
      var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds (0.1), IsEnabled = true };
      timer.Tick += (_, _) => { mSurface.Focus (); timer.IsEnabled = false; };
   }

   // When this panel is unloaded, we dispose the surface and the WindowsFormsHost
   // container that contains it
   void OnUnloaded (object _, RoutedEventArgs __) {
      mSurface?.Dispose (); mSurface = null;
      (Content as WindowsFormsHost)?.Dispose (); Content = null;
      mIt = null; 
      // HW.Panel = null;
   }
}
#endregion

#region class Surface ------------------------------------------------------------------------------
/// <summary>Windows.Forms control that provides the HWND and HDC needed to create an OpenGL rendering context</summary>
class Surface : UserControl {
   // Interface ----------------------------------------------------------------
   public Surface () {
      // Set up some style bits for this Surface to ensure OpenGL works correctly
      (DoubleBuffered, Name, AutoScaleMode) = (false, "PixSurface", AutoScaleMode.None);
      foreach (var style in new[] { Opaque, UserPaint, AllPaintingInWmPaint }) SetStyle (style, true);
      foreach (var style in new[] { OptimizedDoubleBuffer, Selectable }) SetStyle (style, false);
      WPFMouse.Panel = WPFKeyboard.Panel = this; 
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
      mDC = WGL.GetDC ((HWindow)Handle);
      PixelFormatDescriptor pfd = mPFD;
      int iPixelFormat = WGL.ChoosePixelFormat (mDC, ref pfd);
      WGL.SetPixelFormat (mDC, iPixelFormat, ref pfd);
      mGLRC = WGL.CreateContext (mDC);
      WGL.MakeCurrent (mDC, mGLRC);

      int[] versions = [40, 33, 32, 31, 30];
      foreach (int version in versions) {
         int major = version / 10, minor = version % 10;
         HGLRC glrc = WGL.CreateContextAttribsARB (mDC, HGLRC.Zero, major, minor, debug: false, core: false);
         if (glrc != HGLRC.Zero) {
            WGL.MakeCurrent (HDC.Zero, HGLRC.Zero);
            WGL.DeleteContext (mGLRC);
            WGL.MakeCurrent (mDC, mGLRC = glrc);
            break;
         }
      }
      WPFHost.OnReady?.Invoke ();
   }

   /// <summary>An 'empty' cursor</summary>
   internal static FCursor EmptyCursor {
      get {
         if (mEmptyCursor == null)
            using (var stm = Lib.OpenRead ("nori:Cursor/Empty.cur"))
               mEmptyCursor = new FCursor (stm);
         return mEmptyCursor;
      }
   }
   static FCursor? mEmptyCursor;

   // Override OnPaint to call back to PX.Render, where our actual paint code resides
   protected override void OnPaint (PaintEventArgs e) {
      WGL.MakeCurrent (mDC, mGLRC);
      WGL.Viewport (0, 0, Width, Height);
      WPFHost.OnPaint?.Invoke (Width, Height);
      WGL.SwapBuffers (mDC);
   }

   // Private data -------------------------------------------------------------
   HDC mDC;             // Device contex handle used for rendering
   HGLRC mGLRC;         // OpenGL context (HGLRC) used for this control
   readonly PixelFormatDescriptor mPFD = PixelFormatDescriptor.Default;
}
#endregion

#region struct PixelFormatDescriptor ---------------------------------------------------------------
// Structure used to describe an OpenGL pixel-format descriptor
[StructLayout (LayoutKind.Sequential)]
struct PixelFormatDescriptor {
   ushort Size, Version;
   uint Flags;
   byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift;
   byte AlphaBits, AlphaShift, AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits;
   byte DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
   uint LayerMask, VisibleMask, DamageMask;

   // Static used to obtain a 'default' pixel-format-descriptor
   public static PixelFormatDescriptor Default {
      get {
         const uint PFD_DRAW_TO_WINDOW = 4, PFD_SUPPORT_OPENGL = 32, PFD_DOUBLEBUFFER = 1;
         PixelFormatDescriptor pfd = new () {
            Size = 40, Version = 1,
            Flags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
            ColorBits = 32, DepthBits = 32, StencilBits = 8
         };
         if (40 != Marshal.SizeOf<PixelFormatDescriptor> ())
            throw new Exception ("Unexpected size for PixelFormatDescriptor");
         if (8 != Marshal.SizeOf<nint> ())
            throw new Exception ("Expecting 64-bit compilation");
         return pfd;
      }
   }
}
#endregion

#region class WGL ----------------------------------------------------------------------------------
/// <summary>Implements the P-Invoke connections to OpenGL</summary>
static public unsafe class WGL {
   // Interface ----------------------------------------------------------------
   // Creates an OpenGL context in Windows
   internal static HGLRC CreateContextAttribsARB (HDC dc, HGLRC share, int major, int minor, bool debug, bool core) {
      HGLRC retvalue;
      int[] pn = new int[8];
      pCreateContextAttribsARB ??= Load<wglCreateContextAttribsARB> ();
      const int MAJOR_VERSION = 0x2091, MINOR_VERSION = 0x2092, PROFILE_MASK = 0x9126, CONTEXT_FLAGS = 0x2094;
      pn[0] = MAJOR_VERSION; pn[1] = major;
      pn[2] = MINOR_VERSION; pn[3] = minor;           // Set the minor version
      pn[4] = PROFILE_MASK; pn[5] = core ? 1 : 2;     // Select either the 'core' or 'compatibility' profile
      pn[6] = CONTEXT_FLAGS; pn[7] = debug ? 1 : 0;   // Opt for a 'debug' context if needed
      fixed (int* apn = &pn[0]) { retvalue = pCreateContextAttribsARB (dc, share, apn); }
      return retvalue;
   }
   delegate HGLRC wglCreateContextAttribsARB (HDC dc, HGLRC share, int* attribs);
   static wglCreateContextAttribsARB? pCreateContextAttribsARB;

   // P-Invoke imports ---------------------------------------------------------
   [DllImport (GDI32)] internal static extern int ChoosePixelFormat (HDC hDC, [In] ref PixelFormatDescriptor pfd);
   [DllImport (GDI32)] internal static extern int SetPixelFormat (HDC hDC, int iPixelFormat, [In] ref PixelFormatDescriptor pfd);
   [DllImport (GDI32)] internal static extern int SwapBuffers (HDC hDC);

   [DllImport (USER32)] internal static extern HDC GetDC (HWindow hWnd);
   [DllImport (OPENGL32, EntryPoint = "wglDeleteContext")] internal static extern bool DeleteContext (HGLRC hglrc);
   [DllImport (OPENGL32, EntryPoint = "wglCreateContext")] internal static extern HGLRC CreateContext (HDC hdc);
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] internal static extern Ptr GetProcAddress (string name);
   [DllImport (OPENGL32, EntryPoint = "glViewport")] internal static extern void Viewport (int x, int y, int width, int height);
   [DllImport (OPENGL32, EntryPoint = "wglMakeCurrent")] internal static extern int MakeCurrent (HDC hdc, HGLRC hrc);

   const string GDI32 = "gdi32.dll";
   const string OPENGL32 = "opengl32.dll";
   const string USER32 = "user32.dll";

   // Implementation -----------------------------------------------------------
   // Loads an OpenGL entry-point (using dynamic load from the DLL) and returns a
   // raw Delegate that can be cast to the appropriate function signature
   static T Load<T> () where T : Delegate {
      Type type = typeof (T);
      Ptr proc = GetProcAddress (type.Name);
      if (proc == 0) throw new Exception ($"OpenGL function '{type.Name}' not found.");
      Delegate del = Marshal.GetDelegateForFunctionPointer (proc, type);
      return (T)del;
   }
}
#endregion

#region Low level enums ----------------------------------------------------------------------------
// Win32 windows handle
enum HWindow : ulong { Zero }
// Window GDI device context handle
enum HDC : ulong { Zero }
// OpenGL rendering-context handle
enum HGLRC : ulong { Zero }
#endregion
