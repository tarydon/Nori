// ────── ╔╗
// ╔═╦╦═╦╦╬╣ ConvexHull.cs
// ║║║║╬║╔╣║ Implements algorithms to compute the convex hull of a set of 2D points.
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────

namespace Nori;

public static class ConvexHull {
   /// <summary>Computes the "convex hull" of the polyline formed from the given set of points.</summary>
   /// The alternative Andrew's monotone chain algorithm is clearly better. Remove this after testing/comparison.
   public static IEnumerable<Point2> ComputeByGrahamScan (IList<Point2> pts) {
      if (pts.Count <= 3) return pts;

      Point2 p0 = pts.Min (RightMostLowestPointComparer);

      Stack<Point2> hull = new Stack<Point2> (); // Represents the vertices of the resulting convex hull.
      pts = pts.OrderBy (pt => p0.AngleTo (pt)).ThenBy (pt => p0.DistTo (pt)).ToList ();

      // Remove the duplicate points
      for (int i = pts.Count - 1; i >= 1; i--) {
         int j = (i + 1) % pts.Count;
         if (pts[i].EQ (pts[j])) { pts.RemoveAt (i); continue; }
         double a1 = p0.AngleTo (pts[i]), a2 = p0.AngleTo (pts[j]);
         if (a1.EQ (a2))
            pts.RemoveAt (p0.DistTo (pts[i]) < p0.DistTo (pts[j]) ? i : j);
      }

      // If 3 points form a U-notch, remove the middle point
      for (int i = pts.Count - 1; i >= 1; i--) {
         int j = (i + 1) % pts.Count, k = (i + 2) % pts.Count;
         Point2 pa = pts[i], pb = pts[j], pc = pts[k];
         if (pa.DistToLineSq (pb, pc) > Lib.EpsilonSq) continue;
         if ((pb - pa).Opposing (pc - pb)) pts.RemoveAt (j);
      }

      for (int i = 0; i < pts.Count;) {
         Point2 pt = pts[i];
         // if we don't yet have 2 points in the hull, just keep adding points until we do
         if (hull.Count < 2) {
            hull.Push (pt); i++; continue;
         }

         Point2 end = hull.Pop (), start = hull.Peek (); // Get the last 'line' that was added
         int side = pt.ExactSide (start, end);           // And see which side of this line the incoming Point2 lies
         if (side == 1) {                    // If the new Point2 is to the left(strictly) of the line,
            hull.Push (end);                 // then push the endpoint Point2 back in
            hull.Push (pt); i++;             // and push the incoming Point2 (this is a left turn)
         } else if (side == 0) {             // If the Point2 lies on the line, take the Point2 which is farther away for 'second' Point2
            hull.Push (pt.DistTo (start) > end.DistTo (start) ? pt : end);
            i++;
         }
         // If side == -1, the topmost Point2 is not replaced back in AND there is no indexing increment.
      }

      if (hull.Count > 2) {
         // A special case where the last Point2 may be collinear with p0 and the second-last Point2 and hence is redundant.
         Point2 top = hull.Pop ();
         if (p0.Side (hull.Peek (), top) != 0) hull.Push (top);
      }
      return hull.Reverse ();                // Reversal is needed because of the stack action
   }

   /// <summary>Computes the convex hull of a set of points using Andrew's monotone chain algorithm.</summary>
   /// Andrew's Monotone Chain Algorithm which is O(N logN). Simpler and does not require trigonometric functions.
   public static List<Point2> Compute (IReadOnlyList<Point2> points) {
      if (points.Count <= 2)
         return [..points];

      if (points.Count > 100)
         points = PreTrimPts (points) ?? points;

      List<Point2> pts = new List<Point2> (points.Count), lower = new List<Point2> ();
      foreach (var p in points.OrderBy (p => p.X).ThenBy (p => p.Y)) {
         pts.Add (p);
         while (lower.Count >= 2 && Cross (lower[^2], lower[^1], p) <= 0)
            lower.RemoveAt (lower.Count - 1);
         lower.Add (p);
      }

      var upper = new List<Point2> ();
      for (int i = pts.Count - 1; i >= 0; i--) {
         var p = pts[i];
         while (upper.Count >= 2 && Cross (upper[^2], upper[^1], p) <= 0)
            upper.RemoveAt (upper.Count - 1);
         upper.Add (p);
      }

      // Remove last element of each list because it is the starting point of the other list.
      lower.RemoveAt (lower.Count - 1);
      upper.RemoveAt (upper.Count - 1);

      lower.AddRange (upper);
      return lower;
   }

   /// <summary>Computes the convex hull of a polyline</summary>
   /// <param name="poly">The polyline</param>
   /// <param name="isSimplePolygon">Pass true if it is known that the polyline is a cleaned-up 
   /// simple polygon (no self-intersections). In such cases, convex-hull can be computed faster.</param>
   /// Real world 2D part contours are usually guaranteed to be simple polygons.
   public static List<Point2> Compute (Poly poly, bool isSimplePolygon) {
      List<Point2> pts = new List<Point2> (poly.Count);
      poly.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      return isSimplePolygon ? ComputeForSimplePolygon (pts) : Compute (pts);
   }

   /// <summary> Computes the convex hull of a simple polygon using Melkman's algorithm in O(N) time. </summary>
   /// For a simple polygon, which does not have self-intersections, degenerate points or collinear points,
   /// Melkman's algorithm can reliably compute the convex-hull in linear time.
   public static List<Point2> ComputeForSimplePolygon (IReadOnlyList<Point2> polygon) {
      int n = polygon.Count;
      if (n <= 2) return [..polygon];

      // Get the first three points
      Point2 p0 = polygon[0], p1 = polygon[1], p2 = polygon[2];

      // Check the winding direction for the first three points
      double c = Cross (p0, p1, p2);
      if (c == 0)
         return Compute (polygon); // Collinear: fall back to Andrew

      // Set up initial deque so that it forms the convex hull of first three points
      Span<Point2> dq = n <= 256 ? stackalloc Point2[2 * (n + 1)] : new Point2[2 * (n + 1)];
      int bot = n, top = bot + 3;

      if (c > 0) {
         // CCW: p2, p0, p1, p2
         dq[bot] = p2;
         dq[bot + 1] = p0;
         dq[bot + 2] = p1;
         dq[bot + 3] = p2;
      } else {
         // CW: reverse middle two
         dq[bot] = p2;
         dq[bot + 1] = p1;
         dq[bot + 2] = p0;
         dq[bot + 3] = p2;
      }

      for (int i = 3; i < n; i++) {
         Point2 p = polygon[i];
         // If p is inside the current hull, skip it
         if (Cross (dq[bot], dq[bot + 1], p) > 0 && Cross (dq[top - 1], dq[top], p) > 0)
            continue;
         // Remove from front while p is not to the left of front edge
         while (Cross (dq[bot], dq[bot + 1], p) <= 0)
            bot++;
         // Insert new point at front
         dq[--bot] = p;
         // Remove from back while p is not to the left of back edge
         while (Cross (dq[top - 1], dq[top], p) <= 0)
            top--;
         // Insert new point at back
         dq[++top] = p;
      }

      // Repeat the loop one more time for the first point to close the hull
      Point2 pFirst = polygon[0];
      if (Cross (dq[bot], dq[bot + 1], pFirst) <= 0 || Cross (dq[top - 1], dq[top], pFirst) <= 0) {
         while (Cross (dq[bot], dq[bot + 1], pFirst) <= 0)
            bot++;
         dq[--bot] = pFirst;
         while (Cross (dq[top - 1], dq[top], pFirst) <= 0)
            top--;
         dq[++top] = pFirst;
      }

      // Extract hull from deque (from bot to top)
      var hull = new List<Point2> (top - bot + 1);
      for (int i = bot; i <= top; i++)
         hull.Add (dq[i]);

      // Remove possible duplicate start/end
      if (hull.Count > 1 && hull[0].EQ (hull[^1]))
         hull.RemoveAt (hull.Count - 1);

      return hull;
   }

   #region Helper methods --------------------------------------------
   /// <summary> Cross product of vectors (a-to-b) x (b-to-c). Positive if turn at b is counter clockwise,
   /// negative for clockwise, zero if collinear. </summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static double Cross (Point2 a, Point2 b, Point2 c) {
      var abx = b.X - a.X;
      var aby = b.Y - a.Y;
      var acx = c.X - a.X;
      var acy = c.Y - a.Y;
      return abx * acy - aby * acx;
   }

   static int ExactSide (this Point2 pt, Point2 a, Point2 b) {
      double cross = (b.X - a.X) * (pt.Y - a.Y) - (pt.X - a.X) * (b.Y - a.Y);
      if (cross < 0) return -1;
      return cross > 0 ? 1 : 0;
   }

   static readonly IComparer<Point2> RightMostLowestPointComparer = Comparer<Point2>.Create (RightMostLowestPointComparison);
   static int RightMostLowestPointComparison (Point2 a, Point2 b) {
      if (a.Y != b.Y) return a.Y < b.Y ? -1 : 1; // least Y first
      if (a.X != b.X) return a.X < b.X ? 1 : -1; // then maximum X
      return 0;
   }

   // Helper: point-in-convex-polygon test (polygon is small: up to 4 vertices)
   static bool IsOutside (Point2 pt, List<Point2> convexPoly) {
      int n = convexPoly.Count;
      for (int i = 0; i < n; i++) {
         // treat right side as outside
         int side = pt.ExactSide (convexPoly[i], convexPoly[(i + 1) % n]);
         if (side <= 0)
            return true; // outside or on edge
      }
      return false; // inside
   }

   // A pretrim heuristic to trim away points that are definitely inside the convex hull and will not
   // be part of the hull. This can speed up hull computation considerably for large point sets with dense clusters.
   // Returns null if no effective trimming is possible.
   //
   // The heuristic constructs a convex polygon from the extreme points (left-most, right-most, bottom-most, top-most)
   // and removes all points that are inside this polygon.
   static List<Point2>? PreTrimPts (IReadOnlyList<Point2> pts) {
      // Find extreme points: left-most, right-most, bottom-most, top-most
      Point2 left = pts[0], right = pts[0], bottom = pts[0], top = pts[0];

      foreach (var p in pts) {
         if (p.X < left.X || (p.X == left.X && p.Y < left.Y)) left = p; // left then bottom
         if (p.X > right.X || (p.X == right.X && p.Y > right.Y)) right = p; // right then top
         if (p.Y < bottom.Y || (p.Y == bottom.Y && p.X > bottom.X)) bottom = p; // bottom then right
         if (p.Y > top.Y || (p.Y == top.Y && p.X < top.X)) top = p; // top then left
      }

      // Build polygon from these extreme points in CCW order,
      // removing duplicates while preserving order.
      List<Point2> extremes = [left, bottom, right, top];

      // Remove consecutive duplicates
      for (int i = extremes.Count - 1; i > 0; i--) {
         if (extremes[i].EQ (extremes[i - 1]))
            extremes.RemoveAt (i);
      }
      // If first and last are same after cleanup, remove last
      if (extremes.Count > 1 && extremes[0].EQ (extremes[^1]))
         extremes.RemoveAt (extremes.Count - 1);

      // If degenerates to a line or point, no effective pre-trimming possible
      if (extremes.Count < 3)
         return null;

      // Collect only points that are outside the polygon
      var result = new List<Point2> (Math.Max (8, (int)Math.Log10 (pts.Count)));
      foreach (var p in pts)
         if (IsOutside (p, extremes)) // IsOutside is guaranteed to include extremes also.
            result.Add (p);

      return result;
   }
   #endregion
}