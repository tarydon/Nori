// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLFuncs.cs
// ║║║║╬║╔╣║ GL class - DllImport and dynamically loaded functions for OpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
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

#region class GLU ----------------------------------------------------------------------------------
// Contains the GLU interface functions
static unsafe class GLU {
   const string GLU32 = "glu32.dll";
   // Callback types -----------------------------------------------------------
   public delegate void GLUtessBeginProc (EPrimitive type);
   public delegate void GLUtessErrorProc (int type);
   public delegate void GLUtessEdgeFlagProc (byte type);
   public delegate void GLUtessCombineProc (double* coords, void** vdata, float* d3, int* pout);
   public delegate void GLUtessEndProc ();
   public delegate void GLUtessVertexDataProc (Ptr data, Ptr data2);

   // Interface functions ------------------------------------------------------
   // Assigns GLU callback function
   public static HTesselator SetCallback<TCallback> (this HTesselator tess, TCallback cb) where TCallback : Delegate {
      tess.SetCallback (GetType (), cb);
      return tess;

      // Return the Callback type constant.
      static uint GetType () {
         var type = typeof (TCallback);
         if (type == typeof (GLUtessBeginProc)) return GLU_TESS_BEGIN;
         if (type == typeof (GLUtessEdgeFlagProc)) return GLU_TESS_EDGE_FLAG;
         if (type == typeof (GLUtessCombineProc)) return GLU_TESS_COMBINE;
         if (type == typeof (GLUtessVertexDataProc)) return GLU_TESS_VERTEX_DATA;
         if (type == typeof (GLUtessErrorProc)) return GLU_TESS_ERROR;
         if (type == typeof (GLUtessEndProc)) return GLU_TESS_END;
         throw new NotImplementedException ();
      }
   }

   // Need only boundary output?
   public static HTesselator SetOnlyBoundary (this HTesselator tess, bool onlyBoundary) {
      tess.SetProperty (GLU_TESS_BOUNDARY_ONLY, onlyBoundary ? 1 : 0); return tess;
   }

   // Sets the tessellation winding rule
   public static HTesselator SetWinding (this HTesselator tess, EWindingRule winding) {
      tess.SetProperty (GLU_TESS_WINDING_RULE, (int)winding); return tess;
   }

   // PInvokes -----------------------------------------------------------------
   [DllImport (GLU32, EntryPoint = "gluTessVertex")] internal static extern void AddVertex (this HTesselator tess, double* location, Ptr data);
   [DllImport (GLU32, EntryPoint = "gluNewTess")] internal static extern HTesselator NewTess ();
   [DllImport (GLU32, EntryPoint = "gluDeleteTess")] internal static extern void Delete (this HTesselator tess);
   [DllImport (GLU32, EntryPoint = "gluTessProperty")] internal static extern void SetProperty (this HTesselator tess, uint prop, double value);
   [DllImport (GLU32, EntryPoint = "gluTessNormal")] internal static extern void SetNormal (this HTesselator tess, double x, double y, double z);
   [DllImport (GLU32, EntryPoint = "gluTessBeginContour")] internal static extern void BeginContour (this HTesselator tess);
   [DllImport (GLU32, EntryPoint = "gluTessBeginPolygon")] internal static extern void BeginPolygon (this HTesselator tess, Ptr data);
   [DllImport (GLU32, EntryPoint = "gluTessCallback")] static extern void SetCallback (this HTesselator tess, uint which, Delegate proc);
   [DllImport (GLU32, EntryPoint = "gluTessEndContour")] internal static extern void EndContour (this HTesselator tess);
   [DllImport (GLU32, EntryPoint = "gluTessEndPolygon")] internal static extern void EndPolygon (this HTesselator tess);

   // Constants ----------------------------------------------------------------
   const uint GLU_TESS_BEGIN = 100100;
   const uint GLU_TESS_END = 100102;
   const uint GLU_TESS_VERTEX_DATA = 100107;
   const uint GLU_TESS_COMBINE = 100105;
   const uint GLU_TESS_ERROR = 100103;
   const uint GLU_TESS_EDGE_FLAG = 100104;
   const uint GLU_TESS_WINDING_RULE = 100140;
   const uint GLU_TESS_BOUNDARY_ONLY = 100141;
}
#endregion
