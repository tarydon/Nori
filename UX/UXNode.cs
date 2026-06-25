namespace Nori.UX;

public struct UXNode {
   // Layout ...............................................
   public EOrientation Orientation;
   public MarginS Padding;
   public short ChildGap;
   public RangeS Width;
   public RangeS Height;
   public Vec2S ChildOffset;

   // Colors ...............................................
   public Color4 BgrdColor;
   public Color4 OverlayColor;

   // Text .................................................
   public string? Text;
   public short FontId;
   public EWrap Wrap;
   public ETextAlign TextAlign;
   public Color4 TextColor;

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
   
   // Nested types -------------------------------------------------------------
   public enum EOrientation : short { LeftToRight, TopToBottom };
   public enum EWrap : short { Word, Newline, None };
   public enum ETextAlign : short { Left, Center, Right };
   public enum EChildAlignX : short { Left, Center, Right };
   public enum EChildAlignY : short { Top, Center, Bottom };
   public enum ESizeMode : short { Fit, Grow, Fixed, Percent }
   public enum ECorner : short { LeftTop, Top, RightTop, Left, Center, Right, LeftBottom, Bottom, RightBottom };

};

public readonly struct MarginS {
   public MarginS (int a) : this (a, a, a, a) { }
   public MarginS (int l, int r, int t, int b) { Left = (short)l; Right = (short)r; Top = (short)t; Bottom = (short)b; }
   public readonly short Left, Right, Top, Bottom;
}

public readonly struct RangeS {
   public RangeS (int a) : this (a, a) { }
   public RangeS (int min, int max) { Min = (short)min; Max = (short)max; }
   public readonly short Min, Max;
}