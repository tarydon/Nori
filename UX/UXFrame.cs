// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXFrame.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori.UX;

public static partial class UXFrame {
   // Properties ---------------------------------------------------------------
   /// <summary>The global list of UXNode</summary>
   public static UXNode[] All => mNodes;

   /// <summary>List of nodes</summary>
   public static UXNode[] Prev => mSnapshot;

   /// <summary>Reference to the current UX node</summary>
   public static ref UXNode N => ref mNodes[mCurrent];

   /// <summary>The global list of all typefaces (.FontID is an index into this)</summary>
   public static TypeFace[] TypeFaces = [];

   public static Scene[] Scenes = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Begins a new layout pass given the available screen size</summary>
   public static void BeginLayout (Vec2S size) {
      mScreenSize = size;
      mUsed = 1; mCurrent = -1; 
   }

   /// <summary>Sets the current state of the mouse (used to implement hover/clicks/scroll)</summary>
   /// <param name="position">Mouse position in pixels, relative to top left</param>
   /// <param name="wheelDelta">Mouse wheel rotation since last frame</param>
   /// <param name="isPressed">Is the mouse button pressed?</param>
   public static void SetMouseState (Vec2S position, int wheelDelta, bool isPressed) {
      mMousePressedLastFrame = mMousePressed;
      (mMousePos, mWheelDelta, mMousePressed) = (position, wheelDelta, isPressed);
   }

   /// <summary>Ends the layout and generates render commands</summary>
   public static void EndLayout () {
      Lib.Check (mStack.Count == 0, "Unmatched Begin() in UXFrame");
      // At this point, the desired sizes of all the elements are already computed
      // (by the EndNode methods as element is closed)
      GrowChildElements (1); 
      PositionChildren (1);
   }
   
   // Helpers ------------------------------------------------------------------
   /// <summary>Begins a new container</summary>
   public static ref UXNode BeginNode () {
      if (mUsed >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mParent = mCurrent; mStack.Push (mCurrent); mCurrent = mUsed; mUsed++;
      mNodes[mCurrent] = new ();
      N.Id = mCurrent; N.Parent = mParent;
      if (mParent != -1) {
         // If this has a parent, attach this to the linked list of children
         ref UXNode parent = ref mNodes[mParent];
         N.Level = parent.Level + 1;
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
         if (!a.Floating) {
            ref UXNode par = ref mNodes[a.Parent];
            if (par.Horizontal) { par.DX += a.DX; par.DY = Max (a.DY, par.DY); } 
            else { par.DX = Max (a.DX, par.DX); par.DY += a.DY; }
         }
      } else {
         // This is the root element, it should be of fixed size and position
         a.DX = mScreenSize.X; a.DY = mScreenSize.Y;
      }
      mParent = mCurrent = mStack.Pop ();
   }

   public static void Render (bool realRender) {
      if (realRender) {
         for (int i = 1; i < mUsed; i++) {
            ref UXNode node = ref mNodes[i];
            Lux.ZLevel = node.Level + 100;
            if (node.Kind == UXNode.EKind.SceneHolder) {
               Scene? sc = Scenes.SafeGet (node.Tag!.ToInt ());
               if (sc != null) {
                  double dx = mScreenSize.X, dy = mScreenSize.Y;
                  var rect = node.Rect;
                  double left = rect.Left / dx + 0.01, top = 1 - rect.Top / dy - 0.01, right = rect.Right / dx, bottom = 1 - rect.Bottom / dy;
                  Lux.AddSubScene (sc, new (left, top, right, bottom));
               }
            }
            if (!node.BgrdColor.IsTransparent) {
               Lux.Color = node.BgrdColor;
               var rect = node.Rect;
               bool border = !node.Border.IsZero, radius = node.CornerRadius > 0;
               switch (border, radius) {
                  case (false, false): Lux.Rect (rect); break;
                  case (false, true): Lux.RRect (rect, node.CornerRadius); break;
                  case (true, false): Lux.BorderColor = node.BorderColor; Lux.RectBorder (rect, node.Border.Left); break;
                  case (true, true):
                     if (node.Kind == UXNode.EKind.Dialog) {
                        Lux.UIRect (rect.Center, rect.Size, node.CornerRadius, node.Border.Left, node.BgrdColor, node.BorderColor);
                     } else {
                        Lux.BorderColor = node.BorderColor; 
                        Lux.RRectBorder (rect, node.CornerRadius, node.Border.Left);
                     }
                     break;
               }
            }
            if (node.Text != null) {
               Lux.Color = node.TextColor;
               Lux.TypeFace = TypeFaces[node.FontId];
               Lux.Text (node.Text, new (node.X + node.TextOffset.X, node.Y + node.TextOffset.Y));
            }
         }
      }
      (mNodes, mSnapshot) = (mSnapshot, mNodes);
   }

   internal static bool IsHovered (int n)
      => IsHovered (n, 0);

   public static bool IsHovered (int n, int inflate) {
      if (n >= mSnapshot.Length) return false;
      return mSnapshot[n].Rect.Inflated (inflate).Contains (mMousePos);
   }

   internal static bool IsPressed (int n)
      => mMousePressed && IsHovered (n);

   internal static bool AnyPopupsOpen (int n)
      => n < mSnapshot.Length && AnyPopupsOpen (ref mSnapshot[n]);

   internal static bool IsReleased (int n)
      => mMousePressedLastFrame && !mMousePressed && IsHovered (n);

   static bool AnyPopupsOpen (ref UXNode node) {
      for (int c = node.FirstChild; c != 0; c = mSnapshot[c].Next) {
         ref UXNode child = ref mSnapshot[c];
         if (child.Floating && child.Rect.Contains (mMousePos)) return true;
         if (AnyPopupsOpen (ref child)) return true; 
      }
      return false;
   }

   // Private data -------------------------------------------------------------
   static Vec2S mScreenSize;              // Screen size
   static Vec2S mMousePos;                // Mouse position, relative to top left
   static int mWheelDelta;                // Wheel rotation since last frame
   static bool mMousePressed;             // Is the mouse currently pressed
   static bool mMousePressedLastFrame;    // Was the mouse pressed in the last frame?
   static UXNode[] mNodes = new UXNode[32];     // List of nodes 
   static UXNode[] mSnapshot = new UXNode[32]; // List of 'previous' nodes
   static Stack<int> mStack = [];         // Stack of currently open nodes
   static int mUsed;                      // Number of used nodes
   static int mCurrent;                   // Node that is currently being edited
   static int mParent;                    // The parent of the nodes being created
}
