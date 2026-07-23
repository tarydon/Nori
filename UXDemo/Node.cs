// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Node.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace UXDemo;

public struct Node {
   // Core ---------------------------------------------------------------------
   /// <summary>The inded of this node in mNodes[]</summary>
   public short Id;
   /// <summary>What kind of node is this?</summary>
   public EKind Kind;
   /// <summary>Flag bits for this node</summary>
   public EFlags Flags;
   /// <summary>Generic data, interpretation depends on Kind</summary>
   public object Data;

   // Node tree ----------------------------------------------------------------
   /// <summary>Parent node for this (0 for root node)</summary>
   public short Parent;
   /// <summary>First child node (0 means no children)</summary>
   public short FirstChild;
   /// <summary>Last child node (0 means no children, can be same as FirstChild if only one child)</summary>
   public short LastChild;
   /// <summary>Count of children for this node</summary>
   public short ChildCount;
   /// <summary>Next sibling node (0 means end-of-list)</summary>
   public short Next;
   /// <summary>Node's 'level' (root node is at level 0)</summary>
   public short Level;

   // Metrics ------------------------------------------------------------------
   /// <summary>Metrics for the X axis (width direction)</summary>
   public AxisDef X;
   /// <summary>Metrics for the Y axis (height direction)</summary>
   public AxisDef Y;
   /// <summary>Border width, if non-zero</summary>
   public short BorderWidth;
   /// <summary>Corner radius</summary>
   public short CornerRadius;

   // Child positioning --------------------------------------------------------
   /// <summary>Gap between successive children</summary>
   public short ChildGap;
   /// <summary>X-scroll position to start positioning children in X</summary>
   public short XScroll;
   /// <summary>Y-scroll position to start positioning children in Y</summary>
   public short YScroll;

   // Colors -------------------------------------------------------------------
   /// <summary>Background color</summary>
   public Color4 BgrdColor;
   /// <summary>Border color (if border exists)</summary>
   public Color4 BorderColor;
   /// <summary>Foreground color (for example Text color)</summary>
   public Color4 FgrdColor;

   // Text ---------------------------------------------------------------------   
   /// <summary>The text to use</summary>
   public string? Text;
   /// <summary>Font to use</summary>
   public short FontId;
   /// <summary>The text offset (position of Lux.Text call, relative to top-left corner of node)</summary>
   public Vec2S TextOffset;

   // Floating elements --------------------------------------------------------
   /// <summary>Position on this element that is used for alignment</summary>
   public ECorner ElemCorner;
   /// <summary>Position on the parent that is used for alignment</summary>
   public ECorner ParentCorner;
   /// <summary>Offset between the two</summary>
   public Vec2S FloatOffset;

   // Properties ---------------------------------------------------------------
   /// <summary>Is this node having 'horizontal' layout</summary>
   public bool IsHorizontal { readonly get => Get (EFlags.Horizontal); set => Set (EFlags.Horizontal, value); }

   public readonly RectS Rect => new (X.V0, Y.V0, X.V0 + X.DV, Y.V0 + Y.DV);

   // Methods ------------------------------------------------------------------
   public readonly bool GetChildren (List<short> tmp) {
      tmp.Clear (); 
      for (short a = FirstChild; a != 0; a = UXSystem.mNodes[a].Next)
         tmp.Add (a);
      return tmp.Count > 0;
   }

   /// <summary>Set uniform padding all around</summary>
   public void SetPadding (int n) {
      ref AxisDef x = ref X; x.PadStart = x.PadEnd = (short)n;
      ref AxisDef y = ref Y; y.PadStart = y.PadEnd = (short)n;
   }

   // Implementation -----------------------------------------------------------
   readonly bool Get (EFlags flags) => (Flags & flags) != 0;
   void Set (EFlags flags, bool value) { if (value) Flags |= flags; else Flags &= ~flags; }
}
