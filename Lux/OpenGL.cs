using Nori;
using Ptr = nint;

static unsafe class GL2 {
   // Bind a named texture to a texturing target
   public static void BindTexture (ETexTarget target, HTexture id) { }
   static delegate* unmanaged<ETexTarget, HTexture, void> glBindTexture;

   // Specify pixel arithmetic
   public static void BlendFunc (EBlendFactor src, EBlendFactor dest) => glBlendFunc (src, dest);
   static delegate* unmanaged<EBlendFactor, EBlendFactor, void> glBlendFunc;

   // Clear buffers to preset values
   public static void Clear (EBuffer mask) => glClear (mask);
   static delegate* unmanaged<EBuffer, void> glClear;

   // Specify clear values for the color buffers
   public static void ClearColor (float r, float g, float b, float a) => glClearColor (r, g, b, a);
   static delegate* unmanaged<float, float, float, float, void> glClearColor;

   // Disable GL capabilities
   public static void Disable (ECap cap) => glDisable (cap);
   static delegate* unmanaged<ECap, void> glDisable;

   // Render primitives from array data
   public static void DrawArrays (EMode mode, int start, int count) => glDrawArrays (mode, start, count);
   static delegate* unmanaged<EMode, int, int, void> glDrawArrays;

   // Enable GL capabilities
   public static void Enable (ECap cap) => glEnable (cap);
   static delegate* unmanaged<ECap, void> glEnable;
   public static void Enable (ECap cap, bool v) { if (v) Enable (cap); else Disable (cap); }

   // Block until all GL execution is complete
   public static void Finish () => glFinish ();
   static delegate* unmanaged<void> glFinish;

   // Generate texture names
   public static void GenTextures (int n, HTexture* pTex) => glGenTextures (n, pTex);
   static delegate* unmanaged<int, HTexture*, void> glGenTextures;
   public static HTexture GenTexture () { HTexture tex; GenTextures (1, &tex); return tex; }

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
      glBindTexture = (delegate* unmanaged<ETexTarget, HTexture, void>)Get ("glBindTexture");
      glBlendFunc = (delegate* unmanaged<EBlendFactor, EBlendFactor, void>)Get ("glBlendFunc");
      glClear = (delegate* unmanaged<EBuffer, void>)Get ("glClear");
      glClearColor = (delegate* unmanaged<float, float, float, float, void>)Get ("glClearColor");
      glDisable = (delegate* unmanaged<ECap, void>)Get ("glDisable");
      glDrawArrays = (delegate* unmanaged<EMode, int, int, void>)Get ("glDrawArrays");
      glEnable = (delegate* unmanaged<ECap, void>)Get ("glEnable");
      glFinish = (delegate* unmanaged<void>)Get ("glFinish");
      glGenTextures = (delegate* unmanaged<int, HTexture*, void>)Get ("glGenTextures");
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
