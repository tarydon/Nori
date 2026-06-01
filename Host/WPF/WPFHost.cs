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
      Ptr ptr = WGLGetProcAddress (name);
      if (ptr == 0) 
         ptr = NativeLibrary.GetExport (Lib, name);
      return ptr; 
   }

   const string OPENGL32 = "opengl32.dll";
   [DllImport (OPENGL32, EntryPoint = "wglGetProcAddress")] public static extern Ptr WGLGetProcAddress (string name);
}

