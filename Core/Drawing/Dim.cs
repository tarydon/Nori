// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dim.cs
// ║║║║╬║╔╣║ Implements various types of E2Dimension entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E2Dim2P ------------------------------------------------------------------------------
public class E2Dim2P : E2Dimension {
   E2Dim2P () { }

   /// <summary>Aligned linear measurement dimension</summary>
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

#region E2DimAngle ---------------------------------------------------------------------------------
public class E2DimAngle : E2Dimension {
   E2DimAngle () { }

   /// <summary>Angle measurement dimension</summary>
   /// <param name="pc"></param>
   /// <param name="p1"></param>
   /// <param name="p2"></param>
   /// <param name="pd"></param>
   /// <param name="text"></param>
   public E2DimAngle (Layer2 layer, Point2 pc, Point2 p1, Point2 p2, Point2 pd, bool reflexAngle, string? text = null)
      : base (layer) => (Corner, A, B, C, ReflexAngle, Text) = (pc, p1, p2, pd, reflexAngle, text);

   public readonly Point2 Corner, A, B, C;
   readonly string? Text;
   readonly bool ReflexAngle;

   public override IReadOnlyList<Ent2> MakeDim (DimSettings dim) {
      // Measurement ref points,
      double r = Corner.DistTo (C);
      var (angA, angB) = (Corner.AngleTo (A), Corner.AngleTo (B));
      var (A2, B2) = (Corner.Polar (r, angA), Corner.Polar (r, angB));

      var (angle, ccw) = ((A - Corner).AngleTo (B - Corner), B2.Side (Corner, A2) == 1);
      if (ReflexAngle) { angle = Lib.TwoPI - angle; ccw ^= true; }
      var arc = Poly.Arc (Corner, r, angA, angB, ccw);

      List<Ent2> ents = [
         new E2Point (Layer, A), new E2Point (Layer, B),
         new E2Poly (Layer, Poly.Line (A.Polar (dim.DimOffset, angA), A2.Polar (dim.DimExtend, angA))),
         new E2Poly (Layer, Poly.Line (B.Polar (dim.DimOffset, angB), B2.Polar (dim.DimExtend, angB))),
         new E2Poly (Layer, arc)
      ];
      // Dim text
      var text = PrepareDimText (Text, angle.R2D ().Round (dim.DimAngDecimals));
      var aseg = arc[0];
      var textPosAng = Corner.AngleTo (aseg.GetPointAt (0.5));
      var textPos = Corner.Polar (r + dim.DimTxtSize / 5, textPosAng); // Magic: Gap b/w dim-line & dim-text
      var textAng = textPosAng - Lib.HalfPI;
      ents.Add (new E2Text (Layer, dim.DimTextStyle, text, textPos, dim.DimTxtSize, textAng, 0, 1, dim.DimTxtAlign));
      // Arrows
      var (arrowDirA, arrowDirB) = (aseg.GetSlopeAt (0) + Lib.PI, aseg.GetSlopeAt (1));
      ents.AddM (MakeArrow (Layer, A2, arrowDirA, dim.DimArrowSize), MakeArrow (Layer, B2, arrowDirB, dim.DimArrowSize));
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
