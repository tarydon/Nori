// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Geo.cs
// ║║║║╬║╔╣║ Implements the GEO class, containing a number of geometry primitives
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Geo ----------------------------------------------------------------------------------
/// <summary>The Geo class contains a number of core Geoetry functions</summary>
public static class Geo {
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
