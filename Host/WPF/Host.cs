// ────── ╔╗
// ╔═╦╦═╦╦╬╣ WPFHost.cs
// ║║║║╬║╔╣║ Implements a Nori.Host that works with WPF
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
namespace Nori;

#region class WPFHost ------------------------------------------------------------------------------
/// <summary>WPFHost provides WPF-specific implementations of some interfaces</summary>
/// When the Nori rendering system is used with WPF, there is a WPF main window with WPF
/// elements such as menus, status bars, tool bars etc, all surrounding a 'rendering area' where
/// the actual Nori-rendered content should be displayed. 
public static class WPFHost {
   // Methods ------------------------------------------------------------------
   /// <summary>Init should be called once to initialize the WPFHost, and create the GL render surface</summary>
   /// Typically, that rendering area is represented as a WPF.Border element, and an actual OpenGL
   /// rendering surface is created by this host, wrapped around with a UIElement and returned so it
   /// can be placed as a child. So a typical WPF-based Nori application looks like this:
   /// 
   /// public partial class DemoWindow : System.Windows.Window {
   ///    public MainWindow () {
   ///       Lib.Init ();
   ///       InitializeComponent ();
   ///       mBorder.Child = WPFHost.Create (this, OnReady);
   ///    }
   ///   
   ///    void OnReady () => Lux.UIScene = new DemoScene ();
   /// }
   /// 
   /// When this Init function returns, Hub.OpenGL, Hub.Dispatcher, Hub.Keyboard, Hub.Mouse
   /// are all set up (dependency injection). However, the Lux renderer is still being initialized
   /// at this point, and you can start using that only after the OnReady callback you supplied has
   /// been called (as in the example above that places the scene-creation code inside that callback)
   /// 
   /// Note: the OnReady function is called a few milliseconds after the WPFHost.Create call returns.
   /// The OpenGL surface is created asynchronously and is not ready to use after WPFHost.Create 
   /// returns. 
   public static UIElement Init (Window main, Action onReady) {
      if (!mInited) {
         mInited = true;
         Hub.Dispatcher = new WPFDispatcher (main.Dispatcher);
         Hub.OpenGL = new WPFOpenGL ();
         Hub.Keyboard = new WPFKeyboard ();
         Hub.Mouse = new WPFMouse ();
         Main = main;
         OnReady = onReady;
         GLPanel = Panel.It;
      }
      return Panel.It;
   }
   static bool mInited;

   static internal Window? Main;
   static internal Action? OnReady;
   static internal Action<int, int>? OnPaint;
   static internal Panel? GLPanel;
}
#endregion
