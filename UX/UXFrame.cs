namespace Nori.UX;

public static class UXFrame {
   /// <summary>
   /// Reference to the current UX node
   /// </summary>
   public static ref UXNode N => ref mNodes[mCurrent];

   /// <summary>
   /// Begins a new layout pass given the available screen size
   /// </summary>
   public static void BeginLayout (Vec2S size) {
      mScreenSize = size;
      mUsed = 0; mCurrent = -1; 
   }

   /// <summary>
   /// Sets the current state of the mouse (used to implement hover/clicks/scroll)
   /// </summary>
   /// <param name="position">Mouse position in pixels, relative to top left</param>
   /// <param name="wheelDelta">Mouse wheel rotation since last frame</param>
   /// <param name="isPressed">Is the mouse button pressed?</param>
   public static void SetMouseState (Vec2S position, int wheelDelta, bool isPressed)
      => (mMousePos, mWheelDelta, mMousePressed) = (position, wheelDelta, isPressed);

   /// <summary>
   /// Ends the layout and generates render commands
   /// </summary>
   public static void EndLayout () {
      Lib.Check (mStack.Count == 0, "Unmatched Begin() in UXFrame");
   }

   /// <summary>
   /// Begins a new container
   /// </summary>
   public static void Begin () {
      if (mUsed >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mStack.Push (mCurrent); mCurrent = mUsed; mUsed++;
      mNodes[mCurrent] = new ();
   }

   /// <summary>
   /// Ends a container
   /// </summary>
   public static void End () {
      mCurrent = mStack.Pop (); 
   }

   // Private data -------------------------------------------------------------
   static Vec2S mScreenSize;              // Screen size
   static Vec2S mMousePos;                // Mouse position, relative to top left
   static int mWheelDelta;                // Wheel rotation since last frame
   static bool mMousePressed;             // Is the mouse currently pressed

   static UXNode[] mNodes = new UXNode[32];   // List of nodes 
   static Stack<int> mStack = [];         // Stack of currently open nodes
   static int mUsed;                      // Number of used nodes
   static int mCurrent;                   // Node that is currently being edited
}
