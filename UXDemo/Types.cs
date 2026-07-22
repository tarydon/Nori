// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Types.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace UXDemo;

/// <summary>What Kind of UXNode is this?</summary>
public enum EKind {
   Unknown, Root, Rect, Panel, Text,
}

/// <summary>The various sizing modes for an axis</summary>
public enum ESizing { Fit, Grow, Fixed, Percent };
/// <summary>Alignment in X axis</summary>
public enum EXAlign { Left, Center, Right };
/// <summary>Alignment in Y Axis</summary>
public enum EYAlign { Top, Center, Bottom };
/// <summary>Which corners are rounded</summary>
/// 'Left' means the TL and BL corners are rounded, 
/// 'Top' means the TL and TR corners are rounded etc
/// So this model allows one to round 0, 2 or 4 corners (which is all we 
/// should need for UI)
public enum ECorners { None, All, Left, Top, Right, Bottom };

/// <summary>One of the nine corners of a node (used to align floating elements)</summary>
public enum ECorner { TopLeft, Top, TopRight, Left, Center, Right, BotLeft, Bottom, BotRight };

/// <summary>Flag bits for a UXNode</summary>
[Flags]
public enum EFlags {
   /// <summary>Children are laid out left-to-right (otherwise top-to-bottom)</summary>
   Horizontal = 1 << 0,
   /// <summary>If set, children are 'wrapped' if the cannot fit in the main axis</summary>
   Wrap = 1 << 1,
   /// <summary>If set, children are scrolled</summary>
   Scrollable = 1 << 2,
   /// <summary>If set, this is a floating element</summary>
   Floating = 1 << 3,
   HasChildren = 1 << 4,
}

/// <summary>Axis metrics</summary>
/// This contains all axis-specific information like the desired size, grow 
/// mode, computed size and position etc. We maintain one of these for X and Y
public struct AxisDef {
   /// <summary>The sizing mode for this axis</summary>
   public ESizing Mode;
   /// <summary>Minimum size for this axis</summary>
   public short Min;
   /// <summary>Maximum size for this axis (if not zero)</summary>
   /// For Fixed size, Min=Max
   public short Max;  

   /// <summary>Padding at the start (Left/Top)</summary>
   public short PadStart;
   /// <summary>Padding at the end (Right/Bottom)</summary>
   public short PadEnd;
   /// <summary>Total padding</summary>
   public readonly short TotalPad => (short)(PadStart + PadEnd);

   /// <summary>Scroll position in this direction</summary>
   public short Scroll;

   /// <summary>The extent start along this axis (X/Y)</summary>
   public short V0;
   /// <summary>The span along this axis (DX/DY)</summary>
   public short DV;
}
