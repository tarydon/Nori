// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXFrame.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Xml.Serialization;
using static System.Math;
using static Nori.UX.UXNode.ESizeMode;
namespace Nori.UX;

public static class UXFrame {
   /// <summary>Reference to the current UX node</summary>
   public static ref UXNode N => ref mNodes[mCurrent];

   public static UXNode[] All => mNodes;

   public static ref UXNode Get (int n) => ref mNodes[n];

   /// <summary>Begins a new layout pass given the available screen size</summary>
   public static void BeginLayout (Vec2S size) {
      mScreenSize = size;
      mUsed = 1; mCurrent = -1; 
   }

   /// <summary>Sets the current state of the mouse (used to implement hover/clicks/scroll)</summary>
   /// <param name="position">Mouse position in pixels, relative to top left</param>
   /// <param name="wheelDelta">Mouse wheel rotation since last frame</param>
   /// <param name="isPressed">Is the mouse button pressed?</param>
   public static void SetMouseState (Vec2S position, int wheelDelta, bool isPressed)
      => (mMousePos, mWheelDelta, mMousePressed) = (position, wheelDelta, isPressed);

   /// <summary>Ends the layout and generates render commands</summary>
   public static void EndLayout () {
      Lib.Check (mStack.Count == 0, "Unmatched Begin() in UXFrame");
      // At this point, the desired sizes of all the elements are already computed
      // (by the EndNode methods as element is closed)
      GrowChildElements (1);
   }

   static void GrowChildElements (int p) {
      Lib.Trace ($"Growing child elements {p}");
      ref UXNode par = ref mNodes[p];
      var children = par.GetChildren (mNodes);
      short childGaps = (short)(par.ChildGap * Max (par.ChildCount - 1, 0));
      int dxSpace = par.DX - par.Padding.Left - par.Padding.Right;
      int dySpace = par.DY - par.Padding.Top - par.Padding.Bottom;
      if (par.Horizontal) {
         dxSpace -= childGaps;
         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[children[i]];
            dxSpace -= child.DX;
            if (child.Height.Mode == Grow) child.DY += (short)(dySpace - child.DY);
            if (child.Width.Mode != Grow) children.RemoveAt (i);
         }
         if (children.Count == 0) goto Done;

         while (dxSpace > 0 ) {
            int smallest = short.MaxValue, secondSmallest = smallest, widthToAdd = dxSpace;
            foreach (var c in children) {
               ref UXNode child = ref mNodes[c];
               if (child.DX < smallest) { secondSmallest = smallest; smallest = child.DX; }
               if (child.DX > smallest) { secondSmallest = Min (secondSmallest, child.DX); widthToAdd = secondSmallest - smallest; }
            }
            widthToAdd = Max (Min (widthToAdd, dxSpace / children.Count), 1);

            foreach (var c in children) {
               ref UXNode child = ref mNodes[c];
               if (child.DX != smallest) continue;
               child.DX += (short)widthToAdd;
               dxSpace -= widthToAdd; if (dxSpace <= 0) break;
            }
         }
      } else {
         dySpace -= childGaps;
         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[children[i]];
            dySpace -= child.DY;
            if (child.Width.Mode == Grow) child.DX += (short)(dxSpace - child.DX);
            if (child.Height.Mode != Grow) children.RemoveAt (i);
         }
         if (children.Count == 0) goto Done;

         while (dySpace > 0) {
            int smallest = short.MaxValue, secondSmallest = smallest, heightToAdd = dySpace;
            foreach (var c in children) {
               ref UXNode child = ref mNodes[c];
               if (child.DY < smallest) { secondSmallest = smallest; smallest = child.DY; }
               if (child.DY > smallest) { secondSmallest = Min (secondSmallest, child.DY); heightToAdd = secondSmallest - smallest; }
            }
            heightToAdd = Max (Min (heightToAdd, dySpace / children.Count), 1);

            foreach (var c in children) {
               ref UXNode child = ref mNodes[c];
               if (child.DY != smallest) continue;
               child.DY += (short)heightToAdd;
               dySpace -= heightToAdd; if (dySpace <= 0) break;
            }
         }
      }

      Done:
      for (var c = par.FirstChild; c != 0; c = mNodes[c].Next)
         GrowChildElements (c);
   }

   /// <summary>Begins a new container</summary>
   public static ref UXNode BeginNode () {
      if (mUsed >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mParent = mCurrent; mStack.Push (mCurrent); mCurrent = mUsed; mUsed++;
      mNodes[mCurrent] = new ();
      N.Id = mCurrent; N.Parent = mParent;
      if (mParent != -1) {
         // If this has a parent, attach this to the linked list of children
         ref UXNode parent = ref mNodes[mParent];
         if (parent.FirstChild == 0) parent.FirstChild = mCurrent;
         else {
            // If this is not the first child, then there is already a sibling for this,
            // connect up the 'next' pointer of that to this node
            ref UXNode prev = ref mNodes[parent.LastChild];
            prev.Next = mCurrent;
         }
         parent.ChildCount++;
         parent.LastChild = mCurrent;
      }
      return ref mNodes[mCurrent];
   }

   /// <summary>Ends a container</summary>
   public static void EndNode () {
      ref UXNode a = ref mNodes[mCurrent];
      a.DX += (short)(a.Padding.Left + a.Padding.Right);
      a.DY += (short)(a.Padding.Top + a.Padding.Bottom);
      short childGaps = (short)(a.ChildGap * Max (a.ChildCount - 1, 0));
      if (a.Horizontal) a.DX += childGaps; else a.DY += childGaps;
      a.DX = Max (a.DX, a.Width.Min); 
      a.DY = Max (a.DY, a.Height.Min);

      if (a.Parent != -1) {
         ref UXNode par = ref mNodes[a.Parent];
         if (par.Horizontal) { par.DX += a.DX; par.DY = Max (a.DY, par.DY); } 
         else { par.DX = Max (a.DX, par.DX); par.DY += a.DY; }
      } else {
         // This is the root element, it should be of fixed size and position
         a.DX = mScreenSize.X; a.DY = mScreenSize.Y;
      }
      mParent = mCurrent = mStack.Pop ();
   }

   public static void Render () {
   }

   public static void DumpAll () {
      Dump (1, 0);
   }

   static void Dump (int node, int level) {
      ref UXNode a = ref mNodes[node];
      string s = new (' ', level * 2); 
      s += $"{a.Id} {a.Tag} {a.Text} {a.DX}x{a.DY} {a.Parent}";
      Lib.Trace (s);

      foreach (var b in a.EnumChildren (mNodes))
         Dump (b, level + 1);
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
   static int mParent;                    // The parent of the nodes being created
}
