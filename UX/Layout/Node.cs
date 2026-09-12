// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Node.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Dynamic;

namespace Nori;

public partial struct UXNode {
   // Fields -------------------------------------------------------------------
   // Core .......................................
   /// <summary>Index of the node in UXEngine.Nodes[] (this is transient and can change each frame)</summary>
   public short Index;
   /// <summary>What kind of node is this?</summary>
   public EKind Kind;
   /// <summary>Flags bits for this node</summary>
   public EFlags Flags;
   /// <summary>The persistent UID for this node, used to index into Memo[]</summary>
   public uint UId;
   /// <summary>Tag (usually used for debugging)</summary>
   public string? Tag;
   /// <summary>
   /// Is this node disabled?
   /// </summary>
   public bool Disabled;

   // Node tree ..................................
   /// <summary>Parent node for this (0 for root node)</summary>
   public short Parent;
   /// <summary>Fist child node (0 means no children)</summary>
   public short FirstChild;
   /// <summary>Last child node (if same as FirstChild, there is only one child)</summary>
   public short LastChild;
   /// <summary>Count of children for this node</summary>
   public short ChildCount;
   /// <summary>Level of this node in the hierarchy (root node = 0)</summary>
   public int Level;
   /// <summary>Pointer to next sibling (used for tree-traversal)</summary>
   public short Next;

   // Metrics ....................................
   /// <summary>Metrics for X axis (width direction)</summary>
   public Axis X;
   /// <summary>Metrics for Y axis (height direction)</summary>
   public Axis Y;
   /// <summary>Border width</summary>
   public short BorderWidth;
   /// <summary>Corner radius (Fillets tells us which corners are rounded)</summary>
   public short CornerRadius;
   /// <summary>Gap between successive children</summary>
   public short ChildGap;

   // Colors .....................................
   /// <summary>Background color</summary>
   public Color4 BgrdColor;
   /// <summary>Border color, if there is a border</summary>
   public Color4 BorderColor;
   /// <summary>Foreground color (text / drawings etc)</summary>
   public Color4 FgrdColor;

   // Text .......................................
   /// <summary>Text to use</summary>
   public string? Text;
   /// <summary>Font ID (index into UXEngine.Fonts)</summary>
   public short FontId;
   /// <summary>TextOffset (position of Lux.Text call, relative to top-left-corner of node)</summary>
   public Vec2S TextOffset;

   // Floating elements ..........................
   /// <summary>Position on tihs element that is used for alignment</summary>
   public ECorner ElemCorner;
   /// <summary>Position on the parent that is used for alignment</summary>
   public ECorner ParentCorner;
   /// <summary>Offset between the two</summary>
   public Vec2S FloatOffset;

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Does this node have any popups open?
   /// </summary>
   public readonly bool AnyPopupsOpen => GetMemo ().AnyPopupsOpen;

   /// <summary>Is the 'shadow' bit turned on for this node?</summary>
   public readonly bool HasShadow => Get (EFlags.Shadow);

   /// <summary>Is this node having 'horizontal' layout</summary>
   public bool IsHorizontal { readonly get => Get (EFlags.Horizontal); set => Set (EFlags.Horizontal, value); }
   /// <summary>Is the mouse over this element (computed from last frame position)</summary>
   public readonly bool IsMouseOver => GetMemo ().IsMouseOver;
   /// <summary>Is the 'popup' bit turned on for this node?</summary>
   public readonly bool IsPopup => Get (EFlags.Popup);
   /// <summary>Is this POPUP aligned relative to the screen</summary>
   public bool IsScreenRelative { readonly get => Get (EFlags.ScreenRelative); set => Set (EFlags.ScreenRelative, value); }
   /// <summary>
   /// Might this node have popups (not necssarily right now, but ever)
   /// </summary>
   public bool MayHavePopups => Get (EFlags.MayHavePopups);

   /// <summary>The final Rect occupied by this node (in pixel space)</summary>
   public readonly RectS Rect => new (X.V0, Y.V0, X.V0 + X.DV, Y.V0 + Y.DV);

   /// <summary>Lux rendering level for this node</summary>
   public readonly int ZLevel => 100 + Level * 2;

   // Methods ------------------------------------------------------------------
   public void Dump (StringBuilder sb) {
      sb.Append ($"{Index} UID:{UId} {Kind} {Flags} {X.DV}x{Y.DV} Next:{Next} Children:{FirstChild}..{LastChild}");
   }

   /// <summary>Fetch the memo related to this elemet</summary>
   /// The UXMemo stores 'long term' data related to this element, and is not regenerated
   /// on every frame. The memo for a Node is indexed using its UId (which is permanent and
   /// unchanging)
   public readonly ref Memo GetMemo () => ref UXEngine.Memo[UId];

   /// <summary>Does this node 'grow' along the given axis?</summary>
   public readonly bool IsGrow (bool xAxis) {
      if (xAxis) return X.Mode is ESizing.Grow;
      else return Y.Mode is ESizing.Grow;
   }

   /// <summary>Has the mouse been hovering over this element for the given time</summary>
   public readonly bool IsHovered (int ms) => GetMemo ().IsHovered (ms);

   /// <summary>
   /// Has the mouse been released in this frame
   /// </summary>
   public readonly bool IsReleased
      => UXEngine.MousePressedLastFrame && !UXEngine.MousePressed && GetMemo ().IsMouseOver;

   /// <summary>Set uniform padding all around</summary>
   public void SetPadding (int n) {
      ref Axis x = ref X, y = ref Y;
      x.PadStart = x.PadEnd = y.PadStart = y.PadEnd = (short)n;
   }

   /// <summary>Sets different padding for horizontal (left/right) and vertical (top/bottom)</summary>
   public void SetPadding (int h, int v) {
      ref Axis x = ref X, y = ref Y;
      x.PadStart = x.PadEnd = (short)h; y.PadStart = y.PadEnd = (short)v;
   }

   /// <summary>Sets different padding for each of the 4 directions</summary>
   public void SetPadding (int l, int t, int r, int b) {
      ref Axis x = ref X, y = ref Y;
      x.PadStart = (short)l; x.PadEnd = (short)r;
      y.PadStart = (short)t; y.PadEnd = (short)b;
   }

   // Implementation -----------------------------------------------------------
   // Compute if any popups are open (called only after render is complete)
   internal readonly bool ComputePopupsOpen () {
      for (int c = FirstChild; c != 0; c = UXEngine.Nodes[c].Next) {
         ref UXNode child = ref UXEngine.Nodes[c];
         if (!child.IsPopup) continue;
         if (child.IsPopup && child.Rect.Contains (UXEngine.MousePos)) return true;
         if (child.AnyPopupsOpen) return true;
      }
      return false;
   }

   readonly bool Get (EFlags flags) => (Flags & flags) != 0;
   void Set (EFlags flags, bool value) { if (value) Flags |= flags; else Flags &= ~flags; }

   // Does 'fit' sizing of this node along the given axis.
   // This takes the sizes of the children (which are already computed, since this going
   // bottom-up) and either
   // a. Adds them up (including child gaps), adds padding, if the layout is 'ALONG' this axis
   // b. Takes the max of them, adds padding, if the layout is 'ACROSS' this axis
   internal void DoFitSizing (bool xAxis) {
      if (!GetChildren (mTmp, EEnum.Inlaid)) return;
      bool along = xAxis == IsHorizontal;
      ref Axis ax = ref (xAxis ? ref X : ref Y);
      int total = along ? ChildGap * (Math.Max (0, mTmp.Count - 1)) : 0;
      foreach (var c in mTmp) {
         ref UXNode child = ref UXEngine.Nodes[c];
         ref Axis cax = ref (xAxis ? ref child.X : ref child.Y);
         if (along) total += cax.DV;
         else total = Math.Max (total, cax.DV);
      }
      total += ax.TotalPad;
      ax.DV = (short)total.Clamp (ax.Min, ax.Max);
   }
   static List<short> mTmp = [];

   // Grows or shrinks children (along the given axis)
   // ALONG:
   // - If there is more space available, it is distributed amongst the chilren that
   //   have 'grow' mode, while also trying not to grow any child beyond its MAX
   // - If there is less space available, we shrink the children 
   internal void DoGrowShrinkChildren (bool xAxis) {
      if (!GetChildren (mTmp, EEnum.Inlaid)) return;
      bool along = xAxis == IsHorizontal;
      ref Axis ax = ref (xAxis ? ref X : ref Y);
      if (along) {
         // Processing ALONG the layout axis
         int space = GetRemainingSpace (mTmp, xAxis);
         if (space >= 0) {
            // We have more space available. Remove the children that are not marked 'grow'
            // and distribute the remaining space amongst the 'grow' children
            mTmp.RemoveIf (a => !UXEngine.Nodes[a].IsGrow (xAxis));
            if (mTmp.Count > 0) GrowChildren (xAxis, space, mTmp);
         } else 
            // We have less space available than the children demand, we have no option but
            // to 'shrink' them
            ShrinkChildren (xAxis, -space, mTmp);
      } else {
         // Processing ACROSS the layout axis
         int space = ax.DV - ax.TotalPad;
         foreach (var c in mTmp) {
            ref UXNode child = ref UXEngine.Nodes[c];
            ref Axis cax = ref (xAxis ? ref child.X : ref child.Y);
            // If we have more space available, allocate it for the Grow children. Otherwise,
            // if we have less space available, clamp the child size. 
            if (space < cax.DV || cax.Mode == ESizing.Grow)
               cax.DV = (short)space.Clamp (cax.Min, cax.Max);
         }
      }
   }

   // Enumerates the children of this node (either Inlaid or Popups)
   internal readonly bool GetChildren (List<short> tmp, EEnum which) {
      tmp.Clear ();
      if (ChildCount == 0) return false;
      for (short a = FirstChild; a != 0; a = UXEngine.Nodes[a].Next) {
         bool include = which switch {
            EEnum.Inlaid => !UXEngine.Nodes[a].IsPopup,
            EEnum.Popups => UXEngine.Nodes[a].IsPopup,
            _ => true
         };
         if (include) tmp.Add (a);
      }
      return tmp.Count > 0;
   }

   // Returns a particular corner of a node
   internal readonly Vec2S GetCorner (ECorner corner) {
      int x = X.V0, y = Y.V0, dx = X.DV, dy = Y.DV;
      return corner switch {
         ECorner.TopLeft => new (x, y),
         ECorner.Top => new (x + dx / 2, y),
         ECorner.TopRight => new (x + dx, y),
         ECorner.Left => new (x, y + dy / 2),
         ECorner.Center => new (x + dx / 2, y + dy / 2),
         ECorner.Right => new (x + dx, y + dy / 2),
         ECorner.BotLeft => new (x, y + dy),
         ECorner.Bottom => new (x + dx / 2, y + dy),
         ECorner.BotRight => new (x + dx, y + dy),
         _ => throw new BadCaseException (corner)
      };
   }

   /// <summary>Returns the grandparent node for this</summary>
   /// If the node has no grandparent (is not level 2 or above) this will throw an 
   /// exception
   public readonly ref UXNode GetGrandparent ()
      => ref GetParent ().GetParent ();

   /// <summary>Returns the parent node for this</summary>
   /// If this is the root node already, this will throw an exception
   public readonly ref UXNode GetParent ()
      => ref UXEngine.Nodes[Parent];

   // Gets the remaining space in the given axis, which should be the 'ALONG' axis
   // of this ndoe
   internal readonly int GetRemainingSpace (List<short> children, bool xAxis) {
      ref readonly Axis ax = ref (xAxis ? ref X : ref Y);
      int space = ax.DV - ax.TotalPad - ChildGap * (ChildCount - 1);
      Lib.Assert (xAxis == IsHorizontal); // Check we're doing this on the ALONG axis
      foreach (var c in children) {
         ref UXNode child = ref UXEngine.Nodes[c];
         ref Axis cax = ref (xAxis ? ref child.X : ref child.Y);
         space -= cax.DV;
      }
      return space;
   }

   // Grows children along the specified axis (which must be the ALONG axis for this node)
   // For more details on this routine see the excellent YouTube video 'How Clay's UI Layout
   // Algorithm Works'
   readonly void GrowChildren (bool xAxis, int space, List<short> children) {
      while (space > 0 && children.Count > 0) {
         int prevSpace = space;
         int smallest = short.MaxValue, secondSmallest = smallest, widthToAdd = space;
         foreach (var c in children) {
            ref UXNode child = ref UXEngine.Nodes[c];
            int dv = xAxis ? child.X.DV : child.Y.DV;
            if (dv < smallest) { secondSmallest = smallest; smallest = dv; }
            if (dv > smallest) { secondSmallest = Math.Min (secondSmallest, dv); widthToAdd = secondSmallest - smallest; }
         }
         widthToAdd = widthToAdd.Clamp (1, space / children.Count);

         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref UXEngine.Nodes[children[i]];
            ref Axis cax = ref (xAxis ? ref child.X : ref child.Y);
            if (cax.DV != smallest) continue;
            int toAdd = Math.Min (widthToAdd, cax.Max - cax.DV);
            cax.DV = (short)(cax.DV + toAdd);
            if (cax.DV >= cax.Max) children.RemoveAt (i);
            if ((space -= toAdd) <= 0) break;
         }
         if (space == prevSpace) break;
      }
   }

   // This is the symmetric routine to GrowChildren - it shrinks the children if we don't have
   // enough space (as long as we don't go below the minimums for this element). See the YouTube
   // video 'How Clay's UI Layout Algorithm Works' for more on this. 
   readonly void ShrinkChildren (bool xAxis, int space, List<short> children) {
      while (space > 0 && children.Count > 0) {
         int prevSpace = space;
         int largest = 0, secondLargest = 0, widthToSub = space;
         foreach (var c in children) {
            ref UXNode child = ref UXEngine.Nodes[c];
            int dv = xAxis ? child.X.DV : child.Y.DV;
            if (dv > largest) { secondLargest = largest; largest = dv; }
            if (dv < largest) { secondLargest = Math.Max (secondLargest, dv); widthToSub = largest - secondLargest; }
         }
         widthToSub = widthToSub.Clamp (1, space / children.Count);

         for (int i = children.Count - 1; i >= 0; i--) {
            ref UXNode child = ref UXEngine.Nodes[children[i]];
            ref Axis cax = ref (xAxis ? ref child.X : ref child.Y);
            if (cax.DV != largest) continue;
            int toSub = Math.Min (widthToSub, cax.DV - cax.Min);
            cax.DV = (short)(cax.DV - toSub);
            if (cax.DV <= cax.Min) children.RemoveAt (i);
            if ((space -= toSub) <= 0) break;
         }
         if (space == prevSpace) break;
      }
   }

   public override readonly string ToString () => $"Node #{UId} {Kind}";
}
