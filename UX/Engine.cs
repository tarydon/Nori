using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;

namespace Nori.UX;

public static class Engine {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The current mouse position, in pixels (top left = 0,0)
   /// </summary>
   public static Vec2S MousePos { get; private set; }

   /// <summary>
   /// Group containing retained drawing for UI
   /// </summary>
   /// Some UI nodes use retained mode rendering (for example, a node that might
   /// render a thumbnail from a drawing or model). The VNodes created for such
   /// rendering get added as children of this global group. As those UI nodes
   /// get out of scope, these VNodes are removed from the RetainedVN
   public static VNode Retained => mRetained;
   static readonly GroupVN mRetained = new ([]);

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
   /// <summary>
   /// Adds a 'retained mode' VNode to the visual graph
   /// </summary>
   /// See documentation for Retained property above
   public static void AddRetained (VNode node) {
      Debug.Assert (!mDeleteQueue.Contains (node));
      mRetained.Add (node);
   }

   public static void RegisterClass (NodeClass nc) {
   }

   /// <summary>
   /// Removes a node from the Retained VNode group
   /// </summary>
   /// Actually, this just queues it up for delete at the end of the frame, since
   /// we don't want to actually disturb the VNode tree halfway through a frame 
   /// render
   public static void RemoveRetained (VNode node) {
      Debug.Assert (mDeleteQueue.Contains (node));
      mDeleteQueue.Remove (node);
   }
   static readonly List<VNode> mDeleteQueue = [];

   // Implementation -----------------------------------------------------------
   static void EndOfFrame (Unit _) {
      mDeleteQueue.ForEach (mRetained.Remove);
      mDeleteQueue.Clear ();
   }
}
