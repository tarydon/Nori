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

   // Links all the shaders into a single program (shader-pipeline) ............
   public static void LinkProgram (HProgram program)
      => (pLinkProgram ??= Load<glLinkProgram> ()) (program);
   delegate void glLinkProgram (HProgram program);
   static glLinkProgram? pLinkProgram;

   // Map part of a buffer object to client address space ......................
   public static Ptr MapBufferRange (EBufferTarget target, int offset, int length, EMapAccess access) 
      => (pMapBufferRange ??= Load<glMapBufferRange>()) (target, offset, length, access);
   delegate Ptr glMapBufferRange (EBufferTarget target, Ptr offset, Ptr length, EMapAccess access);
   static glMapBufferRange? pMapBufferRange;

   // Set up a parameter for patch rendering (commonly the number of vertices per patch)
   public static void PatchParameter (EPatchParam pname, int value)
      => (pPatchParameteri ??= Load<glPatchParameteri> ()) (pname, value);
   delegate void glPatchParameteri (EPatchParam pname, int value);
   static glPatchParameteri? pPatchParameteri;

   // Set up the sentinel value to signal a primitive-restart ..................
   public static void PrimitiveRestartIndex (uint index) 
      => (pPrimitiveRestartIndex ??= Load<glPrimitiveRestartIndex> ()) (index);
   delegate void glPrimitiveRestartIndex (uint index);
   static glPrimitiveRestartIndex? pPrimitiveRestartIndex;

   // Allocates render buffer storage ..........................................
   public static void RenderBufferStorage (ERenderBufferFormat format, int cx, int cy) 
      => (pRenderBufferStorage ??= Load<glRenderbufferStorage> ()) (ERenderBufferTarget.RenderBuffer, format, cx, cy);
   delegate void glRenderbufferStorage (ERenderBufferTarget target, ERenderBufferFormat format, int cx, int cy);
   static glRenderbufferStorage? pRenderBufferStorage;

   // Set up the source code for a shader ......................................
   public static void ShaderSource (HShader shader, string source)
      => (pShaderSource ??= Load<glShaderSource> ()) (shader, 1, [source], [source.Length]);
   delegate void glShaderSource (HShader shader, int count, string[] source, int[] length);
   static glShaderSource? pShaderSource;

   // Set up the stencil function for testing ..................................
   public static void StencilFunc (EFace face, EStencilFunc func, int value, uint mask)
      => (pStencilFunc ??= Load<glStencilFuncSeparate> ()) (face, func, value, mask);
   delegate void glStencilFuncSeparate (EFace face, EStencilFunc func, int value, uint mask);
   static glStencilFuncSeparate? pStencilFunc;

   // Set up the stencil op for front or back face .............................
   public static void StencilOp (EFace face, EStencilOp sfail, EStencilOp dpfail, EStencilOp dppass)
      => (pStencilOp ??= Load<glStencilOpSeparate> ()) (face, sfail, dpfail, dppass);
   delegate void glStencilOpSeparate (EFace face, EStencilOp sfail, EStencilOp dpfail, EStencilOp dppass);
   static glStencilOpSeparate? pStencilOp;

   // Specify the value of a uniform variable ..................................
   public static void Uniform (int location, float f0)
      => (pUniform1f ??= Load<glUniform1f> ()) (location, f0);
   delegate void glUniform1f (int location, float v0);
   static glUniform1f? pUniform1f;

   public static void Uniform (int location, float f0, float f1)
      => (pUniform2f ??= Load<glUniform2f> ()) (location, f0, f1);
   delegate void glUniform2f (int location, float v0, float v1);
   static glUniform2f? pUniform2f;

   public static void Uniform (int location, float f0, float f1, float f2, float f3)
      => (pUniform4f ??= Load<glUniform4f> ()) (location, f0, f1, f2, f3);
   delegate void glUniform4f (int location, float v0, float v1, float v2, float v3);
   static glUniform4f? pUniform4f;

   public static void Uniform (int location, bool transpose, float* value)
      => (pUniformMatrix4fv ??= Load<glUniformMatrix4fv> ()) (location, 1, transpose, value);
   delegate void glUniformMatrix4fv (int location, int count, bool transpose, float* value);
   static glUniformMatrix4fv? pUniformMatrix4fv;

   // Loads a uniform defined as an int (like a texture ID) ....................
   public static void Uniform1i (int location, int n)
      => (pUniform1i ??= Load<glUniform1i> ()) (location, n);
   delegate void glUniform1i (int location, int val);
   static glUniform1i? pUniform1i;

   // Release the mapping of a buffer object's data store ......................
   public static void UnmapBuffer (EBufferTarget target) 
      => (pUnmapBuffer ??= Load<glUnmapBuffer> ()) (target);
   delegate bool glUnmapBuffer (EBufferTarget target);
   static glUnmapBuffer? pUnmapBuffer;

   // This sets the program object to use for rendering ........................
   public static void UseProgram (HProgram program)
      => (pUseProgram ??= Load<glUseProgram> ()) (program);
   delegate void glUseProgram (HProgram program);
   static glUseProgram? pUseProgram;

   // Defines an element in a Vertex specification (integral type) .............
   public static void VertexAttribIPointer (int index, int size, EDataType type, int stride, int offset) 
      => (pVertexAttribIPointer ??= Load<glVertexAttribIPointer> ()) (index, size, type, stride, offset);
   delegate void glVertexAttribIPointer (int index, int size, EDataType type, int stride, Ptr pointer);
   static glVertexAttribIPointer? pVertexAttribIPointer;

   // Defines an element in a Vertex specification (float type) ................
   public static void VertexAttribPointer (int index, int size, EDataType type, bool normalized, int stride, int offset) 
      => (pVertexAttribPointer ??= Load<glVertexAttribPointer> ()) (index, size, type, normalized, stride, offset);
   delegate void glVertexAttribPointer (int index, int size, EDataType type, bool normalized, int stride, Ptr pointer);
   static glVertexAttribPointer? pVertexAttribPointer;

   // Specify an attribute as 'per-instance' rather than 'per-vertex' ..........
   public static void VertexAttribDivisor (int index, int divisor)
      => (pVertexAttribDivisor ??= Load<glVertexAttribDivisor> ()) (index, divisor);
   delegate void glVertexAttribDivisor (int index, int divisor);
   static glVertexAttribDivisor? pVertexAttribDivisor;

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
