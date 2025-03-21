﻿// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLFuncs.cs
// ║║║║╬║╔╣║ GL class - DllImport and dynamically loaded functions for OpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Ptr = nint;

#region class GL -----------------------------------------------------------------------------------
/// <summary>Implements the P-Invoke connections to OpenGL</summary>
unsafe static class GL {
   // Interface ----------------------------------------------------------------
   // Select the active texture unit ...........................................
   public static void ActiveTexture (ETexUnit unit)
      => (pActiveTexture ??= Load<glActiveTexture> ()) (unit);
   delegate void glActiveTexture (ETexUnit texture);
   static glActiveTexture? pActiveTexture;

   // Attach a shader to a shader-pipeline (program) ...........................
   public static void AttachShader (HProgram program, HShader shader)
      => (pAttachShader ??= Load<glAttachShader> ()) (program, shader);
   delegate void glAttachShader (HProgram program, HShader shader);
   static glAttachShader? pAttachShader;

   // Bind a storage buffer to a buffer target .................................
   public static void BindBuffer (EBufferTarget target, HBuffer buffer) 
      => (pBindBuffer ??= Load<glBindBuffer> ()) (target, buffer);
   delegate void glBindBuffer (EBufferTarget target, HBuffer buffer);
   static glBindBuffer? pBindBuffer;

   // Bind a vertex array object (VAO) for use .................................
   public static void BindVertexArray (HVertexArray array) 
      => (pBindVertexArray ??= Load<glBindVertexArray> ()) (array);
   delegate void glBindVertexArray (HVertexArray array);
   static glBindVertexArray? pBindVertexArray;

   // Allocates and copies data to a buffer object's storage ...................
   public static void BufferData (EBufferTarget target, int size, Ptr data, EBufferUsage usage) 
      => (pBufferData ??= Load<glBufferData> ()) (target, new Ptr (size), data, usage);
   delegate void glBufferData (EBufferTarget target, Ptr size, Ptr data, EBufferUsage usage);
   static glBufferData? pBufferData;

   // Compile an OpenGL shader .................................................
   public static void CompileShader (HShader idShader)
      => (pCompileShader ??= Load<glCompileShader> ()) (idShader);
   delegate void glCompileShader (HShader shader);
   static glCompileShader? pCompileShader;

   // Creates an OpenGL context in Windows .....................................
   public static HGLRC CreateContextAttribsARB (HDC dc, HGLRC share, int major, int minor, bool debug, bool core) {
      var retvalue = HGLRC.Zero;
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

   // Create an OpenGL program (shader pipeline) ...............................
   public static HProgram CreateProgram ()
      => (pCreateProgram ??= Load<glCreateProgram> ()) ();
   delegate HProgram glCreateProgram ();
   static glCreateProgram? pCreateProgram;

   // Create an OpenGL shader (one step of a shader pipeline) ..................
   public static HShader CreateShader (EShader type)
      => (pCreateShader ??= Load<glCreateShader> ()) (type);
   delegate HShader glCreateShader (EShader type);
   static glCreateShader? pCreateShader;

   // Delete a named buffer object .............................................
   public static unsafe void DeleteBuffer (HBuffer buffer) 
      => (pDeleteBuffers ??= Load<glDeleteBuffers> ()) (1, &buffer);
   unsafe delegate void glDeleteBuffers (int n, HBuffer* buffers);
   static glDeleteBuffers? pDeleteBuffers;

   // Delete a texture .........................................................
   public static unsafe void DeleteTexture (HTexture texture)
      => (pDeleteTextures ??= Load<glDeleteTextures> ()) (1, &texture);
   unsafe delegate void glDeleteTextures (int n, HTexture* textures);
   static glDeleteTextures? pDeleteTextures;

   // Deletes a vertex array object ............................................
   public unsafe static void DeleteVertexArray (HVertexArray array) 
      => (pDeleteVertexArrays ??= Load<glDeleteVertexArrays> ()) (1, &array);
   unsafe delegate void glDeleteVertexArrays (int n, HVertexArray* textures);
   static glDeleteVertexArrays? pDeleteVertexArrays;

   // Indexed drawing from an array (with baseVertex added to each index) ......
   public static void DrawElementsBaseVertex (EMode mode, int count, EIndexType type, Ptr indices, int baseVertex)
      => (pDrawElementsBaseVertex ??= Load<glDrawElementsBaseVertex> ()) (mode, count, type, indices, baseVertex);
   delegate void glDrawElementsBaseVertex (EMode mode, int count, EIndexType type, Ptr indices, int baseVertex);
   static glDrawElementsBaseVertex? pDrawElementsBaseVertex;

   // Specify that a particular element (specified by glVertexAttribPointer) is in use
   public static void EnableVertexAttribArray (int index) 
      => (pEnableVertexAttribArray ??= Load<glEnableVertexAttribArray> ()) ((uint)index);
   delegate void glEnableVertexAttribArray (uint index);
   static glEnableVertexAttribArray? pEnableVertexAttribArray;

   // Allocate a new data-storage buffer object ................................
   public static unsafe HBuffer GenBuffer () { 
      HBuffer buffer; 
      (pGenBuffers ??= Load<glGenBuffers> ()) (1, &buffer); 
      return buffer; 
   }
   unsafe delegate void glGenBuffers (int n, HBuffer* buffers);
   static glGenBuffers? pGenBuffers;

   // Creates a new texture ....................................................
   public static HTexture GenTexture () { HTexture tex; GenTextures (1, &tex); return tex; }

   // Allocate a new VertexArray object (VAO) ..................................
   public unsafe static HVertexArray GenVertexArray () { 
      HVertexArray array; 
      (pGenVertexArrays ??= Load<glGenVertexArrays> ()) (1, &array); 
      return array; 
   }
   unsafe delegate void glGenVertexArrays (int n, HVertexArray* arrays);
   static glGenVertexArrays? pGenVertexArrays;

   // Gets information about a program attribute ...............................
   public unsafe static void GetActiveAttrib (HProgram program, int index, out int size, out EDataType type, out string name, out int location) {
      pGetActiveAttrib ??= Load<glGetActiveAttrib> ();
      Span<byte> data = stackalloc byte[256];
      fixed (byte* p = &data[0]) {
         pGetActiveAttrib (program, index, 255, out int length, out size, out type, (Ptr)p);
         name = Encoding.UTF8.GetString (data[0..length]);
         location = GetAttribLocation (program, name);
      }
   }
   delegate void glGetActiveAttrib (HProgram program, int index, int bufsize, out int length, out int size, out EDataType type, Ptr name);
   static glGetActiveAttrib? pGetActiveAttrib;

   // <summary>Gets information about a uniform variable .......................
   public unsafe static void GetActiveUniform (HProgram program, int index, out int size, out EDataType type, out string name, out int location) {
      pGetActiveUniform ??= Load<glGetActiveUniform> ();
      Span<byte> data = stackalloc byte[256];
      fixed (byte* p = &data[0]) {
         pGetActiveUniform (program, index, 255, out int length, out size, out type, (Ptr)p);
         name = Encoding.UTF8.GetString (data[0..length]);
         location = GetUniformLocation (program, name);
      }
   }
   delegate void glGetActiveUniform (HProgram program, int index, int bufSize, out int length, out int size, out EDataType type, Ptr name);
   static glGetActiveUniform? pGetActiveUniform;

   // Gets information about an attribute's location ...........................
   public unsafe static int GetAttribLocation (HProgram program, string name) 
      => (pGetAttribLocation ??= Load<glGetAttribLocation> ()) (program, name);
   delegate int glGetAttribLocation (HProgram program, string name);
   static glGetAttribLocation? pGetAttribLocation;

   // Gets a parameter from a program object ...................................
   public static int GetProgram (HProgram program, EProgramParam pname) {
      (pGetProgramiv ??= Load<glGetProgramiv> ()) (program, pname, ints);
      return ints[0];
   }
   delegate void glGetProgramiv (HProgram program, EProgramParam pname, int[] parameters);
   static glGetProgramiv? pGetProgramiv;
   static int[] ints = [0];

   // Gets the error log for a program .........................................
   public static string GetProgramInfoLog (HProgram program) {
      pGetProgramInfoLog ??= Load<glGetProgramInfoLog> ();
      int length = GetProgram (program, EProgramParam.InfoLogLength);
      if (length == 0) return "";
      StringBuilder sb = new (length + 2);
      pGetProgramInfoLog (program, sb.Capacity, (nint)(&length), sb);
      return sb.ToString ();
   }
   delegate void glGetProgramInfoLog (HProgram program, int bufSize, nint length, StringBuilder infoLog);
   static glGetProgramInfoLog? pGetProgramInfoLog;

   // Gets some information from a shader ......................................
   public static int GetShader (HShader shader, EShaderParam pname) {
      (pGetShaderiv ??= Load<glGetShaderiv> ()) (shader, pname, ints);
      return ints[0];
   }
   delegate void glGetShaderiv (HShader shader, EShaderParam pname, int[] parameters);
   static glGetShaderiv? pGetShaderiv;

   // Gets the error log for a shader ..........................................
   public static string GetShaderInfoLog (HShader shader) {
      pGetShaderInfoLog ??= Load<glGetShaderInfoLog> ();
      int length = GetShader (shader, EShaderParam.InfoLogLength);
      if (length == 0) return "";
      StringBuilder sb = new (length + 2);
      pGetShaderInfoLog (shader, sb.Capacity, (nint)(&length), sb);
      return sb.ToString ();
   }
   delegate void glGetShaderInfoLog (HShader shader, int bufSize, nint length, StringBuilder infoLog);
   static glGetShaderInfoLog? pGetShaderInfoLog;

   // Gets the location (slot) of a uniform variable ...........................
   public static int GetUniformLocation (HProgram program, string name)
      => (pGetUniformLocation ??= Load<glGetUniformLocation> ()) (program, name);
   delegate int glGetUniformLocation (HProgram program, string name);
   static glGetUniformLocation? pGetUniformLocation;

   // Links all the shaders into a single program (shader-pipeline) ............
   public static void LinkProgram (HProgram program)
      => (pLinkProgram ??= Load<glLinkProgram> ()) (program);
   delegate void glLinkProgram (HProgram program);
   static glLinkProgram? pLinkProgram;

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

   public static unsafe void Uniform (int location, bool transpose, float* value)
      => (pUniformMatrix4fv ??= Load<glUniformMatrix4fv> ()) (location, 1, transpose, value);
   delegate void glUniformMatrix4fv (int location, int count, bool transpose, float* value);
   static glUniformMatrix4fv? pUniformMatrix4fv;

   // Loads a uniform defined as an int (like a texture ID) ....................
   public static void Uniform1i (int location, int n)
      => (pUniform1i ??= Load<glUniform1i> ()) (location, n);
   delegate void glUniform1i (int location, int val);
   static glUniform1i? pUniform1i;

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

   // P-Invoke imports ---------------------------------------------------------
   [DllImport (GDI32)] public static extern int ChoosePixelFormat (HDC hDC, [In] ref PixelFormatDescriptor pfd);
   [DllImport (GDI32)] public static extern int SetPixelFormat (HDC hDC, int iPixelFormat, [In] ref PixelFormatDescriptor pfd);
   [DllImport (GDI32)] public static extern int SwapBuffers (HDC hDC);

   [DllImport (USER32)] public static extern HDC GetDC (HWindow hWnd);

   [DllImport (OPENGL32, EntryPoint = "glBegin")] public static extern void Begin (EMode mode);
   [DllImport (OPENGL32, EntryPoint = "glBindTexture")] public static extern void BindTexture (ETexTarget target, HTexture id);
   [DllImport (OPENGL32, EntryPoint = "glBlendFunc")] static internal extern void BlendFunc (EBlendFactor sfactor, EBlendFactor dfactor);
   [DllImport (OPENGL32, EntryPoint = "glClear")] public static extern void Clear (EBuffer mask);
   [DllImport (OPENGL32, EntryPoint = "glClearColor")] public static extern void ClearColor (float red, float green, float blue, float alpha);
   [DllImport (OPENGL32, EntryPoint = "glColor3f")] public static extern void Color (float red, float green, float blue);
   [DllImport (OPENGL32, EntryPoint = "wglCreateContext")] public static extern HGLRC CreateContext (HDC hdc);
   [DllImport (OPENGL32, EntryPoint = "wglDeleteContext")] public static extern bool DeleteContext (HGLRC hglrc);
   [DllImport (OPENGL32, EntryPoint = "glDisable")] public static extern void Disable (ECap cap);
   [DllImport (OPENGL32, EntryPoint = "glDrawArrays")] public static extern void DrawArrays (EMode mode, int start, int count);
   [DllImport (OPENGL32, EntryPoint = "glEnable")] public static extern void Enable (ECap cap);
   public static void Enable (ECap cap, bool v) { if (v) Enable (cap); else Disable (cap); }

   [DllImport (OPENGL32, EntryPoint = "glEnd")] public static extern void End ();
   [DllImport (OPENGL32, EntryPoint = "glGenTextures")] public static extern void GenTextures (int n, HTexture* pTex);
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] public static extern nint GetProcAddress (string name);
   [DllImport (OPENGL32, EntryPoint = "wglMakeCurrent")] public static extern int MakeCurrent (HDC hdc, HGLRC hrc);
   [DllImport (OPENGL32, EntryPoint = "glPixelStorei")] static internal extern void PixelStore (EPixelStoreParam pname, int param);
   [DllImport (OPENGL32, EntryPoint = "glPolygonOffset")] static internal extern void PolygonOffset (float factor, float units);
   [DllImport (OPENGL32, EntryPoint = "glTexImage2D")] public static extern void TexImage2D (ETexTarget target, int level, EPixelInternalFormat publicformat, int width, int height, int border, EPixelFormat format, EPixelType type, void* pixels);
   public static void TexImage2D (ETexTarget target, EPixelInternalFormat infmt, int width, int height, EPixelFormat fmt, EPixelType type, byte[] data) 
      { fixed (byte* p = &data[0]) TexImage2D (target, 0, infmt, width, height, 0, fmt, type, p); }
   public static void TexImage2D (ETexTarget target, EPixelInternalFormat infmt, int width, int height, EPixelFormat fmt, EPixelType type, byte[,] data) 
      { fixed (byte* p = &data[0,0]) TexImage2D (target, 0, infmt, width, height, 0, fmt, type, p); }
   [DllImport (OPENGL32, EntryPoint = "glTexParameteri")] public static extern void TexParameter (ETexTarget target, ETexParam pname, int param);
   [DllImport (OPENGL32, EntryPoint = "glVertex2f")] public static extern void Vertex (float x, float y);
   [DllImport (OPENGL32, EntryPoint = "glVertex3f")] public static extern void Vertex (float x, float y, float z);
   [DllImport (OPENGL32, EntryPoint = "glViewport")] public static extern void Viewport (int x, int y, int width, int height);

   const string GDI32 = "gdi32.dll";
   const string OPENGL32 = "opengl32.dll";
   const string USER32 = "user32.dll";

   // Implementation -----------------------------------------------------------
   // Loads an OpenGL entry-point (using dynamic load from the DLL) and returns a
   // raw Delegate that can be cast to the appropriate function signature
   static T Load<T> () where T : Delegate {
      Type type = typeof (T);
      nint proc = GetProcAddress (type.Name);
      if (proc == 0) throw new Exception ($"OpenGL function '{type.Name}' not found.");
      Delegate del = Marshal.GetDelegateForFunctionPointer (proc, type);
      return (T)del;
   }
}
#endregion
