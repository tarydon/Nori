// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Engine.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static Nori.UXNode;
namespace Nori;

public static class UXEngine {
   // Properties ---------------------------------------------------------------
   /// <summary>The current mouse position, in pixels (top left = 0,0)</summary>
   public static Vec2S MousePos { get; private set; }

   /// <summary>Group containing retained drawing for UI</summary>
   /// Some UI nodes use retained mode rendering (for example, a node that might
   /// render a thumbnail from a drawing or model). The VNodes created for such
   /// rendering get added as children of this global group. As those UI nodes
   /// get out of scope, these VNodes are removed from the RetainedVN
   public static VNode Retained => mRetained;
   static readonly GroupVN mRetained = new ([]);

   /// <summary>The screen size</summary>
   public static Vec2S ScreenSize { get; private set; }

   /// <summary>The set of typefaces used for rendering</summary>
   /// The NFace index within each UX.Node points to an entry from 
   /// this array
   public static TypeFace[] Typefaces = [];

   /// <summary>Mouse wheel rotation during this frame</summary>
   public static int WheelDelta { get; private set; }

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a 'retained mode' VNode to the visual graph</summary>
   /// See documentation for Retained property above
   public static void AddRetained (VNode node) {
      Debug.Assert (!mDeleteQueue.Contains (node));
      mRetained.Add (node);
   }

   /// <summary>Begins a new Inlay layout - each Draw pass should start with this</summary>
   /// This returns a Node that covers the entire screen and is the root node for 
   /// the layout
   public static ref UXNode BeginLayout (Vec2S screenSize) {
      ScreenSize = screenSize;
      // Note that we are not using mNodes[0], so we start with mUsed = 1
      mUsed = 1; mParent = 0; mCurrent = 0; mStack.Clear ();
      return ref BeginNode (EKind.Root, 0, screenSize.X, screenSize.Y);
   }

   /// <summary>Begins a node of a given kind, with a specified size</summary>
   public static ref UXNode BeginNode (EKind kind, uint uid, Size width, Size height) {
      ref UXNode node = ref BeginNode (kind, uid);
      node.X.Set (width); node.Y.Set (height);
      return ref node;
   }

   public static ref UXNode BeginNode (EKind kind, uint idMemo) {
      if (mUsed >= Nodes.Length)
         Array.Resize (ref Nodes, Nodes.Length * 2);
      mStack.Push (mParent = mCurrent); mCurrent = mUsed++;
      Nodes[mCurrent] = new ();    // Reset to zeroes!

      ref UXNode node = ref Nodes[mCurrent];
      node.Index = mCurrent; node.UId = idMemo;
      while (Memo.Length <= idMemo) Array.Resize (ref Memo, Memo.Length * 2);
      Memo[idMemo].UId = idMemo;
      if ((node.Parent = mParent) != 0) {
         // If this has a parent, attach this node to the linked list of children
         // of that parent
         ref UXNode parent = ref Nodes[mParent];
         node.Level = (short)(parent.Level + 1);
         if (parent.FirstChild == 0) parent.FirstChild = node.Index;
         else {
            // If this is not the first child, then there is an earlier sibling for
            // this, connect up that one to this node
            ref UXNode prev = ref Nodes[parent.LastChild];
            prev.Next = mCurrent;
         }
         parent.ChildCount++;
         parent.LastChild = mCurrent;
      }
      var clas = Classes[(int)kind];
      if (clas == null) Fatal ($"No class registered for EKind.{kind}");
      clas.Init (ref node); 
      return ref node;
   }

   /// <summary>Matching call for BeginNode</summary>
   /// Unlike in the Clay renderer, we do not at this point immediately compute anything,
   /// we defer since some of this node's properties (that affect layout) may still be set by 
   /// calls after EndNode. Instead we defer this to EndLayout (with no loss of generality or
   /// performance)
   public static void EndNode () {
      mParent = mCurrent = mStack.Pop ();
   }

   /// <summary>Called once each frame at the end of the layout process</summary>
   /// This is wha actually computes the layout (sizes and positions) using the 7 stage
   /// process as explained in the Youtube video 'How Clay's UI Layout Algorithm Works'
   public static void EndLayout () {
      EndNode (); // End the 'ROOT' node that BeginLayout created 
      Lib.Check (mStack.Count == 0, "Unmatched UXSystem.BeginNode()");

      // 1. Compute the top-down traversal order of the nodes
      mQueue.Enqueue (1); mTraverse.Clear ();
      while (mQueue.TryDequeue (out short n)) {
         mTraverse.Add (n);
         for (short a = Nodes[n].FirstChild; a != 0; a = Nodes[a].Next)
            mQueue.Enqueue (a);
      }

      // 2. Compute the sizes of these nodes (bottom-up order), since the sizes of all 
      // children must be known before we can compute the size of a parent. Also, in this pass,
      // we do the fit-sizing of all nodes in the x direction
      for (int i = mTraverse.Count - 1; i >= 0; i--) {
         ref UXNode node = ref Nodes[mTraverse[i]];
         Classes[(int)node.Kind].Measure (ref node);
         if (node.X.Max == 0) node.X.Max = short.MaxValue;
         if (node.Y.Max == 0) node.Y.Max = short.MaxValue;
         if (node.X.Mode is ESizing.Fit or ESizing.Grow) node.DoFitSizing (true);
      }

      // 3. Grow/Shrink sizing in X
      foreach (var n in mTraverse)
         Nodes[n].DoGrowShrinkChildren (true);

      // 4. Wrap text
      foreach (var n in mTraverse) {
         ref UXNode node = ref Nodes[n];
         if ((node.Flags & EFlags.Wrap) != 0) Classes[(int)node.Kind].Wrap (ref node);
      }

      // 5. Fit sizing in Y
      for (int i = mTraverse.Count - 1; i >= 0; i--) {
         ref UXNode node = ref Nodes[mTraverse[i]];
         if (node.Y.Mode is ESizing.Fit or ESizing.Grow) node.DoFitSizing (false);
      }

      // 6. Grow/Shrink sizing in Y
      foreach (var n in mTraverse)
         Nodes[n].DoGrowShrinkChildren (false);

      // 7. Compute the positions of all the nodes
      foreach (var n in mTraverse)
         PositionChildren (n);
   }

   public static void Init () {
      if (!sInited) {
         sInited = true;
         Lux.OnFrameEnd.Subscribe (EndOfFrame);
         UXClass.RegisterAll ();
      }
   }
   static bool sInited;

   /// <summary>Register a UX node class</summary>
   /// Since UXNode are all structs, we want to move all the implementation of Draw code 
   /// to these 'classes' to keep those structs lightweight and simple. The UXNode themselves
   /// are regenerated on each frame, while any persistent data they need for drawing/measuring etc
   /// are stored in UXMemo that are maintained for longer periods (and indexed using Node.UID)
   public static void RegisterClass (UXClass clas) {
      int n = (int)clas.Kind;
      while (Classes.Length <= n) Array.Resize (ref Classes, Classes.Length * 2);
      if (Classes[n] != null) Fatal ($"UX.Node {clas.Kind} already registered");
      Classes[n] = clas;
   }

   /// <summary>Called at the end of the layout cycle to render the nodes</summary>
   public static void Render () {
      // Render the nodes in top-down traversal order (we want to draw the
      // parents before children
      foreach (var n in mTraverse) {
         ref UXNode node = ref Nodes[n];
         var clas = Classes[(int)node.Kind]; clas.Draw (ref node);
         ref Memo memo = ref Memo[node.UId];
         memo.Rect = node.Rect;
      }
   }

   /// <summary>Called at the start of each frame to set up the mouse position, wheel-delta and button state</summary>
   public static void SetMouseState (Vec2S position, int wheelDelta, bool pressed) {
      MousePressedLastFrame = MousePressed;
      (MousePos, WheelDelta, MousePressed) = (position, wheelDelta, pressed);
   }

   /// <summary>Removes a node from the Retained VNode group</summary>
   /// Actually, this just queues it up for delete at the end of the frame, since
   /// we don't want to actually disturb the VNode tree halfway through a frame 
   /// render
   public static void RemoveRetained (VNode node) {
      Debug.Assert (mDeleteQueue.Contains (node));
      mDeleteQueue.Remove (node);
   }
   static readonly List<VNode> mDeleteQueue = [];

   // Implementation -----------------------------------------------------------
   // Helper used to delayed-delete retained VNodes that go out of visibility during this
   // frame (for example, scrolled out)
   static void EndOfFrame (Unit _) {
      mDeleteQueue.ForEach (mRetained.Remove);
      mDeleteQueue.Clear ();
   }

   [DoesNotReturn]
   static void Fatal (string s) {
      throw new Exception (s);
   }

   static void PositionChildren (int n) {
      ref UXNode node = ref Nodes[n];
      if (node.GetChildren (mTmp, EEnum.Inlaid)) {
         // This positions the inlaid children (that occupy space within this element).
         // In this code below 'ax' stands for the ALONG axis, and 'ay' for the ACROSS axis
         // of this node. Likewise 'cax' and 'cay' for the child nodes. Read this code as though
         // we are dealing with a horizontal element and the same logic then works for a vertical
         // layout element as well
         bool horizontal = node.IsHorizontal;
         ref Axis ax = ref (horizontal ? ref node.X : ref node.Y);
         ref Axis ay = ref (horizontal ? ref node.Y : ref node.X);
         int xRemain = node.GetRemainingSpace (mTmp, horizontal);
         int xDelta = ax.ChildAlign switch { EAlign.Middle => xRemain / 2, EAlign.End => xRemain, _ => 0 };
         int xPos = ax.V0 + ax.PadStart + xDelta;

         foreach (var c in mTmp) {
            // In the ALONG direction, we position the children one after the other, starting with the 
            // initial padding, placing each child, and allowing the ChildGap after that. In the ACROSS
            // direction, each child is positioned independently depending on the alignment in that 
            // direction (start/middle/end => Left/Center/Right for a horizontal element)
            ref UXNode child = ref Nodes[c];
            ref Axis cax = ref (horizontal ? ref child.X : ref child.Y);
            ref Axis cay = ref (horizontal ? ref child.Y : ref child.X);
            cax.V0 = (short)xPos; xPos += cax.DV + node.ChildGap;

            int yRemain = ay.DV - cay.DV - ay.TotalPad;
            int yDelta = ay.ChildAlign switch { EAlign.Middle => yRemain / 2, EAlign.End => yRemain, _ => 0 };
            cay.V0 = (short)(ay.V0 + ay.PadStart + yDelta);
            if (node.Kind == EKind.VScroll) {
               // Scroll panels are handled specially - all the children are displaced in the scroll
               // direction by the negative of the current scroll amount. In general, only some subset
               // of children will be visible
               Lib.Check (horizontal);
               ref var memo = ref node.GetMemo ();
               memo.ChildSize = cay.DV;
               if (node.IsMouseOver && WheelDelta != 0) memo.ScrollPos -= WheelDelta * 100;
               memo.MaxScrollPos = Math.Max (-yRemain, 0);
               memo.ScrollPos = memo.ScrollPos.Clamp (0, memo.MaxScrollPos);
               cay.V0 = (short)(cay.V0 - memo.ScrollPos);
            }
         }
      }

      if (node.GetChildren (mTmp, EEnum.Popups)) {
         // Here, we position the popup children of this element - these don't take up any space
         // within this element and are positioned with one of their corners relative to one of 
         // the corners of this parent (each is positioned independently). 
         foreach (var c in mTmp) {
            ref UXNode popup = ref Nodes[c];
            ref UXNode owner = ref (popup.IsScreenRelative ? ref Nodes[1] : ref node);
            Vec2S parentPos = owner.GetCorner (popup.ParentCorner) + popup.FloatOffset;
            Vec2S childPos = popup.GetCorner (popup.ElemCorner);
            popup.X.V0 = (short)(parentPos.X - childPos.X);
            popup.Y.V0 = (short)(parentPos.Y - childPos.Y);
         }
      }
   }

   // Private data -------------------------------------------------------------
   static short mUsed;                    // Number of used nodes
   static short mCurrent;                 // Node that is currently being edited
   static short mParent;                  // Parent for the current node
   static Stack<short> mStack = [];       // Stack of all nodes

   static internal bool MousePressed;           // Is the mouse currently pressed?
   static internal bool MousePressedLastFrame;  // Was the mouse pressed on the last frame?

   internal static UXNode[] Nodes = new UXNode[32];      // List of nodes
   internal static Memo[] Memo = new Memo[32];
   internal static UXClass[] Classes = new UXClass[8];

   static List<short> mTraverse = [];     // Top-down traversal of nodes, breadth first
   static Queue<short> mQueue = [];       // Queue used to compute mTraverse
   static List<short> mTmp = [];
}
