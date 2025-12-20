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
   /// <param name="c1">Center point of first circle</param>
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
      if (R.IsZero ()) return buffer[..0];     // Circles are concentric
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
      if (Abs (f3Sq) < 0.0000000001) { buffer[0] = (Point2)(v1 + v2); return buffer[..1]; }

      // If the square of the 3rd term multiplicand is negative, there are no solutions
      // (the circles don't intersect at all), and we return zero intersection points
      if (f3Sq < 0) return buffer[..0];

      // Otherwise, this is the most general case and there are 2 intersection points that
      // we return here as (v1 + v2) + v3, and (v1 + v2) - v3:
      double f3 = 0.5 * Sqrt (f3Sq);
      Vector2 v3 = new ((c2.Y - c1.Y) * f3, (c1.X - c2.X) * f3);
      buffer[0] = (Point2)(v1 + v2 + v3); buffer[1] = (Point2)(v1 + v2 - v3);
      // Return a slice of the input buffer with exactly 2 results in it
      return buffer[..2];
   }

   /// <summary>Returns the intersections between a circle and a line (0, 1, or 2 points)</summary>
   /// <param name="cen">Center point of circle</param>
   /// <param name="rad">Radius of the circle</param>
   /// <param name="p1">First point on the line</param>
   /// <param name="p2">Second point on the line</param>
   /// <param name="buffer">A buffer with space for at least 2 points</param>
   /// <returns>A slice of the same buffer containing 0, 1 or 2 points</returns>
   /// This routine uses a non-trignometric (analytical geometry) method to compute the two
   /// centers without any trignometric functions, and is very performant. To avoid allocating
   /// short-lived arrays to hold the results, this routine takes a Span and returns a subset
   /// of that span (see CircleXCircle for more details on this).
   public static ReadOnlySpan<Point2> CircleXLine (Point2 cen, double rad, Point2 p1, Point2 p2, Span<Point2> buffer) {
      // Try to frame this as a quadratic that has 0, 1 or 2 unique solutions
      double dx = p2.X - p1.X, dy = p2.Y - p1.Y, A = dx * dx + dy * dy;
      if (A.IsZero ()) return [];
      double vx = p1.X - cen.X, vy = p1.Y - cen.Y;
      double B = 2 * (dx * vx + dy * vy);
      double C = vx * vx + vy * vy - rad * rad;
      double det = B * B - 4 * A * C;

      // Handle the cases for 1, 2 and 0 solutions below:
      if (det.IsZero ()) {
         double t = -B / (2 * A);
         buffer[0] = new (p1.X + t * dx, p1.Y + t * dy);
         return buffer[..1];
      } else if (det > 0) {
         A *= 2;
         double sdet = Sqrt (det);
         double t = (-B + sdet) / A;
         buffer[0] = new (p1.X + t * dx, p1.Y + t * dy);
         t = (-B - sdet) / A;
         buffer[1] = new (p1.X + t * dx, p1.Y + t * dy);
         return buffer[..2];
      } else
         return [];
   }

   /// <summary>Returns the intersection between a circle and line that is closest to the given point</summary>
   /// If the circle does not intersect the line at all, this returns Point2.Nil. Otherwise, it
   /// returns the closer of the two intersection points between the circle and the line (to the
   /// given reference point 'close')
   public static Point2 CircleXLineClosest (Point2 cen, double rad, Point2 p1, Point2 p2, Point2 close) {
      Span<Point2> buffer = stackalloc Point2[2];
      var pts = CircleXLine (cen, rad, p1, p2, buffer);
      return pts.Length switch {
         0 => Point2.Nil,
         1 => pts[0],
         _ => pts[0].DistToSq (close) < pts[1].DistToSq (close) ? pts[0] : pts[1]
      };
   }

   /// <summary>Returns a tuple (center, radius) which forms a circle tangential to lines AB, CD, EF</summary>
   /// The pick points specify which bisectors (relative to intersection point of the lines)
   /// to use to compute the centre. If pick points don't lie on the corresponding line,
   /// the point is snapped to the nearest point on the corresponding line.
   /// If input lines are invalid, or all 3 lines are parallel or,
   /// bisectors are parallel (Nil, 0) is returned.
   public static (Point2 Center, double Radius) CircleTangentLLL (Point2 a, Point2 b, Point2 c, Point2 d, Point2 e, Point2 f,
                                                                  Point2 pick1, Point2 pick2, Point2 pick3) {
      if (a.EQ (b) || c.EQ (d) || e.EQ (f)) return (Point2.Nil, 0);
      (pick1, pick2, pick3) = (pick1.SnappedToLine (a, b), pick2.SnappedToLine (c, d), pick3.SnappedToLine (e, f));

      var (P1, Q1) = GetBisector (a, b, c, d, pick1, pick2);
      var (P2, Q2) = GetBisector (c, d, e, f, pick2, pick3);
      // If any of the lines are parallel, we try to get an alternate bisector.
      if (P1.IsNil) (P1, Q1) = GetBisector (a, b, e, f, pick1, pick3);
      if (P2.IsNil) (P2, Q2) = GetBisector (a, b, e, f, pick1, pick3);

      var center = LineXLine (P1, Q1, P2, Q2);
      return (center, center.IsNil ? 0 : center.DistToLine (e, f));
   }

   /// <summary> Returns the center of the circle passing through three non-collinear points</summary>
   /// If the points are collinear, this returns Point2.Nil
   public static Point2 Get3PCircle (Point2 a, Point2 b, Point2 c) {
      // Get the midpoints of the sides, and the perpendicular bisector
      // vectors of the two sides
      Point2 mid1 = a.Midpoint (b), mid2 = b.Midpoint (c);
      Vector2 perp1 = (b - a).Perpendicular (), perp2 = (c - b).Perpendicular ();

      // The center is the intersection of these two perpendicular bisectors
      return LineXLine (mid1, mid1 + perp1, mid2, mid2 + perp2);
   }

   /// <summary>Returns the angle-bisector of the lines a-b and c-d</summary>
   /// This returns a tuple (P,Q) where P is the intersection point of the two lines,
   /// and Q is a point on the bisector leading out from that intersection. Since there are
   /// 4 possible bisectors, the points pick1 and pick2 are used to disambiguate.
   /// pick1 should be close to ab, while pick2 is close to cb. The positions of pick1
   /// and pick2 (relative to P) specify which of the 4 bisectors is returned.
   ///
   /// If the lines ab and cd do not intersect, this returns the tuple (Nil, Nil)
   public static (Point2 P, Point2 Q) GetBisector (Point2 a, Point2 b, Point2 c, Point2 d, Point2 pick1, Point2 pick2) {
      Point2 P = LineXLine (a, b, c, d);
      if (P.IsNil) return (P, Point2.Nil);// If lines don't intersect, return invalid result
      // Build direction vectors. Note that these have to be normalized, since
      // we are going to _average_ them below and that will work only if they are
      // the same lengths
      Vector2 v1 = (b - a).Normalized (), v2 = (d - c).Normalized ();
      // Flip direction if pick is "behind" the intersection
      if (P.GetLieOn (a, b) > pick1.GetLieOn (a, b)) v1 = -v1;
      if (P.GetLieOn (c, d) > pick2.GetLieOn (c, d)) v2 = -v2;

      // Returns base point P and a second point in the average direction of v1 and v2
      return (P, P + (v1 + v2));
   }

   /// <summary>Given a Z vector, returns X and Y vectors in that coordinate system</summary>
   /// Of course, there are infinitely many X and Y vectors possible, this just
   /// picks one pair and returns them, such that X * Y = Z
   public static (Vector3 x, Vector3 y) GetXYFromZ (Vector3 z) {
      z = z.Normalized ();
      // Take the least component of this and consider that arbitrarily the x
      double cx = Math.Abs (z.X), cy = Math.Abs (z.Y), cz = Math.Abs (z.Z);
      Vector3 x = Vector3.ZAxis;
      if (cx < cy && cx < cz) {
         x = Vector3.XAxis;
      } else if (cy < cz) {
         x = Vector3.YAxis;
      } 
      // Of course, this choice of x is not really likely to be correct, so we can
      // now compute a 'correct' y by cross multiplication. Then, we can recompute
      // x again by cross-multiplication
      Vector3 y = (z * x).Normalized (); x = (y * z).Normalized ();
      return (x, y);
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
