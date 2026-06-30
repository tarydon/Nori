// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UXNode.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.UX;

public struct UXNode {
   // Tree .................................................
   public int Id;
   public int Parent;
   public int FirstChild;
   public int LastChild;
   public int ChildCount;
   public int Next;
   public string? Tag;
   public int Level;
   public EKind Kind;

   // Layout ...............................................
   public EOrientation Orientation;
   public MarginS Padding;
   public short ChildGap;
   public SizeS Width;
   public SizeS Height;
   public Vec2S ChildOffset;
   public EChildAlignX ChildAlignX;
   public EChildAlignY ChildAlignY;

   public readonly bool IsHovered => UXFrame.IsHovered (Id);
   public readonly bool IsPressed => UXFrame.IsPressed (Id);

   // Colors ...............................................
   public Color4 BgrdColor;
   public Color4 OverlayColor;

   // Text .................................................
   public string? Text;
   public short FontId;
   public EWrap Wrap;
   public ETextAlign TextAlign;
   public Color4 TextColor;
   public Vec2S TextOffset;

   // Border ...............................................
   public short CornerRadius;
   public MarginS Border;
   public Color4 BorderColor;

   // Floating .............................................
   public bool Floating;
   public Vec2S FloatOffset;
   public short ZIndex;
   public ECorner ElemCorner;
   public ECorner ParentCorner;

   // Computed .............................................
   public int X, Y, DX, DY;

   public readonly bool Horizontal => Orientation == EOrientation.LeftToRight;
   public readonly bool Vertical => Orientation == EOrientation.TopToBottom;

   public readonly IEnumerable<int> EnumChildren (UXNode[] nodes) {
      for (int n = FirstChild; n != 0; n = nodes[n].Next)
         yield return n; 
   }

   public readonly List<int> GetChildren (UXNode[] nodes) {
      List<int> children = [];
      for (int n = FirstChild; n != 0; n = nodes[n].Next) children.Add (n);
      return children;
   }

   public readonly void GetChildren (UXNode[] nodes, List<int> children, bool skipFloating) {
      children.Clear ();
      for (int n = FirstChild; n != 0; n = nodes[n].Next) {
         if (skipFloating) {
            ref UXNode child = ref nodes[n];
            if (child.Floating) continue;
         }
         children.Add (n);
      }
   }

   public readonly ref UXNode GetParent (UXNode[] nodes) => ref nodes[Parent];

   public readonly ref UXNode GetGrandParent (UXNode[] nodes)
      => ref GetParent (nodes).GetParent (nodes);

   // Nested types -------------------------------------------------------------
   public enum EOrientation : short { LeftToRight, TopToBottom };
   public enum EWrap : short { Word, Newline, None };
   public enum ETextAlign : short { Left, Center, Right };
   public enum EChildAlignX : short { Left, Center, Right };
   public enum EChildAlignY : short { Top, Middle, Bottom };
   public enum ESizeMode : short { Fit, Grow, Fixed, Percent }
   public enum ECorner : short { LeftTop, Top, RightTop, Left, Center, Right, LeftBottom, Bottom, RightBottom };
   public enum EKind {
      Generic, TopMenu, 
   }
};

public readonly struct MarginS {
   public MarginS (int a) : this (a, a, a, a) { }
   public MarginS (int l, int r, int t, int b) { Left = (short)l; Right = (short)r; Top = (short)t; Bottom = (short)b; }
   public static implicit operator MarginS (int a) => new (a);

   public readonly short Horizontal => (short)(Left + Right);
   public readonly short Vertical => (short)(Top + Bottom);
   public bool IsZero => Left + Top + Right + Bottom == 0; 

   public readonly short Left, Right, Top, Bottom;
}

public readonly struct SizeS {
   public SizeS (int a) : this (a, a, UXNode.ESizeMode.Fixed) { }
   public SizeS (int min, int max, UXNode.ESizeMode mode) { Min = (short)min; Max = (short)max; Mode = mode; }
   public static SizeS Grow (int min = 0) => new (min, short.MaxValue, UXNode.ESizeMode.Grow);
   public static SizeS Fit (int min = 0) => new (min, short.MaxValue, UXNode.ESizeMode.Fit);

   public readonly short Min, Max;
   public readonly UXNode.ESizeMode Mode;

   public static implicit operator SizeS (int a) => new (a);
}
