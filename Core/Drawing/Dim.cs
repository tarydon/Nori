// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dim.cs
// ║║║║╬║╔╣║ Implements various types of E2Dimension entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E2Dim2P ------------------------------------------------------------------------------
public class E2Dim2P : E2Dimension {
   E2Dim2P () { }

   /// <summary>Aligned dimension c'tor</summary>
   /// <param name="layer">Provide drawing layer to place it in</param>
   /// <param name="a">Start point of measurement</param>
   /// <param name="b">End point of measurement</param>
   /// <param name="pt">Dimension placement point</param>
   /// <param name="angle">Direction of measurement</param>
   /// <param name="text">Actual text to display (or null if it should be computed)</param>
   public E2Dim2P (Layer2 layer, Point2 a, Point2 b, Point2 pt, double angle, string? text = null)
      : base (layer) => (A, B, C, Angle, Text) = (a, b, pt, angle, text);

   public readonly Point2 A, B, C;
   public readonly double Angle;
   public readonly string? Text;

   public override IReadOnlyList<Ent2> MakeDim (DimSettings dim) {
      List<Ent2> ents = [];
      if (A.EQ (B)) return ents;
      // Consider an infinite line via A and B, perpendicular to Angle
      var (A2, B2) = (A.Polar (100, Angle + Lib.HalfPI), B.Polar (100, Angle + Lib.HalfPI));
      // Consider another infinite line via C, parallel to Angle
      var C2 = C.Polar (100, Angle);
      // Dimension line points
      var (pt, pt2) = (Geo.LineXLine (A, A2, C, C2), Geo.LineXLine (B, B2, C, C2));
      Lib.Check (!pt.IsNil && !pt2.IsNil, "Coding error");
      ents.AddM (new E2Poly (Layer, Poly.Line (pt, pt2))
         , new E2Point (Layer, A), new E2Point (Layer, B)
         , new E2Poly (Layer, Poly.Line (A.Polar (dim.DimOffset, A.AngleTo (pt)), pt.Polar (dim.DimExtend, A.AngleTo (pt))))
         , new E2Poly (Layer, Poly.Line (B.Polar (dim.DimOffset, B.AngleTo (pt2)), pt2.Polar (dim.DimExtend, B.AngleTo (pt2)))));
      var text = PrepareDimText (Text, pt.DistTo (pt2).Round (dim.DimLinDecimals));
      var textAng = pt.AngleTo (pt2);
      // Fix the textAng to avoid inverted text, etc [Lets limit it to -90, 90 range]
      bool revDir = textAng is > Lib.HalfPI or < -Lib.HalfPI;
      if (revDir) textAng += Lib.PI;
      var (arrowDirA, arrowDirB) = revDir ? (textAng, textAng + Lib.PI) : (textAng + Lib.PI, textAng);
      ents.AddM (MakeArrow (Layer, pt, arrowDirA, dim.DimArrowSize), MakeArrow (Layer, pt2, arrowDirB, dim.DimArrowSize));
      var textPos = pt.Midpoint (pt2).Polar (dim.DimTxtSize / 5, textAng + Lib.HalfPI); // Magic: Gap b/w dim-line & dim-text
      ents.Add (new E2Text (Layer, dim.DimTextStyle, text, textPos, dim.DimTxtSize, textAng, 0, 1, dim.DimTxtAlign));
      return ents;
   }
}
#endregion

#region class DimSettings --------------------------------------------------------------------------
public class DimSettings {
   /// <summary>Dimension arrow size</summary>
   public float DimArrowSize = 6;
   /// <summary>Gap between actual reference point and start of the extension line</summary>
   public float DimOffset = 4;
   /// <summary>Extension line length</summary>
   public float DimExtend = 4;
   /// <summary>How many decimal places in linear dimensions</summary>
   public short DimLinDecimals = 2;
   /// <summary>How many decimal places in angular dimensions</summary>
   public short DimAngDecimals = 2;
   /// <summary>Text height for dimensioning</summary>
   public float DimTxtSize = 10;
   /// <summary>Text alignment for dimensioning</summary>
   public ETextAlign DimTxtAlign = ETextAlign.BaseCenter;
   /// <summary>Font/text style for dimensioning</summary>
   public Style2 DimTextStyle = Style2.Default;
}
#endregion
