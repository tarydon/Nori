using System;
namespace Nori;

// Contains types nested inside UXNode
public partial struct UXNode {
   // Enumerations -------------------------------------------------------------
   /// <summary>What kind of UX node is this?</summary>
   public enum EKind {
      Root, Rect, Panel,
   }

   /// <summary>Flags bits for a UXNode</summary>
   [Flags]
   public enum EFlags {
      /// <summary>Children are laid out left-to-right (otherwise, top-to-bottom)</summary>
      Horizontal = 1 << 0,
      /// <summary>We need a 'wrap' pass (the contents cannot always fit in the horizontal axis)</summary>
      Wrap = 1 << 1,
      /// <summary>This behaves like a 'scroll container', the child can be scrolled</summary>
      Scrollable = 1 << 2,
      /// <summary>This is a floating / popup element</summary>
      Popup = 1 << 3,
      /// <summary>This popup is laid out relative to screen, not relative to parent</summary>
      ScreenRelative = 1 << 4,
      /// <summary>Draw a shadow for this</summary>
      Shadow = 1 << 5,
   }

   /// <summary>Various sizing modes for an axis</summary>
   public enum ESizing { Fit, Grow, Fixed, Percent }
   /// <summary>Alignment aling X or Y axis (left..right or top..bottom)</summary>
   public enum EAlign { Start, Middle, End }

   /// <summary>One of the nine corners of a node (used to align floating elements)</summary>
   public enum ECorner {
      TopLeft, Top, TopRight, Left, Center, Right, BotLeft, Bottom, BotRight
   }
   /// <summary>Which corners are rounded?</summary>
   /// 'Left' means TL and BL corners are rounded
   /// 'Top' means TL and RT corners are rounded etc
   /// So this model allows one to round 0, 2 or 4 corners (which is all we should need
   /// for UI)
   public enum ECorners { None, All, Left, Top, Right, Bottom }

   // Struct Axis --------------------------------------------------------------
   /// <summary>Axis metrics</summary>
   /// This contains all axis-specific information like the desired size, grow mode,
   /// computed size and position etc. We maintain one of these for X and Y 
   public struct Axis {
      /// <summary>The sizing mode for this axis</summary>
      public ESizing Mode;
      /// <summary>Minimum, maximum size for this axis (Max=0 is same as Max=int.MaxValue)</summary>
      public int Min, Max;

      /// <summary>Padding at the start (Left/Top)</summary>
      public short PadStart;
      /// <summary>Padding at the end (Right/Bottom)</summary>
      public short PadEnd;
      /// <summary>Alignment of children in this axis</summary>
      public EAlign ChildAlign;
      /// <summary>Total padding on this axis (start + end)</summary>
      public readonly short TotalPad => (short)(PadStart + PadEnd);

      /// <summary>The start position along this axis (X/Y)</summary>
      public int V;
      /// <summary>The span along this axis (DX/DY) extent is the semi-open interval [V, V+DV)</summary>
      public int DV;

      public void Set (Size size) { Mode = size.Mode; Min = size.Min; Max = size.Max; }
   }

   // Struct Size --------------------------------------------------------------
   public readonly struct Size {
      // Constructors 
      public Size (ESizing mode, int min, int max) => (Mode, Min, Max) = (mode, min, max);

      public static Size Grow () => new (ESizing.Grow, 0, 0);
      public static Size Grow (int n) => new (ESizing.Grow, n, 0);
      public static Size Grow (int n0, int n1) => new (ESizing.Grow, n0, n1);
      public static Size Fit () => new (ESizing.Fit, 0, 0);    // <-- This is the default
      public static Size Fit (int n) => new (ESizing.Fit, n, 0);
      public static Size Fit (int n0, int n1) => new (ESizing.Fit, n0, n1);
      public static Size Fixed (int n) => new (ESizing.Fixed, n, n);

      public static implicit operator Size (int n) => new (ESizing.Fixed, n, n);

      public readonly ESizing Mode;
      public readonly int Min;
      public readonly int Max;
   }
}

