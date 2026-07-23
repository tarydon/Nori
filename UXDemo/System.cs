// ────── ╔╗
// ╔═╦╦═╦╦╬╣ System.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nori;
namespace UXDemo;

static public class UXSystem {
   // Properties ---------------------------------------------------------------
   public static TypeFace[] Typefaces = [];
   public static NodeClass[] Classes = new NodeClass[8];

   // Methods ------------------------------------------------------------------
   public static void Register (NodeClass clas) {
      int n = (int)clas.Kind;
      while (Classes.Length <= n) Array.Resize (ref Classes, Classes.Length * 2);
      if (Classes[n] != null) Fatal ($"UX.Node {clas.Kind} already registered");
      Classes[n] = clas;
   }

   /// <summary>Begins a new Inlay layout - each Draw pass should start with this</summary>
   /// This returns a Node that covers the entire screen and is the root node for 
   /// the layout
   public static ref Node BeginLayout (Vec2S screenSize) {
      mScreenSize = screenSize;
      // Note that we are not using mNodes[0], so we start with mUsed = 1
      mUsed = 1; mCurrent = 0; mStack.Clear ();
      return ref BeginNode (EKind.Root, screenSize.X, screenSize.Y);
   }

   public static void SetMouseState (Vec2S position, int wheelDelta, bool pressed) {
      mMousePressedLastFrame = mMousePressed;
      (mMousePos, mWheelDelta, mMousePressed) = (position, wheelDelta, pressed);
   }

   public static ref Node BeginNode (EKind kind, Size width, Size height) {
      ref Node node = ref BeginNode (kind);
      node.X.Set (width); node.Y.Set (height);
      return ref node;
   }

   public static ref Node BeginNode (EKind kind) {
      if (mUsed >= mNodes.Length) {
         Array.Resize (ref mNodes, mNodes.Length * 2);
         Array.Resize (ref mSnapshot, mSnapshot.Length * 2);
      }
      mStack.Push (mParent = mCurrent); mCurrent = mUsed++;
      mNodes[mCurrent] = new ();    // Reset to zeroes!

      ref Node node = ref mNodes[mCurrent];
      node.Id = mCurrent;
      if ((node.Parent = mParent) != 0) {
         // If this has a parent, attach this node to the linked list of children
         // of that parent
         ref Node parent = ref mNodes[mParent];
         node.Level = (short)(parent.Level + 1);
         if (parent.FirstChild == 0) parent.FirstChild = node.Id;
         else {
            // If this is not the first child, then there is an earlier sibling for
            // this, connect up that one to this node
            ref Node prev = ref mNodes[parent.LastChild];
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

   public static void EndNode () {
      mParent = mCurrent = mStack.Pop ();
   }

   public static void EndLayout () {
      EndNode (); // End the 'ROOT' node that BeginLayout created 
      Lib.Check (mStack.Count == 0, "Unmatched UXSystem.BeginNode()");

      // 1. Compute the top-down traversal order of the nodes
      mQueue.Enqueue (1); mTraverse.Clear ();
      while (mQueue.TryDequeue (out short n)) {
         mTraverse.Add (n);
         for (short a = mNodes[n].FirstChild; a != 0; a = mNodes[a].Next)
            mQueue.Enqueue (a);
      }

      // 2. Compute the sizes of these nodes (bottom-up order), since the sizes of all 
      // children must be known before we can compute the size of a parent
      for (int i = mTraverse.Count - 1; i >= 0; i--) {
         ref Node node = ref mNodes[mTraverse[i]];
         Classes[(int)node.Kind].Measure (ref node);
      }

      // 3. Compute the positions of all the nodes
      foreach (var n in mTraverse) PositionChildren (n);
   }

   public static void Render (bool realRender) {
      if (realRender) {
         // Render the nodes in top-down traversal order (we want to draw the
         // parents before children
         foreach (var n in mTraverse) {
            ref Node node = ref mNodes[n];
            var clas = Classes[(int)node.Kind]; clas.Draw (ref node);
         }
      }
      (mNodes, mSnapshot) = (mSnapshot, mNodes);
   }

   [DoesNotReturn]
   public static void Fatal (string s) {
      throw new Exception (s);
   }

   // Implementation -----------------------------------------------------------
   static void PositionChildren (int n) {
      ref Node node = ref mNodes[n];
      if (!node.GetChildren (mTmp)) return;

      // First, position along the axis
      bool horizontal = node.IsHorizontal;
      ref AxisDef ax = ref (horizontal ? ref node.X : ref node.Y);
      ref AxisDef ay = ref (horizontal ? ref node.Y : ref node.X);
      int xpos = ax.V0 + ax.PadStart;
      foreach (var c in mTmp) {
         ref Node child = ref mNodes[c];
         ref AxisDef cax = ref (horizontal ? ref child.X : ref child.Y);
         ref AxisDef cay = ref (horizontal ? ref child.Y : ref child.X);
         cax.V0 = (short)xpos; xpos += cax.DV + node.ChildGap;

         int yRemain = ay.DV - cay.DV - ay.TotalPad;
         int delta = ay.ChildAlign switch { EAlign.Middle => yRemain / 2, EAlign.End => yRemain, _ => 0 };
         cay.V0 = (short)(ay.V0 + ay.PadStart + delta);
      }
   }

   // Private data -------------------------------------------------------------
   static short mUsed;           // Number of used nodes
   static short mCurrent;        // Node that is currently being edited
   static short mParent;         // Parent for the current node
   static Node[] mSnapshot = new Node[32];   // Snapshot (previous frame)
   static Stack<short> mStack = [];   // Stack of all nodes
   static bool mMousePressed;         // Is the mouse pressed in this frame
   static bool mMousePressedLastFrame;       // and in the last frame?
   static int mWheelDelta;       // Mouse wheel movement in this frame
   static Vec2S mMousePos;       // Mouse position this frame
   internal static Node[] mNodes = new Node[32];      // List of nodes
   internal static Vec2S mScreenSize;     // Screen size

   static List<short> mTraverse = [];     // Top-down traversal of nodes, breadth first
   static Queue<short> mQueue = [];       // Queue used to compute mTraverse
   static List<short> mTmp = [];
}
