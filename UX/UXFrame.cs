// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXFrame.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
using static Nori.UX.UXNode.ESizeMode;
using static Nori.UX.UXNode.EChildAlignX;
using static Nori.UX.UXNode.EChildAlignY;
namespace Nori.UX;

public static class UXFrame {
   /// <summary>Reference to the current UX node</summary>
   public static ref UXNode N => ref mNodes[mCurrent];

   public static UXNode[] All => mNodes;

   public static ref UXNode Get (int n) => ref mNodes[n];

   public static TypeFace[] TypeFaces = [];

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
      PositionChildren (1);
   }

   static void PositionChildren (int p) {
      ref UXNode parent = ref mNodes[p];
      var (dxSpace, dySpace) = GetRemainingSpace (p);
      parent.GetChildren (mNodes, mTmp);
      if (parent.Horizontal) {
         int left = parent.Padding.Left;
         left += parent.ChildAlignX switch { Center => dxSpace / 2, Right => dxSpace, _ => 0 };
         foreach (var c in mTmp) {
            ref UXNode child = ref mNodes[c];
            child.X = (parent.X + left);
            left += (child.DX + parent.ChildGap);
            child.Y = (parent.Y + parent.Padding.Top);
            switch (parent.ChildAlignY) {
               case Middle: child.Y += ((dySpace - child.DY) / 2); break;
               case Bottom: child.Y += (dySpace - child.DY); break;
            }
         }
      } else {
         int top = parent.Padding.Top;
         top += parent.ChildAlignY switch { Middle => dySpace / 2, Bottom => dySpace, _ => 0 };
         foreach (var c in mTmp) {
            ref UXNode child = ref mNodes[c];
            child.Y = (parent.Y + top);
            top += (child.DY + parent.ChildGap);
            child.X = (parent.X + parent.Padding.Left);
            switch (parent.ChildAlignX) {
               case Center: child.X += ((dxSpace - child.DX) / 2); break;
               case Right: child.X += (dxSpace - child.DX); break;
            }
         }
      }

      for (int c = parent.FirstChild; c != 0; c = mNodes[c].Next)
         PositionChildren (c);
   }

   // Given a parent node, returns the amount of extra space left after accounting for
   // the padding. Along the layout direction, we also subtract the sizes of the children
   // and the gaps between children. 
   static (int, int) GetRemainingSpace (int p) {
      ref UXNode parent = ref mNodes[p];
      parent.GetChildren (mNodes, mTmp);
      int childGaps = parent.ChildGap * Max (parent.ChildCount - 1, 0);
      int dxSpace = parent.DX - parent.Padding.Horizontal;
      int dySpace = parent.DY - parent.Padding.Vertical;
      if (parent.Horizontal) {
         dxSpace -= childGaps;
         foreach (var c in mTmp) dxSpace -= mNodes[c].DX;
      } else {
         dySpace -= childGaps;
         foreach (var c in mTmp) dySpace -= mNodes[c].DY;
      }
      return (dxSpace, dySpace);
   }
   static List<int> mTmp = [];

   static void GrowChildElements (int p) {
      ref UXNode par = ref mNodes[p];
      var children = par.GetChildren (mNodes);
      int childGaps = (par.ChildGap * Max (par.ChildCount - 1, 0));
      int dxSpace = par.DX - par.Padding.Left - par.Padding.Right;
      int dySpace = par.DY - par.Padding.Top - par.Padding.Bottom;
      if (par.Horizontal) {
         dxSpace -= childGaps;
         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[children[i]];
            dxSpace -= child.DX;
            if (child.Height.Mode == Grow) child.DY += (dySpace - child.DY);
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
               child.DX += widthToAdd;
               dxSpace -= widthToAdd; if (dxSpace <= 0) break;
            }
         }
      } else {
         dySpace -= childGaps;
         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[children[i]];
            dySpace -= child.DY;
            if (child.Width.Mode == Grow) child.DX += (dxSpace - child.DX);
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
               child.DY += heightToAdd;
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
      if (mUsed >= mNodes.Length) {
         Array.Resize (ref mNodes, mNodes.Length * 2);
         Array.Resize (ref mSizes, mSizes.Length * 2);
      }
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
      if (a.Text != null) {
         TypeFace tf = TypeFaces[a.FontId];
         RectS r = tf.Measure (a.Text);
         a.DX = r.Width; a.DY = r.Height;
         a.TextOffset = new (-r.Left, -r.Top);
      }
      a.DX += (a.Padding.Left + a.Padding.Right);
      a.DY += (a.Padding.Top + a.Padding.Bottom);
      int childGaps = (a.ChildGap * Max (a.ChildCount - 1, 0));
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
      for (int i = 1; i < mUsed; i++) {
         ref UXNode node = ref mNodes[i];
         RectS rect = new (node.X, node.Y, node.X + node.DX, node.Y + node.DY);
         mSizes[i] = rect;
         Lux.ZLevel = i;
         if (!node.BgrdColor.IsTransparent) {
            Lux.Color = node.BgrdColor;
            bool border = !node.Border.IsZero, radius = node.CornerRadius > 0;
            switch (border, radius) {
               case (false, false): Lux.Rect (rect); break;
               case (false, true): Lux.RRect (rect, node.CornerRadius); break;
               case (true, false): Lux.BorderColor = node.BorderColor; Lux.RectBorder (rect, node.Border.Left); break;
               case (true, true): Lux.BorderColor = node.BorderColor; Lux.RRectBorder (rect, node.CornerRadius, node.Border.Left); break;
            }
         }
         if (node.Text != null) {
            Lux.Color = node.TextColor;
            Lux.TypeFace = TypeFaces[node.FontId];
            Lux.Text (node.Text, new (node.X + node.TextOffset.X, node.Y + node.TextOffset.Y));
         }
      }
   }

   public static void DumpAll () {
      Dump (1, 0);
   }

   static void Dump (int node, int level) {
      ref UXNode a = ref mNodes[node];
      string s = new (' ', level * 2); 
      s += $"{a.Id} {a.Tag} {a.Text} {a.DX}x{a.DY} @ {a.X},{a.Y}";
      Lib.Trace (s);

      foreach (var b in a.EnumChildren (mNodes))
         Dump (b, level + 1);
   }

   internal static bool IsHovered (int n)
      => n < mSizes.Length && mSizes[n].Contains (mMousePos);

   internal static bool IsPressed (int n)
      => mMousePressed && IsHovered (n);

   // Private data -------------------------------------------------------------
   static Vec2S mScreenSize;              // Screen size
   static Vec2S mMousePos;                // Mouse position, relative to top left
   static int mWheelDelta;                // Wheel rotation since last frame
   static bool mMousePressed;             // Is the mouse currently pressed
   static UXNode[] mNodes = new UXNode[32];     // List of nodes 
   static RectS[] mSizes = new RectS[32];       // And their screen rects after layout
   static Stack<int> mStack = [];         // Stack of currently open nodes
   static int mUsed;                      // Number of used nodes
   static int mCurrent;                   // Node that is currently being edited
   static int mParent;                    // The parent of the nodes being created
}
