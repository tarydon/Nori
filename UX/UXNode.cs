// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXNode.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public partial struct UXNode {
   // Fields -------------------------------------------------------------------
   // Core ........................
   /// <summary>Index of the node in UXEngine.Nodes[] (this is transient and can change each frame)</summary>
   public short Id;
   /// <summary>What kind of node is this?</summary>
   public EKind Kind;
   /// <summary>Flags bits for this node</summary>
   public EFlags Flags;
   /// <summary>The persistent UID for this node, used to index into Memo[]</summary>
   public uint IdMemo;

   // Node tree ----------------------------------------------------------------
   /// <summary>Parent node for this (0 for root node)</summary>
   public short Parent;
   /// <summary>Fist child node (0 means no children)</summary>
   public short FirstChild;
   /// <summary>Last child node (if same as FirstChild, there is only one child)</summary>
   public short LastChild;
   /// <summary>Count of children for this node</summary>
   public short ChildCount;

   // Metrics ------------------------------------------------------------------
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

   // Colors -------------------------------------------------------------------
   /// <summary>Background color</summary>
   public Color4 BgrdColor;
   /// <summary>Border color, if there is a border</summary>
   public Color4 BorderColor;
   /// <summary>Foreground color (text / drawings etc)</summary>
   public Color4 FgrdColor;

   // Text ---------------------------------------------------------------------
   /// <summary>Text to use</summary>
   public string? Text;
   /// <summary>Font ID (index into UXEngine.Fonts)</summary>
   public short FontId;
   /// <summary>TextOffset (position of Lux.Text call, relative to top-left-corner of node)</summary>
   public Vec2S TextOffset;

   // Floating elements --------------------------------------------------------
   /// <summary>Position on tihs element that is used for alignment</summary>
   public ECorner ElemCorner;
   /// <summary>Position on the parent that is used for alignment</summary>
   public ECorner ParentCorner;
   /// <summary>Offset between the two</summary>
   public Vec2S FloatOffset;

   /*

   // Properties ---------------------------------------------------------------
   /// <summary>Is this node having 'horizontal' layout</summary>
   public bool IsHorizontal { readonly get => Get (EFlags.Horizontal); set => Set (EFlags.Horizontal, value); }

   public readonly RectS Rect => new (X.V0, Y.V0, X.V0 + X.DV, Y.V0 + Y.DV);

   public readonly int ZLevel => 100 + Level * 2;

   public readonly bool IsPopup => Get (EFlags.Popup);

   public readonly bool HasShadow => Get (EFlags.Shadow);

   /// <summary>Is this POPUP aligned relative to the screen</summary>
   public bool IsScreenRelative { readonly get => Get (EFlags.ScreenRelative); set => Set (EFlags.ScreenRelative, value); }

   // Methods ------------------------------------------------------------------
   public void Dump (StringBuilder sb) {
      sb.Append ($"{Id} {Kind} {Data} {X.DV}x{Y.DV} ({X.Max}) {X.Mode} Remain:{RemainingSpace}");
   }

   public void DoFitSizing (bool xAxis) {
      if (ChildCount == 0 || !GetChildren (mTmp, EEnum.Children)) return;
      bool along = xAxis == IsHorizontal;
      ref AxisDef ax = ref (xAxis ? ref X : ref Y);
      int total = along ? ChildGap * (Math.Max (0, mTmp.Count - 1)) : 0;
      foreach (var c in mTmp) {
         ref Node child = ref UXSystem.Nodes[c];
         ref AxisDef cax = ref (xAxis ? ref child.X : ref child.Y);
         if (along) total += cax.DV;
         else total = Math.Max (total, cax.DV);
      }
      total += ax.TotalPad;
      ax.DV = (short)total.Clamp (ax.Min, ax.Max);
   }

   public void DoGrowShrinkChildren (bool xAxis) {
      if (!GetChildren (mTmp, EEnum.Children)) return;
      bool along = xAxis == IsHorizontal;
      ref AxisDef ax = ref (xAxis ? ref X : ref Y);
      if (along) {
         // If we have a positive amount of space left, remove the children that are not 'grow'
         int space = GetRemainingSpace (mTmp, xAxis);
         RemainingSpace = (short)space;
         if (space >= 0) {
            mTmp.RemoveIf (a => !UXSystem.Nodes[a].IsGrow (xAxis));
            if (mTmp.Count > 0) GrowChildren (xAxis, space, mTmp);
         } else
            ShrinkChildren (xAxis, -space, mTmp);
      } else {
         int space = ax.DV - ax.TotalPad;
         foreach (var c in mTmp) {
            ref Node child = ref UXSystem.Nodes[c];
            ref AxisDef cax = ref (xAxis ? ref child.X : ref child.Y);
            if (space < cax.DV || cax.Mode == ESizing.Grow)
               cax.DV = (short)space.Clamp (cax.Min, cax.Max);
         }
      }
   }
   static List<short> mTmp = [];

   public readonly Vec2S GetCorner (ECorner corner) {
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

   public readonly ref Node GetParent ()
      => ref UXSystem.Nodes[Parent];

   public readonly ref Node GetGrandparent ()
      => ref GetParent ().GetParent ();

   readonly void ShrinkChildren (bool xAxis, int space, List<short> children) {
      while (space > 0 && children.Count > 0) {
         int prevSpace = space;
         int largest = 0, secondLargest = 0, widthToSub = space;
         foreach (var c in children) {
            ref Node child = ref UXSystem.Nodes[c];
            int dv = child.GetSize (xAxis);
            if (dv > largest) { secondLargest = largest; largest = dv; }
            if (dv < largest) { secondLargest = Math.Max (secondLargest, dv); widthToSub = largest - secondLargest; }
         }
         widthToSub = widthToSub.Clamp (1, space / children.Count);

         for (int i = children.Count - 1; i >= 0; i--) {
            ref Node child = ref UXSystem.Nodes[children[i]];
            ref AxisDef cax = ref (xAxis ? ref child.X : ref child.Y);
            if (cax.DV != largest) continue;
            int toSub = Math.Min (widthToSub, cax.DV - cax.Min);
            cax.DV = (short)(cax.DV - toSub);
            if (cax.DV <= cax.Min) children.RemoveAt (i);
            if ((space -= toSub) <= 0) break;
         }
         if (space == prevSpace) break;
      }
   }

   readonly void GrowChildren (bool xAxis, int space, List<short> children) {
      while (space > 0 && children.Count > 0) {
         int prevSpace = space;
         int smallest = short.MaxValue, secondSmallest = smallest, widthToAdd = space;
         foreach (var c in children) {
            ref Node child = ref UXSystem.Nodes[c];
            int dv = child.GetSize (xAxis);
            if (dv < smallest) { secondSmallest = smallest; smallest = dv; }
            if (dv > smallest) { secondSmallest = Math.Min (secondSmallest, dv); widthToAdd = secondSmallest - smallest; }
         }
         widthToAdd = widthToAdd.Clamp (1, space / children.Count);

         for (int i = children.Count - 1; i >= 0; i--) {
            ref Node child = ref UXSystem.Nodes[children[i]];
            ref AxisDef cax = ref (xAxis ? ref child.X : ref child.Y);
            if (cax.DV != smallest) continue;
            int toAdd = Math.Min (widthToAdd, cax.Max - cax.DV);
            cax.DV = (short)(cax.DV + toAdd);
            if (cax.DV >= cax.Max) children.RemoveAt (i);
            if ((space -= toAdd) <= 0) break;
         }
         if (space == prevSpace) break;
      }
   }

   public readonly bool IsGrow (bool xAxis) {
      if (xAxis) return X.Mode is ESizing.Grow;
      else return Y.Mode is ESizing.Grow;
   }

   public readonly bool IsMouseOver => GetMemo ().IsMouseOver;

   public readonly bool IsHovered (int ms) => GetMemo ().IsHovered (ms);

   public readonly short GetSize (bool xAxis) => xAxis ? X.DV : Y.DV;

   public readonly bool GetChildren (List<short> tmp, EEnum which) {
      tmp.Clear ();
      for (short a = FirstChild; a != 0; a = UXSystem.Nodes[a].Next) {
         bool include = which switch {
            EEnum.Children => !UXSystem.Nodes[a].IsPopup,
            EEnum.Popups => UXSystem.Nodes[a].IsPopup,
            _ => true
         };
         if (include) tmp.Add (a);
      }
      return tmp.Count > 0;
   }

   public readonly ref NodeMemo GetMemo () => ref UXSystem.Memo[UId];

   public readonly int GetRemainingSpace (List<short> children, bool xAxis) {
      ref readonly AxisDef ax = ref (xAxis ? ref X : ref Y);
      int space = ax.DV - ax.TotalPad - ChildGap * (ChildCount - 1);
      foreach (var c in children) {
         ref Node child = ref UXSystem.Nodes[c];
         ref AxisDef cax = ref (xAxis ? ref child.X : ref child.Y);
         space -= cax.DV;
      }
      return space;
   }

   /// <summary>Set uniform padding all around</summary>
   public void SetPadding (int n) {
      ref AxisDef x = ref X; x.PadStart = x.PadEnd = (short)n;
      ref AxisDef y = ref Y; y.PadStart = y.PadEnd = (short)n;
   }

   public void SetPadding (int left, int top, int right, int bottom) {
      ref AxisDef x = ref X; x.PadStart = (short)left; x.PadEnd = (short)right;
      ref AxisDef y = ref Y; y.PadStart = (short)top; y.PadEnd = (short)bottom;
   }

   public override readonly string ToString () => $"Node #{UId} {Kind}";

   // Implementation -----------------------------------------------------------
   readonly bool Get (EFlags flags) => (Flags & flags) != 0;
   void Set (EFlags flags, bool value) { if (value) Flags |= flags; else Flags &= ~flags; }
*/
}
