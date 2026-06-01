namespace Nori;

public static class Host {
   public static void Init () {
      IPlatform.It = new GLFWPlatform ();
   }
}

class GLFWPlatform : IPlatform {
   public nint GetGLProcAddress (string name) => throw new NotImplementedException ();
}

