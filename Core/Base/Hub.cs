// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Hub.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>A central whiteboard</summary>
public static class Hub {
   /// <summary>The current dispatcher (used to execute code synchronously/asynchronously on UI thread)</summary>
   public static IDispatcher Dispatcher { get; set; } = null!;

   /// <summary>Abstraction for the current keyboard (provides keys, modifiers etc)</summary>
   public static IKeyboard Keyboard { get; set; } = null!;

   /// <summary>Abstraction for the current mouse (provides moves, clicks, wheel-rotates etc)</summary>
   public static IMouse Mouse { get; set; } = null!;

   /// <summary>The OpenGL server</summary>
   public static IOpenGL OpenGL { get; set; } = null!;
}
