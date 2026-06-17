// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLFuncs.cs
// ║║║║╬║╔╣║ GL class - DllImport and dynamically loaded functions for OpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Nori.WGL;
using Ptr = nint;

#region class GL -----------------------------------------------------------------------------------
/// <summary>Implements the P-Invoke connections to OpenGL</summary>
static public unsafe class GL {
   // Interface ----------------------------------------------------------------
   // Creates an OpenGL context in Windows .....................................
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
