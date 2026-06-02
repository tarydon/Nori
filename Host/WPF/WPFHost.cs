using System.Runtime.InteropServices;
namespace Nori;
using Ptr = nint;

public static class Host {
   public static void Init () {
      IPlatform.It = new WPFPlatform ();
   }
}

class WPFPlatform : IPlatform {
   public WPFPlatform () => Lib = NativeLibrary.Load ("opengl32.dll");
   readonly Ptr Lib;

   public Ptr GetGLProcAddress (string name) {
      Ptr proc = WGLGetProcAddress (name);
      if (proc == 0) 
         proc = NativeLibrary.GetExport (Lib, name);
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc; 
   }

   const string OPENGL32 = "opengl32.dll";
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] public static extern Ptr WGLGetProcAddress (string name);
}

