namespace Nori;
using Ptr = nint;

public static class Host {
   public static void Init () {
      IPlatform.It = new GLFWPlatform ();
   }
}

class GLFWPlatform : IPlatform {
   public Ptr GetGLProcAddress (string name) {
      var szName = Marshal.StringToHGlobalAnsi (name);
      Ptr proc = GLFW.GetProcAddress (szName);
      Marshal.FreeHGlobal (szName);
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc;
   }
}

