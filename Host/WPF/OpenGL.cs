// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OpenGL.cs
// ║║║║╬║╔╣║ WPFOpenGL is a WPF-specific implementation of the IOpenGL interface
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.InteropServices;
using System.Windows;
using Ptr = nint;
namespace Nori;

/// <summary>Implementation of the IOpenGL interface for WPF</summary>
/// OpenGL itself is a huge interface with hundreds of functions. However, there are only a few
/// 'differences' between various OpenGL implementations and that is all that is captured in the
/// IOpenGL interface. The bulk of the renderer code works by first using GetGLProcAddress to get
/// pointers to the dozens of OpenGL function it needs (like "glCompileShader" or "glDrawArrays"),
/// and then using those pointers. So all those functions don't need to be exposed in this
/// interface directly, but are obtained indirectly using the GetGLProcAddress. 
class WPFOpenGL : IOpenGL {
   // Interface ----------------------------------------------------------------
   // When WPFOpenGL is initialized, load the "opengl32.dll" library, we will later
   // use this to fetch pointers from it
   public WPFOpenGL () => GLLib = NativeLibrary.Load ("opengl32.dll");
   readonly Ptr GLLib;

   // For old OpenGL functions (like glBegin, glEnable, glFlush etc), we use the 
   // NativeLibrary.GetExport("opengl32.dll", name) method - this just directly gets the
   // exported function from the DLL and returns it. However, a number of more modern functions
   // (like "glCompileShader", "glDrawArrays") are not exported directly from Opengl32.dll
   // but are obtained indirectly by calling wglGetProcAddress with the function name. 
   // To make this GetGLProcAddress universal, we try WGLGetProcAddress first, and if that
   // fails, we fall back to NativeLibrary.GetExport.
   public Ptr GetGLProcAddress (string name) {
      Ptr proc = WGLGetProcAddress (name);
      if (proc == 0)
         try { proc = NativeLibrary.GetExport (GLLib, name); } catch { }
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc;
   }

   // Pass on the OnPaint handler to WPFHost, from where WM_PAINT handler of the
   // OpenGL surface will use it to do the actual drawing. This is set up by Lux to point to 
   // code that draws the current UIScene
   public Action<int, int> OnPaint { set => WPFHost.OnPaint = value; }

   // Request a redraw by doing an old-fashioned Invalidate of the UserControl that hosts
   // the GL context
   public void Redraw () => WPFHost.GLPanel?.Redraw ();

   /// <summary>Handle cursorvisible by setting it on the panel</summary>
   public bool CursorVisible { set => Panel.CursorVisible = value; }

   // The DPI scaling can be obtained from the WPF main window using its TransformToDevice.
   // If no WPF main window exists, or if we are running tests, we just return 1. 
   public float DPIScale {
      get {
         if (mDPIScale == 0) {
            if (Lib.Testing) mDPIScale = 1;
            else if (PresentationSource.FromVisual (WPFHost.Main) is { } source) {
               var xfm = source.CompositionTarget.TransformToDevice;
               mDPIScale = (float)(xfm.M11 + xfm.M22) / 2;
            } else return 1;
         }
         return mDPIScale;
      }
   }
   float mDPIScale = 0;

   // Implementation -----------------------------------------------------------
   const string OPENGL32 = "opengl32.dll";
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] public static extern Ptr WGLGetProcAddress (string name);
}
