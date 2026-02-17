// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dim.cs
// ║║║║╬║╔╣║ Implements various types of E2Dimension entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E2Dim2P ------------------------------------------------------------------------------
public class E2Dim2P : E2Dimension {
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

   const double Overhang = 4;

   public override IEnumerable<Ent2> MakeDim () {
      if (A.EQ (B)) yield break;
      // Consider an infinite line via A and B, perpendicular to Angle
      var (A2, B2) = (A.Polar (100, Angle + Lib.HalfPI), B.Polar (100, Angle + Lib.HalfPI));
      // Consider another infinite line via C, parallel to Angle
      var C2 = C.Polar (100, Angle);
      // Dimension line points
      var (pt, pt2) = (Geo.LineXLine (A, A2, C, C2), Geo.LineXLine (B, B2, C, C2));
      Lib.Check (!pt.IsNil && !pt2.IsNil, "Coding error");
      yield return new E2Poly (Layer, Poly.Line (pt, pt2));
      yield return new E2Poly (Layer, Poly.Line (A, pt.Polar (Overhang, A.AngleTo (pt))));
      yield return new E2Poly (Layer, Poly.Line (B, pt2.Polar (Overhang, B.AngleTo (pt2))));
   }
}
#endregion
