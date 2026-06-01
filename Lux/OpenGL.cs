using Nori;

static unsafe class GL2 {
   public static void ClearColor (float r, float g, float b, float a) => glClearColor (r, g, b, a);
   static delegate* unmanaged<float, float, float, float, void> glClearColor;

   // Implementation -----------------------------------------------------------
   static GL2 () {
      glClearColor = (delegate* unmanaged<float, float, float, float, void>)Get ("glClearColor");
   }
   static nint Get (string name) => IPlatform.It.GetGLProcAddress (name);
}
