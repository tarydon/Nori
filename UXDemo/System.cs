// ────── ╔╗
// ╔═╦╦═╦╦╬╣ System.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Nori;
namespace UXDemo;

static public class UXSystem {
   // Properties ---------------------------------------------------------------
   public static TypeFace[] Typefaces = [];
   public static NodeClass[] Classes = new NodeClass[8];
   public static NodeMemo[] Memo = new NodeMemo[32];

   public static Vec2S MousePos { get; private set; }

   public static int WheelDelta { get; private set; }       // Mouse wheel movement in this frame

   public static Vec2S ScreenSize { get; private set; }     // Screen size

   public static GroupVN RetainedVN = new ([]);

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
      ScreenSize = screenSize;
      // Note that we are not using mNodes[0], so we start with mUsed = 1
      mUsed = 1; mCurrent = 0; mStack.Clear ();
      return ref BeginNode (EKind.Root, 0, screenSize.X, screenSize.Y);
   }

   public static void SetMouseState (Vec2S position, int wheelDelta, bool pressed) {
      mMousePressedLastFrame = mMousePressed;
      (MousePos, WheelDelta, mMousePressed) = (position, wheelDelta, pressed);
   }

   public static ref Node BeginNode (EKind kind, uint uid, Size width, Size height) {
      ref Node node = ref BeginNode (kind, uid);
      node.X.Set (width); node.Y.Set (height);
      return ref node;
   }

   public static ref Node BeginNode (EKind kind, uint uid) {
      if (mUsed >= Nodes.Length) 
         Array.Resize (ref Nodes, Nodes.Length * 2);
      mStack.Push (mParent = mCurrent); mCurrent = mUsed++;
      Nodes[mCurrent] = new ();    // Reset to zeroes!

      ref Node node = ref Nodes[mCurrent];
      node.Id = mCurrent; node.UId = uid;
      while (Memo.Length <= uid) Array.Resize (ref Memo, Memo.Length * 2);
      Memo[uid].UId = uid;
      if ((node.Parent = mParent) != 0) {
         // If this has a parent, attach this node to the linked list of children
         // of that parent
         ref Node parent = ref Nodes[mParent];
         node.Level = (short)(parent.Level + 1);
         if (parent.FirstChild == 0) parent.FirstChild = node.Id;
         else {
            // If this is not the first child, then there is an earlier sibling for
            // this, connect up that one to this node
            ref Node prev = ref Nodes[parent.LastChild];
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

   public static string Dump () {
      StringBuilder sb = new ();
      Dump (sb, 1, 0);
      return sb.ToString ();
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
         for (short a = Nodes[n].FirstChild; a != 0; a = Nodes[a].Next)
            mQueue.Enqueue (a);
      }

      // 2. Compute the sizes of these nodes (bottom-up order), since the sizes of all 
      // children must be known before we can compute the size of a parent. Also, in this pass,
      // we do the fit-sizing of all nodes in the x direction
      for (int i = mTraverse.Count - 1; i >= 0; i--) {
         ref Node node = ref Nodes[mTraverse[i]];
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
         ref Node node = ref Nodes[n];
         if ((node.Flags & EFlags.Wrap) != 0) Classes[(int)node.Kind].Wrap (ref node);
      }

      // 5. Fit sizing in Y
      for (int i = mTraverse.Count - 1; i >= 0; i--) {
         ref Node node = ref Nodes[mTraverse[i]];
         if (node.Y.Mode is ESizing.Fit or ESizing.Grow) node.DoFitSizing (false);
      }

      // 6. Grow/Shrink sizing in Y
      foreach (var n in mTraverse) 
         Nodes[n].DoGrowShrinkChildren (false);

      // 7. Compute the positions of all the nodes
      foreach (var n in mTraverse) 
         PositionChildren (n);
      File.WriteAllText ("c:/etc/dump.txt", Dump ());

   }

   public static void Render () {
      // Render the nodes in top-down traversal order (we want to draw the
      // parents before children
      foreach (var n in mTraverse) {
         ref Node node = ref Nodes[n];
         var clas = Classes[(int)node.Kind]; clas.Draw (ref node);
         ref NodeMemo memo = ref Memo[node.UId];
         memo.Rect = node.Rect;
      }
   }

   [DoesNotReturn]
   public static void Fatal (string s) {
      throw new Exception (s);
   }

   // Implementation -----------------------------------------------------------
   static void Dump (StringBuilder sb, int n, int level) {
      ref Node node = ref Nodes[n];
      sb.Append (new string (' ', level));
      node.Dump (sb); sb.AppendLine ();
      List<short> tmp = []; node.GetChildren (tmp, EEnum.All);
      foreach (var c in tmp) Dump (sb, c, level + 1);
   }

   static void PositionChildren (int n) {
      ref Node node = ref Nodes[n];
      if (node.GetChildren (mTmp, EEnum.Children)) {
         // First, position along the axis
         bool horizontal = node.IsHorizontal;
         ref AxisDef ax = ref (horizontal ? ref node.X : ref node.Y);
         ref AxisDef ay = ref (horizontal ? ref node.Y : ref node.X);
         int xRemain = node.GetRemainingSpace (mTmp, horizontal);
         int xDelta = ax.ChildAlign switch { EAlign.Middle => xRemain / 2, EAlign.End => xRemain, _ => 0 };
         int xPos = ax.V0 + ax.PadStart + xDelta;

         foreach (var c in mTmp) {
            ref Node child = ref Nodes[c];
            ref AxisDef cax = ref (horizontal ? ref child.X : ref child.Y);
            ref AxisDef cay = ref (horizontal ? ref child.Y : ref child.X);
            cax.V0 = (short)xPos; xPos += cax.DV + node.ChildGap;

            int yRemain = ay.DV - cay.DV - ay.TotalPad;
            int yDelta = ay.ChildAlign switch { EAlign.Middle => yRemain / 2, EAlign.End => yRemain, _ => 0 };
            cay.V0 = (short)(ay.V0 + ay.PadStart + yDelta);
            if (node.Kind == EKind.VScroll) {
               Lib.Check (horizontal);
               ref var memo = ref node.GetMemo ();
               memo.ChildSize = cay.DV;
               if (node.IsMouseOver && WheelDelta != 0) memo.ScrollPos -= WheelDelta * 10;
               memo.MaxScrollPos = Math.Max (-yRemain, 0);
               memo.ScrollPos = memo.ScrollPos.Clamp (0, memo.MaxScrollPos);
               cay.V0 = (short)(cay.V0 - memo.ScrollPos);
            }
         }
      }

      if (node.GetChildren (mTmp, EEnum.Popups)) {
         foreach (var c in mTmp) {
            ref Node popup = ref Nodes[c];
            ref Node owner = ref (popup.IsScreenRelative ? ref Nodes[1] : ref node);
            Vec2S parentPos = owner.GetCorner (popup.ParentCorner) + popup.FloatOffset;
            Vec2S childPos = popup.GetCorner (popup.ElemCorner);
            popup.X.V0 = (short)(parentPos.X - childPos.X);
            popup.Y.V0 = (short)(parentPos.Y - childPos.Y);
         }
      }
   }

   // Private data -------------------------------------------------------------
   static short mUsed;           // Number of used nodes
   static short mCurrent;        // Node that is currently being edited
   static short mParent;         // Parent for the current node
   static Stack<short> mStack = [];   // Stack of all nodes
   static bool mMousePressed;         // Is the mouse pressed in this frame
   static bool mMousePressedLastFrame;       // and in the last frame?
   internal static Node[] Nodes = new Node[32];      // List of nodes

   static List<short> mTraverse = [];     // Top-down traversal of nodes, breadth first
   static Queue<short> mQueue = [];       // Queue used to compute mTraverse
   static List<short> mTmp = [];
}
