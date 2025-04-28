// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Geo.cs
// ║║║║╬║╔╣║ Implements the GEO class, containing a number of geometry primitives
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Geo ----------------------------------------------------------------------------------
/// <summary>The Geo class contains a number of core Geoetry functions</summary>
public static class Geo {
   /// <summary>Computes the intersection points between two circles (could be zero, one or two points)</summary>
   /// <param name="c1">Center point of first circle/param>
   /// <param name="r1">Radius of the first circle</param>
   /// <param name="c2">Center point of the second circle</param>
   /// <param name="r2">Radius of the second circle</param>
   /// <param name="buffer">A buffer with space for at least 2 points</param>
   /// <returns>A slice of the same buffer containing 0, 1 or 2 points</returns>
   /// This routine uses a non-trignometric (analytical geometry) method to compute the two
   /// centers without any trignometric functions, and is very performant. The algorithmn we
   /// use was taken from a stackoverflow response and is summarized in this image:
   /// file://N:/Doc/Img/CircleXCircle.png
   /// 
   /// To avoid allocating a short-lived array to hold the results, this routine takes in a
   /// Span that can hold at least 2 points. The simplest way to allocate that (while avoiding a
   /// heap allocation) is via stackalloc:
   /// <code>
   /// Span&lt;Point&gt; buffer = stackalloc Point2[2];
   /// var pts = Geo.CircleXCircle (c1, r1, c2, r2, buffer);
   /// Console.WriteLine (${pts.Length} intersections found");
   /// foreach (var pt in pts) { ... }
   /// </code>
   public static ReadOnlySpan<Point2> CircleXCircle (Point2 c1, double r1, Point2 c2, double r2, Span<Point2> buffer) {
      double R = c1.DistTo (c2);
      if (R.IsZero ()) return buffer[0..0];     // Circles are concentric
      double R1Sq = r1 * r1, R2Sq = r2 * r2, RSq = R * R, Rp4 = RSq * RSq;

      // The equation shown in the figure has 3 terms (each with an X and Y component in 
      // tuple. We compute those 3 terms as v1, v2 and v3. 
      Vector2 v1 = new ((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);

      // Second term
      double f2 = (R1Sq - R2Sq) / (2 * RSq);
      Vector2 v2 = new ((c2.X - c1.X) * f2, (c2.Y - c1.Y) * f2);

      // Multiplicand for the third term, which will be added or subtracted from v1+v2
      // to get the two points
      double tmp = R1Sq - R2Sq;
      double f3Sq = 2 * (R1Sq + R2Sq) / RSq - tmp * tmp / Rp4 - 1;

      // If the multiplicand is 0, then the two intersection points merge into one (the
      // two circles are touching tangentially), and we can just return the one intersection
      // point as v1 + v2 (the v3 component gets multiplied by zero)
      if (Abs (f3Sq) < 0.0000000001) { buffer[0] = (Point2)(v1 + v2); return buffer[0..1]; }

      // If the square of the 3rd term multiplicand is negative, there are no solutions 
      // (the circles don't intersect at all), and we return zero intersection points
      if (f3Sq < 0) return buffer[0..0];

      // Otherwise, this is the most general case and there are 2 intersection points that
      // we return here as (v1 + v2) + v3, and (v1 + v2) - v3:
      double f3 = 0.5 * Sqrt (f3Sq);
      Vector2 v3 = new ((c2.Y - c1.Y) * f3, (c1.X - c2.X) * f3);
      buffer[0] = (Point2)(v1 + v2 + v3); buffer[1] = (Point2)(v1 + v2 - v3);
      // Return a slice of the input buffer with exactly 2 results in it
      return buffer[0..2];
   }

   /// <summary>Return the intersection Point2 of two lines A-B and C-D</summary>
   /// <param name="A">First Point2 on line 1</param>
   /// <param name="B">Second Point2 on line 1</param>
   /// <param name="C">First Point2 on line 2</param>
   /// <param name="D">Second Point2 on line 2</param>
   /// This treats the lines A-B and C-D as infinite lines, not as finite segments.
   /// If the lines are parallel (do not intersect), this returns Point2.Nil. 
   public static Point2 LineXLine (Point2 A, Point2 B, Point2 C, Point2 D) {
      // Line AB represented as a1x + b1y = c1
      double a1 = B.Y - A.Y, b1 = A.X - B.X, c1 = a1 * A.X + b1 * A.Y;
      // Line CD represented as a2x + b2y = c2
      double a2 = D.Y - C.Y, b2 = C.X - D.X, c2 = a2 * C.X + b2 * C.Y;

      // Use determinant to figure out if the lines are parallel, and return Nil if so
      double determinant = a1 * b2 - a2 * b1;
      if (Abs (determinant) < 0.0000000001) return Point2.Nil;    
      return new ((b2 * c1 - b1 * c2) / determinant, (a1 * c2 - a2 * c1) / determinant);
   }

   /// <summary>Return the intersection Point2 of two line segments A-B and C-D</summary>
   /// <param name="A">First Point2 on line 1</param>
   /// <param name="B">Second Point2 on line 1</param>
   /// <param name="C">First Point2 on line 2</param>
   /// <param name="D">Second Point2 on line 2</param>
   /// This treats the lines A-B and C-D as finite segments, and not as infinite lines.
   /// If the lines are parallel (do not intersect), this returns Point2.Nil. 
   /// If the intersection point lies outside the span of either of the lines, this returns Point2.Nil
   public static Point2 LineSegXLineSeg (Point2 A, Point2 B, Point2 C, Point2 D) {
      var pt = LineXLine (A, B, C, D);
      if (!pt.IsNil) {
         double lie = pt.GetLieOn (A, B);
         if (lie is > -0.0000000001 and < 1.0000000001) {
            lie = pt.GetLieOn (C, D);
            if (lie is > -0.0000000001 and < 1.0000000001) return pt;
         }
      }
      return Point2.Nil;
   }
}
#endregion
