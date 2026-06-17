// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GLFWHost.cs
// ║║║║╬║╔╣║ Implements GLFWHost, which provides GLFW-specific implementations of some interfaces
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GLFWHost -----------------------------------------------------------------------------
/// <summary>The GLFWHost class provides GLFW-specific implementations of some interfaces</summary>
/// The Nori.Hub whiteboard contains pointers to implementations of various interfaces like
/// IKeyboard, IMouse etc. Each of the different Nori.Host.* provides a particualar implementation,
/// and this here provides an implementation that uses GLFW to manage window creation, keyboard/mouse
/// event management, and OpenGL interface. It also implements a dispatcher that integrates with 
/// the GLFW message pump that is implemented here (in Window.Run). 
public static class GLFWHost {
   // Methods ------------------------------------------------------------------
   /// <summary>Init should be called once to initialize the GLFWHost</summary>
   /// A basic application using this looks like this: 
   /// 
   /// static void Main () { 
   ///    Lib.Init ();                       // Initialize Nori core library
   ///    GLFWHost.Init (OnReady);           // Initialize GLFW system
   ///    var w = new Nori.Window (...);     // Create application main-window
   ///    w.Run ();                          // Run the message pump
   /// }   
   /// static void OnReady () => Lux.UIScene = new DemoScene ();  
   /// 
   /// When this Init function returns, Hub.OpenGL, Hub.Dispatcher, Hub.Keyboard, Hub.Mouse
   /// are all set up (dependency injection). However, the Lux renderer is still being initialized
   /// at this point, and you can start using that only after the OnReady callback you supplied has
   /// been called (as in the example above that places the scene-creation code inside that callback)
   /// 
   /// Note: In fact, the Lux renderer is initialized only during the creation of the Nori.Window,
   /// and OnReady will be called during that process (during the call to new Nori.Window above).
   public static void Init (Action onReady) {
      if (!mInited) {
         mInited = true;
         Hub.Dispatcher = new GLFWDispatcher ();
         Hub.OpenGL = new GLFWOpenGL ();
         Hub.Keyboard = new GLFWKeyboard ();
         Hub.Mouse = new GLFWMouse ();
         SynchronizationContext.SetSynchronizationContext (new GLFWSyncContext (Hub.Dispatcher));
         OnReady = onReady;
      }
   }
   static bool mInited;

   // Implementation -----------------------------------------------------------
   internal static Action? OnReady;
   internal static Action<int, int>? OnPaint;
   internal static Window? Win;
}
#endregion
