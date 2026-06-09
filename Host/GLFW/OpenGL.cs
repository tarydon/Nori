// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OpenGL.cs
// ║║║║╬║╔╣║ Implements GLFWOpenGL : a GLFW-specific implementation of IOpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Runtime.InteropServices;
using Ptr = nint;

#region class GLFWOpenGL ---------------------------------------------------------------------------
/// <summary>GLFWOpenGL is a GLFW-specific implementation of the IOpenGL interface</summary>
/// OpenGL itself is a huge interface with hundreds of functions. However, there are only a few
/// 'differences' between various OpenGL implementations and that is all that is captured in the
/// IOpenGL interface. The bulk of the renderer code works by first using GetGLProcAddress to get
/// pointers to the dozens of OpenGL function it needs (like "glCompileShader" or "glDrawArrays"),
/// and then using those pointers. So all those functions don't need to be exposed in this
/// interface directly, but are obtained indirectly using the GetGLProcAddress. 
class GLFWOpenGL : IOpenGL {
   // This gets set by the Lux renderer, and it typically just renders the
   // currently installed UIScene into the viewport
   public Action<int, int> OnPaint { set => GLFWHost.OnPaint = value; }
   
   // The 'bootstrap' function used by the renderer to fetch pointers to all the other
   // functions it needs
   public Ptr GetGLProcAddress (string name) {
      var szName = Marshal.StringToHGlobalAnsi (name);
      Ptr proc = GLFW.GetProcAddress (szName);
      Marshal.FreeHGlobal (szName);
      if (proc == 0) throw new Exception ($"OpenGL function '{name}' not found.");
      return proc;
   }

   // Called by the renderer to tell the GL system that the window needs to be redrawn
   // In this implementation, it simply wakes up the GLFW message pump (if it is dormant) by
   // posting a dummy event into the queue
   public void Redraw () => GLFW.PostEmptyEvent ();

   // Sets whether the mouse cursor is visible over the OpenGL render surface
   public bool CursorVisible { set { } }

   // The current DPI-Scaling is fetched from the current GLFW window, if one has been 
   // created. When tests are running, we always set the DPIScale to 1 (for consistency across
   // machines / across time). 
   public float DPIScale {
      get {
         if (mDPIScale == 0) {
            if (Lib.Testing) mDPIScale = 1;
            else if (GLFWHost.Win is { } win) mDPIScale = win.DPIScale;
            else return 1;
         }
         return mDPIScale;
      }
   }
   float mDPIScale = 0;
}
#endregion
