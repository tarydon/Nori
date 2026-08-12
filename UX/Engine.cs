namespace Nori.UX;

public static class Engine {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The current mouse position, in pixels (top left = 0,0)
   /// </summary>
   public static Vec2S MousePos { get; private set; }

   /// <summary>
   /// The screen size
   /// </summary>
   public static Vec2S ScreenSize { get; private set; }

   /// <summary>
   /// The set of typefaces used for rendering
   /// </summary>
   /// The NFace index within each UX.Node points to an entry from 
   /// this array
   public static TypeFace[] Typefaces = [];

   /// <summary>
   /// Mouse wheel rotation during this frame
   /// </summary>
   public static int WheelDelta { get; private set; }

   // Methods ------------------------------------------------------------------
}
