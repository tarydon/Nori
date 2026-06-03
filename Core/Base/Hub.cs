namespace Nori;

/// <summary>
/// A central whiteboard
/// </summary>
public static class Hub {
   /// <summary>
   /// The OpenGL server
   /// </summary>
   public static IOpenGL OpenGL { get; set; } = null!;
}
