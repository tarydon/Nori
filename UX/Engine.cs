// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Engine.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using Nori.UX;
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

   /// <summary>
   /// Begins a node of a given kind, with a specified size
   /// </summary>
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
      node.Index = mCurrent; node.UID = idMemo;
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


   public static void RegisterClass (UXClass nc) {
      throw new NotImplementedException ();
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
   static void EndOfFrame (Unit _) {
      mDeleteQueue.ForEach (mRetained.Remove);
      mDeleteQueue.Clear ();
   }

   // Private data -------------------------------------------------------------
   static short mUsed;                    // Number of used nodes
   static short mCurrent;                 // Node that is currently being edited
   static short mParent;                  // Parent for the current node
   static Stack<short> mStack = [];       // Stack of all nodes

   internal static UXNode[] Nodes = new UXNode[32];      // List of nodes
   internal static NodeMemo[] Memo = new NodeMemo[32];
   internal static UXClass[] 

   static List<short> mTraverse = [];     // Top-down traversal of nodes, breadth first
   static Queue<short> mQueue = [];       // Queue used to compute mTraverse
   static List<short> mTmp = [];
}
