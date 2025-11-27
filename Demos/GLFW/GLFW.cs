using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.CallingConvention;
using Nori.Internal;
namespace Nori;

#region class GLFW ---------------------------------------------------------------------------------
/// <summary>
/// Class that encapsulates the GLFW functions
/// </summary>
static class GLFW {
   // Constructors -------------------------------------------------------------
   static GLFW () {
      Init ();
      SetErrorCallback (mError);
   }
   static readonly ErrorCallback mError = GlfwError;

   // GLFW wrappers ------------------------------------------------------------
#if WINDOWS
   // Returns the HWND handle of the GLFW window
   [DllImport (LIB, EntryPoint = "glfwGetWin32Window", CallingConvention = Cdecl)]
   public static extern IntPtr GetWin32Window (HWindow window);
#endif

   // Initializes the GLFW library. 
   // Must be called before most GLFW functions can be used, and you need to call 
   // Terminate at the end (if this function returns true)
   [DllImport (LIB, EntryPoint = "glfwInit", CallingConvention = Cdecl)]
   public static extern bool Init ();

   // Sets the error callback, which is called with an error code and human-readable description
   // each time a GLFW error occurs
   [DllImport (LIB, EntryPoint = "glfwSetErrorCallback", CallingConvention = Cdecl)]
   [return: MarshalAs (UnmanagedType.FunctionPtr, MarshalTypeRef = typeof (ErrorCallback))]
   public static extern ErrorCallback SetErrorCallback (ErrorCallback errorHandler);

   // Destroys all remaining windows and cursors and resets the system.
   // Once this function is called, you must again call Init before using GLFW functions
   [DllImport (LIB, EntryPoint = "glfwTerminate", CallingConvention = CallingConvention.Cdecl)]
   public static extern void Terminate ();

   // Constants ----------------------------------------------------------------
   // The native library name
#if LINUX
   const string LIB = "glfw3";
#elif OSX
   const string LIB = "libglfw.3";
#elif WINDOWS
   const string LIB = "glfw3";
#endif

   // Implementation -----------------------------------------------------------
   private static void GlfwError (ErrorCode code, nint ptrMessage) {
      string message = Marshal.PtrToStringUTF8 (ptrMessage) ?? "";
      throw new Exception ($"GLFW exception {code}: {message}");
   }
}
#endregion
