using System.Text;
using Nori;
using Ptr = nint;

static unsafe class GL2 {
   // Select the active texture unit
   public static void ActiveTexture (ETexUnit unit) => glActiveTexture (unit);
   static delegate* unmanaged<ETexUnit, void> glActiveTexture;

   // Attach a shader to a shader-pipeline (program)
   public static void AttachShader (HProgram program, HShader shader) => glAttachShader (program, shader);
   static delegate* unmanaged<HProgram, HShader, void> glAttachShader;

   // Bind a storage buffer to a buffer target 
   public static void BindBuffer (EBufferTarget target, HBuffer buffer) => glBindBuffer (target, buffer);
   static delegate* unmanaged<EBufferTarget, HBuffer, void> glBindBuffer;

   // Bind a frame buffer for use
   public static void BindFrameBuffer (EFrameBufferTarget target, HFrameBuffer buffer) => glBindFramebuffer (target, buffer);
   static delegate* unmanaged<EFrameBufferTarget, HFrameBuffer, void> glBindFramebuffer;

   // Bind a named texture to a texturing target
   public static void BindTexture (ETexTarget target, HTexture id) => glBindTexture (target, id);
   static delegate* unmanaged<ETexTarget, HTexture, void> glBindTexture;

   // Bind a render buffer to a target 
   public static void BindRenderBuffer (ERenderBufferTarget target, HRenderBuffer buffer) => glBindRenderbuffer (target, buffer);
   static delegate* unmanaged<ERenderBufferTarget, HRenderBuffer, void> glBindRenderbuffer;

   // Bind a vertex array object (VAO) for use
   public static void BindVertexArray (HVertexArray array) => glBindVertexArray (array);
   static delegate* unmanaged<HVertexArray, void> glBindVertexArray;

   // Specify pixel arithmetic 
   public static void BlendFunc (EBlendFactor src, EBlendFactor dest) => glBlendFunc (src, dest);
   static delegate* unmanaged<EBlendFactor, EBlendFactor, void> glBlendFunc;

   // Allocates and copies data to a buffer object's storage
   public static void BufferData (EBufferTarget target, int size, Ptr data, EBufferUsage usage) => glBufferData (target, size, data, usage);
   static delegate* unmanaged<EBufferTarget, int, Ptr, EBufferUsage, void> glBufferData;

   // Check the completeness of the frame buffer
   public static EFrameBufferStatus CheckFrameBufferStatus (EFrameBufferTarget target) => glCheckFramebufferStatus (target);
   static delegate* unmanaged<EFrameBufferTarget, EFrameBufferStatus> glCheckFramebufferStatus;

   // Clear buffers to preset values
   public static void Clear (EBuffer mask) => glClear (mask);
   static delegate* unmanaged<EBuffer, void> glClear;

   // Specify clear values for the color buffers
   public static void ClearColor (float r, float g, float b, float a) => glClearColor (r, g, b, a);
   static delegate* unmanaged<float, float, float, float, void> glClearColor;

   // Compile an OpenGL shader
   public static void CompileShader (HShader hShader) => glCompileShader (hShader);
   static delegate* unmanaged<HShader, void> glCompileShader;

   // Create an OpenGL program (shader pipeline)
   public static HProgram CreateProgram () => glCreateProgram ();
   static delegate* unmanaged<HProgram> glCreateProgram;

   // Create an OpenGL shader (one step of a shader pipeline)
   public static HShader CreateShader (EShader type) => glCreateShader (type);
   static delegate* unmanaged<EShader, HShader> glCreateShader;

   // Delete a named buffer object
   public static void DeleteBuffer (HBuffer buffer) => glDeleteBuffers (1, &buffer);
   static delegate* unmanaged<int, HBuffer*, void> glDeleteBuffers;

   // Delete a texture
   public static void DeleteTexture (HTexture texture) => glDeleteTextures (1, &texture);
   static delegate* unmanaged<int, HTexture*, void> glDeleteTextures;

   // Deletes a vertex array object
   public static void DeleteVertexArray (HVertexArray array) => glDeleteVertexArrays (1, &array);
   static delegate* unmanaged<int, HVertexArray*, void> glDeleteVertexArrays;

   // Disable GL capabilities
   public static void Disable (ECap cap) => glDisable (cap);
   static delegate* unmanaged<ECap, void> glDisable;

   // Disable a vertex attribute array
   public static void DisableVertexAttribArray (int index) => glDisableVertexAttribArray (index);
   static delegate* unmanaged<int, void> glDisableVertexAttribArray;

   // Render primitives from array data
   public static void DrawArrays (EMode mode, int start, int count) => glDrawArrays (mode, start, count);
   static delegate* unmanaged<EMode, int, int, void> glDrawArrays;

   // Instanced drawing (multiple instances)
   public static void DrawArraysInstanced (EMode mode, int start, int instances, int count) => glDrawArraysInstanced (mode, start, instances, count);
   static delegate* unmanaged<EMode, int, int, int, void> glDrawArraysInstanced;

   // Indexed drawing from an array (with baseVertex added to each index)
   public static void DrawElementsBaseVertex (EMode mode, int count, EIndexType type, Ptr indices, int baseVertex) => glDrawElementsBaseVertex (mode, count, type, indices, baseVertex);
   static delegate* unmanaged<EMode, int, EIndexType, Ptr, int, void> glDrawElementsBaseVertex;

   // Enable GL capabilities
   public static void Enable (ECap cap) => glEnable (cap);
   static delegate* unmanaged<ECap, void> glEnable;
   public static void Enable (ECap cap, bool v) { if (v) Enable (cap); else Disable (cap); }

   // Specify that a particular element (specified by glVertexAttribPointer) is in use
   public static void EnableVertexAttribArray (int index) => glEnableVertexAttribArray (index);
   static delegate* unmanaged<int, void> glEnableVertexAttribArray;

   // Block until all GL execution is complete
   public static void Finish () => glFinish ();
   static delegate* unmanaged<void> glFinish;

   // Attach render-buffer to frame buffer
   public static void FrameBufferRenderBuffer (EFrameBufferTarget ftarget, EFrameBufferAttachment attachment, HRenderBuffer rbo) => glFramebufferRenderbuffer (ftarget, attachment, rbo);
   static delegate* unmanaged<EFrameBufferTarget, EFrameBufferAttachment, HRenderBuffer, void> glFramebufferRenderbuffer;

   // Allocate a new data-storage buffer object
   public static HBuffer GenBuffer () { HBuffer buffer; glGenBuffers (1, &buffer); return buffer; }
   static delegate* unmanaged<int, HBuffer*, void> glGenBuffers;

   // Create a new framebuffer (for render-to-image)
   public static HFrameBuffer GenFrameBuffer () { HFrameBuffer buffer; glGenFramebuffers (1, &buffer); return buffer; }
   static delegate* unmanaged<int, HFrameBuffer*, void> glGenFramebuffers;

   // Create a new render buffer
   public static HRenderBuffer GenRenderBuffer () { HRenderBuffer buffer; glGenRenderbuffers (1, &buffer); return buffer; }
   static delegate* unmanaged<int, HRenderBuffer*, void> glGenRenderbuffers;

   // Generate texture names
   public static void GenTextures (int n, HTexture* pTex) => glGenTextures (n, pTex);
   static delegate* unmanaged<int, HTexture*, void> glGenTextures;
   public static HTexture GenTexture () { HTexture tex; GenTextures (1, &tex); return tex; }

   // Allocate a new VertexArray object (VAO)
   public static HVertexArray GenVertexArray () { HVertexArray array; glGenVertexArrays (1, &array); return array; }
   static delegate* unmanaged<int, HVertexArray*, void> glGenVertexArrays;

   // Gets information about a program attribute
   public static void GetActiveAttrib (HProgram program, int index, out int size, out EDataType type, out string name, out int location) {
      Span<byte> data = stackalloc byte[256];
      fixed (byte* p = data) {
         glGetActiveAttrib (program, index, 255, out int length, out size, out type, (Ptr)p);
         name = Encoding.UTF8.GetString (data[..length]);
         location = GetAttribLocation (program, name);
      }
   }
   static delegate* unmanaged<HProgram, int, int, out int, out int, out EDataType, Ptr, void> glGetActiveAttrib;

   // <summary>Gets information about a uniform variable
   public static void GetActiveUniform (HProgram program, int index, out int size, out EDataType type, out string name, out int location) {
      Span<byte> data = stackalloc byte[256];
      fixed (byte* p = data) {
         glGetActiveUniform (program, index, 255, out int length, out size, out type, (Ptr)p);
         name = Encoding.UTF8.GetString (data[..length]);
         location = GetUniformLocation (program, name);
      }
   }
   static delegate* unmanaged<HProgram, int, int, out int, out int, out EDataType, Ptr, void> glGetActiveUniform;

   // Gets information about an attribute's location
   public static int GetAttribLocation (HProgram program, string name) => glGetAttribLocation (program, name);
   static delegate* unmanaged<HProgram, string, int> glGetAttribLocation;

   // Gets a parameter from a program object
   public static int GetProgram (HProgram program, EProgramParam pname) { int n; glGetProgramiv (program, pname, &n); return n; }
   static delegate* unmanaged<HProgram, EProgramParam, int*, void> glGetProgramiv;

   // Gets the error log for a program
   public static string GetProgramInfoLog (HProgram program) {
      int length = GetProgram (program, EProgramParam.InfoLogLength), actual = length;
      if (length <= 1) return "";
      byte[] data = new byte[length];
      fixed (byte* p = data) glGetProgramInfoLog (program, length, &actual, p);
      return Encoding.UTF8.GetString (data);
   }
   static delegate* unmanaged<HProgram, int, int*, byte*, void> glGetProgramInfoLog;

   // Gets some information from a shader
   public static int GetShader (HShader shader, EShaderParam pname) { int n; glGetShaderiv (shader, pname, &n); return n; }
   static delegate* unmanaged<HShader, EShaderParam, int*, void> glGetShaderiv;

   // Gets the error log for a shader
   public static string GetShaderInfoLog (HShader shader) {
      int length = GetShader (shader, EShaderParam.InfoLogLength), actual = length;
      if (length <= 1) return "";
      byte[] data = new byte[length];
      fixed (byte* p = data) glGetShaderInfoLog (shader, length, &actual, p);
      return Encoding.UTF8.GetString (data);
   }
   static delegate* unmanaged<HShader, int, int*, byte*, void> glGetShaderInfoLog;

   // Gets the location (slot) of a uniform variable
   public static int GetUniformLocation (HProgram program, string name) => glGetUniformLocation (program, name);
   static delegate* unmanaged<HProgram, string, int> glGetUniformLocation;

   // Read a block of pixels from the frame buffer
   public static void ReadPixels (int x, int y, int width, int height, EPixelFormat format, EPixelType type, Ptr pixels) => glReadPixels (x, y, width, height, format, type, pixels);
   static delegate* unmanaged<int, int, int, int, EPixelFormat, EPixelType, Ptr, void> glReadPixels;
   public static void ReadPixels<T> (int x, int y, int width, int height, EPixelFormat format, EPixelType ptype, T[] data) where T : struct {
      GCHandle pixelptr = GCHandle.Alloc (data, GCHandleType.Pinned);
      try { ReadPixels (x, y, width, height, format, ptype, pixelptr.AddrOfPinnedObject ()); } finally { pixelptr.Free (); }
   }

   // Set pixel storage modes
   public static void PixelStore (EPixelStoreParam pname, int param) => glPixelStorei (pname, param);
   static delegate* unmanaged<EPixelStoreParam, int, void> glPixelStorei;

   // Set the scale and units used to calculate depth values
   public static void PolygonOffset (float factor, float units) => glPolygonOffset (factor, units);
   static delegate* unmanaged<float, float, void> glPolygonOffset;

   // Define the scissor box
   public static void Scissor (int x, int y, int width, int height) => glScissor (x, y, width, height);
   static delegate* unmanaged<int, int, int, int, void> glScissor;

   // Specify a two-dimensional texture image
   public static void TexImage2D (ETexTarget target, int level, EPixelInternalFormat publicformat, int width, int height, int border, EPixelFormat format, EPixelType type, void* pixels)
      => glTexImage2D (target, level, publicformat, width, height, border, format, type, pixels);
   static delegate* unmanaged<ETexTarget, int, EPixelInternalFormat, int, int, int, EPixelFormat, EPixelType, void*, void> glTexImage2D;      
   public static void TexImage2D (ETexTarget target, EPixelInternalFormat infmt, int width, int height, EPixelFormat fmt, EPixelType type, byte[] data)
      {  fixed (byte* p = &data[0]) TexImage2D (target, 0, infmt, width, height, 0, fmt, type, p); }
   public static void TexImage2D (ETexTarget target, EPixelInternalFormat infmt, int width, int height, EPixelFormat fmt, EPixelType type, byte[,] data)
      { fixed (byte* p = &data[0, 0]) TexImage2D (target, 0, infmt, width, height, 0, fmt, type, p); }

   // Set texture parameters
   public static void TexParameter (ETexTarget target, ETexParam pname, int param) => glTexParameteri (target, pname, param);
   static delegate* unmanaged<ETexTarget, ETexParam, int, void> glTexParameteri;

   // Set the viewport
   public static void Viewport (int x, int y, int width, int height) => glViewport (x, y, width, height);
   static delegate* unmanaged<int, int, int, int, void> glViewport;

   // Implementation -----------------------------------------------------------
   static GL2 () {
      glActiveTexture = (delegate* unmanaged<ETexUnit, void>)Get ("glActiveTexture");
      glAttachShader = (delegate* unmanaged<HProgram, HShader, void>)Get ("glAttachShader");
      glBindBuffer = (delegate* unmanaged<EBufferTarget, HBuffer, void>)Get ("glBindBuffer");
      glBindFramebuffer = (delegate* unmanaged<EFrameBufferTarget, HFrameBuffer, void>)Get ("glBindFramebuffer");
      glBindRenderbuffer = (delegate* unmanaged<ERenderBufferTarget, HRenderBuffer, void>)Get ("glBindRenderbuffer");
      glBindTexture = (delegate* unmanaged<ETexTarget, HTexture, void>)Get ("glBindTexture");
      glBindVertexArray = (delegate* unmanaged<HVertexArray, void>)Get ("glBindVertexArray");
      glBlendFunc = (delegate* unmanaged<EBlendFactor, EBlendFactor, void>)Get ("glBlendFunc");
      glBufferData = (delegate* unmanaged<EBufferTarget, int, Ptr, EBufferUsage, void>)Get ("glBufferData");
      glCheckFramebufferStatus = (delegate* unmanaged<EFrameBufferTarget, EFrameBufferStatus>)Get ("glCheckFramebufferStatus");
      glClear = (delegate* unmanaged<EBuffer, void>)Get ("glClear");
      glClearColor = (delegate* unmanaged<float, float, float, float, void>)Get ("glClearColor");
      glCreateProgram = (delegate* unmanaged<HProgram>)Get ("glCreateProgram");
      glCreateShader = (delegate* unmanaged<EShader, HShader>)Get ("glCreateShader");
      glCompileShader = (delegate* unmanaged<HShader, void>)Get ("glCompileShader");
      glDisable = (delegate* unmanaged<ECap, void>)Get ("glDisable");
      glDisableVertexAttribArray = (delegate* unmanaged<int, void>)Get ("glDisableVertexAttribArray");
      glDeleteBuffers = (delegate* unmanaged<int, HBuffer*, void>)Get ("glDeleteBuffers");
      glDeleteTextures = (delegate* unmanaged<int, HTexture*, void>)Get ("glDeleteTextures");
      glDeleteVertexArrays = (delegate* unmanaged<int, HVertexArray*, void>)Get ("glDeleteVertexArrays");
      glDrawArrays = (delegate* unmanaged<EMode, int, int, void>)Get ("glDrawArrays");
      glDrawArraysInstanced = (delegate* unmanaged<EMode, int, int, int, void>)Get ("glDrawArraysInstanced");
      glDrawElementsBaseVertex = (delegate* unmanaged<EMode, int, EIndexType, Ptr, int, void>)Get ("glDrawElementsBaseVertex");
      glEnable = (delegate* unmanaged<ECap, void>)Get ("glEnable");
      glEnableVertexAttribArray = (delegate* unmanaged<int, void>)Get ("glEnableVertexAttribArray");
      glFinish = (delegate* unmanaged<void>)Get ("glFinish");
      glFramebufferRenderbuffer = (delegate* unmanaged<EFrameBufferTarget, EFrameBufferAttachment, HRenderBuffer, void>)Get ("glFramebufferRenderbuffer");
      glGenBuffers = (delegate* unmanaged<int, HBuffer*, void>)Get ("glGenBuffers");
      glGenFramebuffers = (delegate* unmanaged<int, HFrameBuffer*, void>)Get ("glGenFramebuffers");
      glGenRenderbuffers = (delegate* unmanaged<int, HRenderBuffer*, void>)Get ("glGenRenderbuffers");
      glGenTextures = (delegate* unmanaged<int, HTexture*, void>)Get ("glGenTextures");
      glGenVertexArrays = (delegate* unmanaged<int, HVertexArray*, void>)Get ("glGenVertexArrays");
      glGetActiveAttrib = (delegate* unmanaged<HProgram, int, int, out int, out int, out EDataType, Ptr, void>)Get ("glGetActiveAttrib");
      glGetActiveUniform = (delegate* unmanaged<HProgram, int, int, out int, out int, out EDataType, Ptr, void>)Get ("glGetActiveUniform");
      glGetAttribLocation = (delegate* unmanaged<HProgram, string, int>)Get ("glGetAttribLocation");
      glGetProgramiv = (delegate* unmanaged<HProgram, EProgramParam, int*, void>)Get ("glGetProgramiv");
      glGetProgramInfoLog = (delegate* unmanaged<HProgram, int, int*, byte*, void>)Get ("glGetProgramInfoLog");
      glGetShaderiv = (delegate* unmanaged<HShader, EShaderParam, int*, void>)Get ("glGetShaderiv");
      glGetShaderInfoLog = (delegate* unmanaged<HShader, int, int*, byte*, void>)Get ("glGetShaderInfoLog");
      glGetUniformLocation = (delegate* unmanaged<HProgram, string, int>) Get ("glGetUniformLocation");
      glReadPixels = (delegate* unmanaged<int, int, int, int, EPixelFormat, EPixelType, Ptr, void>)Get ("glReadPixels");
      glPixelStorei = (delegate* unmanaged<EPixelStoreParam, int, void>)Get ("glPixelStorei");
      glPolygonOffset = (delegate* unmanaged<float, float, void>)Get ("glPolygonOffset");
      glScissor = (delegate* unmanaged<int, int, int, int, void>)Get ("glScissor");
      glTexImage2D = (delegate* unmanaged<ETexTarget, int, EPixelInternalFormat, int, int, int, EPixelFormat, EPixelType, void*, void>)Get ("glTexImage2D");
      glTexParameteri = (delegate* unmanaged<ETexTarget, ETexParam, int, void>)Get ("glTexParameteri");
      glViewport = (delegate* unmanaged<int, int, int, int, void>)Get ("glViewport");
   }
   static nint Get (string name) => IPlatform.It.GetGLProcAddress (name);
}
