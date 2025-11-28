using Nori.Internal;
namespace Nori;
using static CallingConvention;
using Ptr = nint;

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
   // Create a GLFW window
   [DllImport (LIB, EntryPoint = "glfwCreateWindow", CallingConvention = Cdecl)]
   public static extern HWindow CreateWindow (int width, int height, byte[] title, HMonitor monitor, HWindow share);

   // Returns the size of the frame-buffer attached to a window (OpenGL viewport size)
   [DllImport (LIB, EntryPoint = "glfwGetFramebufferSize", CallingConvention = Cdecl)]
   public static extern void GetFramebufferSize (HWindow window, out int width, out int height);

   // Returns handles to the set of monitors in the system
   [DllImport (LIB, EntryPoint = "glfwGetMonitors", CallingConvention = Cdecl)]
   public static extern Ptr GetMonitors (out int count);

   // Returns the scaling factors of the monitor
   [DllImport (LIB, EntryPoint = "glfwGetMonitorContentScale", CallingConvention = Cdecl)]
   public static extern void GetMonitorContentScale (HMonitor monitor, out float xScale, out float yScale);

   // Returns the name of a monitor
   [DllImport (LIB, EntryPoint = "glfwGetMonitorName", CallingConvention = Cdecl)]
   public static extern HString GetMonitorName (HMonitor monitor);

   // Returns the position of the top-left corner of the monitor in overall screen space
   [DllImport (LIB, EntryPoint = "glfwGetMonitorPos", CallingConvention = Cdecl)]
   public static extern void GetMonitorPosition (HMonitor monitor, out int x, out int y);

   // Gets the current work rectangle of this monitor in overall screen space
   [DllImport (LIB, EntryPoint = "glfwGetMonitorWorkarea", CallingConvention = Cdecl)]
   public static extern void GetMonitorWorkArea (HMonitor monitor, out int x, out int y, out int width, out int height);

   // Returns the primary monitor connected to the system
   [DllImport (LIB, EntryPoint = "glfwGetPrimaryMonitor", CallingConvention = Cdecl)]
   public static extern HMonitor GetPrimaryMonitor ();

   // Fetches the address of an OpenGL procedure or extension
   [DllImport (LIB, EntryPoint = "glfwGetProcAddress", CallingConvention = Cdecl)]
   public static extern Ptr GetProcAddress (Ptr procName);

   // Returns the current video mode of the given monitor
   [DllImport (LIB, EntryPoint = "glfwGetVideoMode", CallingConvention = Cdecl)]
   public static extern Ptr GetVideoMode (HMonitor monitor);

   // Returns a boolean window attribute
   [DllImport (LIB, EntryPoint = "glfwGetWindowAttrib", CallingConvention = Cdecl)]
   public static extern bool GetWindowAttribute (HWindow window, EWindowAttr attribute);

   // Gets the margins used by the window's non-client area in all 4 directions
   [DllImport (LIB, EntryPoint = "glfwGetWindowFrameSize", CallingConvention = Cdecl)]
   public static extern void GetWindowFrameSize (HWindow window, out int left, out int top, out int right, out int bottom);

   // Returns the monitor a window is running on 
   [DllImport (LIB, EntryPoint = "glfwGetWindowMonitor", CallingConvention = Cdecl)]
   public static extern HMonitor GetWindowMonitor (HWindow window);

   // Gets the position of the top-left corner of the window's client area
   [DllImport (LIB, EntryPoint = "glfwGetWindowPos", CallingConvention = Cdecl)]
   public static extern void GetWindowPosition (HWindow window, out int x, out int y);

   // Gets the size of a window's client area
   [DllImport (LIB, EntryPoint = "glfwGetWindowSize", CallingConvention = Cdecl)]
   public static extern void GetWindowSize (HWindow window, out int width, out int height);

#if WINDOWS
   // Returns the HWND handle of the GLFW window
   [DllImport (LIB, EntryPoint = "glfwGetWin32Window", CallingConvention = Cdecl)]
   public static extern Ptr GetWin32Window (HWindow window);
#endif

   // Initializes the GLFW library. 
   // Must be called before most GLFW functions can be used, and you need to call 
   // Terminate at the end (if this function returns true)
   [DllImport (LIB, EntryPoint = "glfwInit", CallingConvention = Cdecl)]
   public static extern bool Init ();

   // Makes the GL context attached to the given window as 'current'
   [DllImport (LIB, EntryPoint = "glfwMakeContextCurrent", CallingConvention = Cdecl)]
   public static extern void MakeContextCurrent (HWindow window);

   // Maximize a window
   [DllImport (LIB, EntryPoint = "glfwMaximizeWindow", CallingConvention = Cdecl)]
   public static extern void MaximizeWindow (HWindow window);

   // Pumps any existing messages in the queue and returns immediately
   // (if there are no messages, this does not wait)
   [DllImport (LIB, EntryPoint = "glfwPollEvents", CallingConvention = Cdecl)]
   public static extern void PollEvents ();

   // Restores a window from maximized
   [DllImport (LIB, EntryPoint = "glfwRestoreWindow", CallingConvention = Cdecl)]
   public static extern void RestoreWindow (HWindow window);

   // Sets the error callback, which is called with an error code and human-readable description
   // each time a GLFW error occurs
   [DllImport (LIB, EntryPoint = "glfwSetErrorCallback", CallingConvention = Cdecl)]
   [return: MarshalAs (UnmanagedType.FunctionPtr, MarshalTypeRef = typeof (ErrorCallback))]
   public static extern ErrorCallback SetErrorCallback (ErrorCallback errorHandler);

   // Sets up the monitor callback
   [UnmanagedFunctionPointer (Cdecl)] public delegate void MonitorCallback (HMonitor monitor, ConnectionStatus status);
   [DllImport (LIB, EntryPoint = "glfwSetMonitorCallback", CallingConvention = Cdecl)]
   public static extern Ptr SetMonitorCallback (MonitorCallback monitorCallback);

   // Sets the window's position, in pixels
   [DllImport (LIB, EntryPoint = "glfwSetWindowPos", CallingConvention = Cdecl)]
   public static extern void SetWindowPosition (HWindow window, int x, int y);

   // Sets the window client area size in pixels
   [DllImport (LIB, EntryPoint = "glfwSetWindowSize", CallingConvention = Cdecl)]
   public static extern void SetWindowSize (HWindow window, int width, int height);

   // Swap the front and back GL buffers (paints to screen)
   [DllImport (LIB, EntryPoint = "glfwSwapBuffers", CallingConvention = Cdecl)]
   public static extern void SwapBuffers (HWindow window);

   // Set to 1 to wait for vertical retrace interval before swap-buffers
   [DllImport (LIB, EntryPoint = "glfwSwapInterval", CallingConvention = Cdecl)]
   public static extern void SwapInterval (int interval);

   // Destroys all remaining windows and cursors and resets the system.
   // Once this function is called, you must again call Init before using GLFW functions
   [DllImport (LIB, EntryPoint = "glfwTerminate", CallingConvention = Cdecl)]
   public static extern void Terminate ();

   // Pumps messages and waits until a message appears in our queue
   [DllImport (LIB, EntryPoint = "glfwWaitEvents", CallingConvention = Cdecl)]
   public static extern void WaitEvents ();

   // Set up hints before creating a window
   [DllImport (LIB, EntryPoint = "glfwWindowHint", CallingConvention = Cdecl)]
   public static extern void WindowHint (Hint hint, int value);
   public static void WindowHint (Hint hint, ClientApi api) => WindowHint (hint, (int)api);
   public static void WindowHint (Hint hint, GLProfile profile) => WindowHint (hint, (int)profile);
   public static void WindowHint (Hint hint, bool value) => WindowHint (hint, value ? 1 : 0);
   
   // Should this window close?
   [DllImport (LIB, EntryPoint = "glfwWindowShouldClose", CallingConvention = Cdecl)]
   public static extern bool WindowShouldClose (HWindow window);

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
   private static void GlfwError (ErrorCode code, Ptr ptrMessage) {
      string message = Marshal.PtrToStringUTF8 (ptrMessage) ?? "";
      throw new Exception ($"GLFW exception {code}: {message}");
   }
}
#endregion
