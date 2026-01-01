// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MES.cs
// ║║║║╬║╔╣║ Implements minimum enclosing circle and sphere
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static Nori.Lib;
namespace Nori;

#region struct MinCircle ---------------------------------------------------------------------------
/// <summary>Implements a minimum circle for a given set of points.</summary>
public readonly struct MinCircle {
   // Private constructor to construct a circle from radius and center.
   MinCircle (double radius, Point2 center) { 
      Radius = radius; 
      Center = center; 
      RSqr = radius > 0 ? (radius * radius) + Epsilon : 0;
   }

   /// <summary>Center of the circle.</summary>
   readonly public Point2 Center;
   /// <summary>Radius of the circle.</summary>
   readonly public double Radius;
   /// <summary>Is this a valid circle?</summary>
   public bool OK => Radius > Epsilon;
   /// <summary>Checks if a circle contains a given point </summary>
   public bool Contains (in Point2 pt) => OK && Center.DistToSq (pt) < RSqr;
   /// <summary>Checks if a circle contains a set of points.</summary>
   /// Like Contains (Point2), it avoid Sqrt for better performance.
   public bool Contains (ReadOnlySpan<Point2> pts) {
      if (!OK) return false;
      for (int i = 0; i < pts.Length; i++)
         if (Center.DistToSq (pts[i]) > RSqr)
            return false;
      return true;
   }

   // Is this circle equal the other circle?
   public bool EQ (in MinCircle other) => Radius.EQ (other.Radius) && Center.EQ (other.Center);

   public override string ToString () => $"{Radius.R6 ()}, {Center}";

   /// <summary>Constructs a mimimum-enclosing-cicle from a given set of points.</summary>
   /// Recursively computes the minimum enclosing circle in O(n) expected time.
   /// https://en.wikipedia.org/wiki/Smallest-circle_problem
   /// <code>
   /// Point2[] points = [(100, 200), (400, 300), (200, 500), (100, 0)];
   /// MinCircle circle = MinCircle.From (points);
   /// </code>
   /// <param name="pts">The input points</param>
   public static MinCircle From (ReadOnlySpan<Point2> pts) {
      return pts.Length <= 3 ? MEC ([], pts) : MEC (pts, []);

      static MinCircle MEC (ReadOnlySpan<Point2> pts, ReadOnlySpan<Point2> outer) {
         MinCircle c = outer.Length switch {
            2 => From (outer[0], outer[1]), // Circle from diameter
            3 => From (outer[0], outer[1], outer[2]), // Possibly Circumcircle
            _ => Nil
         };
         if (pts.Length == 0 || outer.Length == 3) return c;

         for (int i = 0; i < pts.Length; i++) {
            var pt = pts[i];
            if (c.Contains (pt)) continue;
            c = (outer.Length, c.OK) switch {
               (0, _) => MEC (pts[..i], [pt]),
               (1, false) => From (outer[0], pt), // Circle from diameter
               (1, _) => MEC (pts[..i], [outer[0], pt]),
               (2, _) => MEC (pts[..i], [outer[0], outer[1], pt]),
               _ => throw new Exception ("Not expecting this")
            };
         }
         return c;
      }
   }

   /// <summary>Constructs a circle with two endpoints specifying the diameter.</summary>
   public static MinCircle From (Point2 a, Point2 b) => new (a.DistTo (b) * 0.5, (a + b) * 0.5);

   /// <summary>Constructs the 'minimum enclosing circle' from three points</summary>
   public static MinCircle From (Point2 a, Point2 b, Point2 c) {
      // To make computations easier, translate points by vector '-a' so 'a' becomes (0, 0).
      var ba = b - a; var ca = c - a;
      var cen = GetCenter (ba.X, ba.Y, ca.X, ca.Y);
      // Circum-circle from three points
      MinCircle best = new (cen.DistTo (a), cen);
      // Find MEC from pair of points
      ReadOnlySpan<Point2> pts = [a, b, c];
      for (int i = 0; i < pts.Length - 1; i++)
         for (int j = i + 1; j < pts.Length; j++)
            Consider (From (pts[i], pts[j]), pts);
      return best;

      // Considers a candidate circle for being the minimum enclosing circle.
      void Consider (in MinCircle c, ReadOnlySpan<Point2> pts) {
         if ((!best.OK || c.Radius < (best.Radius - Epsilon)) && c.Contains (pts))
            best = c;
      }
      // Computes the center of the circum-circle from three points (with 'a' at origin).
      Point2 GetCenter (double bx, double by, double cx, double cy) {
         var B = bx * bx + by * by;
         var C = cx * cx + cy * cy;
         // Denominator
         var D = 2 * (bx * cy - by * cx);
         return new Point2 (a.X + (cy * B - by * C) / D, a.Y + (bx * C - cx * B) / D);
      }
   }
   /// <summary>The uninitialized 'Nil' circle.</summary>
   public readonly static MinCircle Nil = new (-1, Point2.Nil);
   readonly double RSqr;
}
#endregion

#region struct MinSphere ---------------------------------------------------------------------------
/// <summary>Implements a minimum sphere for a given set of points.</summary>
public readonly struct MinSphere {
   // Private constructor to construct a sphere from radius and center.
   MinSphere (double radius, Point3 center) {
      Radius = radius;
      Center = center;
      RSqr = radius > 0 ? (radius * radius) + Epsilon : 0;
   }

   /// <summary>Center of the sphere.</summary>
   readonly public Point3 Center;
   /// <summary>Radius of the sphere.</summary>
   readonly public double Radius;
   /// <summary>Is this a valid sphere?</summary>
   public bool OK => Radius > Epsilon;
   /// <summary>Checks if a sphere contains a given point.</summary>
   public bool Contains (in Point3 pt) => OK && Center.DistToSq (pt) < RSqr;

   /// <summary>Checks if a sphere contains a set of points.</summary>
   /// Like Contains (Point3), it avoids Sqrt for better performance.
   public bool Contains (ReadOnlySpan<Point3> pts) {
      if (!OK) return false;
      for (int i = 0; i < pts.Length; i++)
         if (Center.DistToSq (pts[i]) > RSqr)
            return false;
      return true;
   }

   /// <summary>Is this sphere equal the other sphere?</summary>
   public bool EQ (in MinSphere other) => Radius.EQ (other.Radius) && Center.EQ (other.Center);

   public override string ToString () => $"{Radius.R6 ()}, {Center}";

   /// <summary>Constructs a mimimum-enclosing-cicle from a given set of points.</summary>
   /// Recursively computes the minimum enclosing sphere in O(n) expected time.
   /// <code>
   /// Point3[] points = [(100, 200, 300), (400, 500, 600), (500, 200, 100), (100, 0, 500)];
   /// MinSphere sphere = MinSphere.From (points);
   /// </code>
   /// <param name="pts">The input points.</param>
   /// This is an implementation of Welzl's algorithm for finding the minimum enclosing sphere.
   /// The input points should be pre-shuffled (or randomized order) for optimal performance.
   static MinSphere Welzl (ReadOnlySpan<Point3> pts) {
      return pts.Length <= 4 ? MES ([], pts) : MES (pts, []);

      static MinSphere MES (ReadOnlySpan<Point3> pts, ReadOnlySpan<Point3> outer) {
         MinSphere s = outer.Length switch {
            2 => From (outer[0], outer[1]), // Sphere from two boundary points
            3 => From (outer[0], outer[1], outer[2]), // Sphere from 3 boundary points
            4 => From (outer[0], outer[1], outer[2], outer[3]), // Possibly circumsphere from boundary points
            _ => Nil
         };
         if (pts.Length == 0 || outer.Length == 4) return s;

         for (int i = 0; i < pts.Length; i++) {
            var pt = pts[i];
            if (s.Contains (pt)) continue;
            s = (outer.Length, s.OK) switch {
               (0, _) => MES (pts[..i], [pt]),
               (1, false) => From (outer[0], pt), // Sphere from diameter points
               (1, _) => MES (pts[..i], [outer[0], pt]),
               (2, false) => From (outer[0], outer[1], pt), // Minimum sphere from 3 points
               (2, _) => MES (pts[..i], [outer[0], outer[1], pt]),
               (3, _) => MES (pts[..i], [outer[0], outer[1], outer[2], pt]),
               _ => throw new Exception ("Not expecting this")
            };
         }
         return s;
      }
   }

   /// <summary>Constructs a minimum-enclosing-circle from a given set of points</summary>
   /// 1. We start off similar to Rittor's approximation by finding two farthest points.
   /// 2. Then we use Welzl's algorithm to compute the exact minimum enclosing sphere. But before that
   ///    we sort the points in decreasing order of distance from one of the farthest points. This will
   ///    ensure we quickly find the boundary points and converge faster.
   /// Compared to a naive Welzl's algorithm, this approach is about 2x - 10x faster in practice.
   public static MinSphere From (IEnumerable<Point3> pts) {
      if (!pts.Any ()) return Nil;

      Point3[] arr = pts.ToArray ();
      Point3 p0 = arr[0];

      // Find the point p1 farthest from p0.
      int id1 = 0;
      double maxD = -1;
      for (int i = 0; i < arr.Length; i++) {
         double d = p0.DistToSq (arr[i]);
         if (d > maxD) {
            maxD = d;
            id1 = i;
         }
      }

      Point3 p1 = arr[id1];
      (arr[0], arr[id1]) = (arr[id1], arr[0]); // Move p1 to front

      // Sort points by decreasing distance from p1 as points farthest from p1 are more likely to be on boundary.
      var tmp = new (Point3 Point, double Dist)[arr.Length - 1];
      for (int i = 1; i < arr.Length; i++)
         tmp[i - 1] = (arr[i], p1.DistToSq (arr[i]));
      Array.Sort (tmp, (x, y) => y.Dist.CompareTo (x.Dist));
      for (int i = 1; i < arr.Length; i++)
         arr[i] = tmp[i - 1].Point;

      return Welzl (arr);
   }

   /// <summary>Constructs a minimum-enclosing-circle from a given set of points</summary>
   /// 1. We start off similar to Rittor's approximation by finding two farthest points.
   /// 2. Then we use Welzl's algorithm to compute the exact minimum enclosing sphere. But before that
   ///    we sort the points in decreasing order of distance from one of the farthest points. This will
   ///    ensure we quickly find the boundary points and converge faster.
   /// Compared to a naive Welzl's algorithm, this approach is about 2x - 10x faster in practice.
   public static MinSphere From2 (IEnumerable<Point3> pts) {
      if (!pts.Any ()) return Nil;

      Point3[] arr = [.. pts.Shuffle ()];

      // Move points with extreme projections on X, Y & Z axes to the front of the array.
      int ixMin = 0, ixMax = 0, iyMin = 0, iyMax = 0, izMin = 0, izMax = 0;
      for (int i = 1; i < arr.Length; i++) {
         var p = arr[i];
         if (p.X < arr[ixMin].X) ixMin = i;
         if (p.X > arr[ixMax].X) ixMax = i;
         if (p.Y < arr[iyMin].Y) iyMin = i;
         if (p.Y > arr[iyMax].Y) iyMax = i;
         if (p.Z < arr[izMin].Z) izMin = i;
         if (p.Z > arr[izMax].Z) izMax = i;
      }

      // Collect unique extreme indices preserving order: Xmin, Xmax, Ymin, Ymax, Zmin, Zmax.
      Span<int> idx = stackalloc int[6];
      int count = 0;
      void AddIdx (Span<int> s, int v) {
         for (int j = 0; j < count; j++)
            if (s[j] == v) return;
         s[count++] = v;
      }
      AddIdx (idx, ixMin);
      AddIdx (idx, ixMax);
      AddIdx (idx, iyMin);
      AddIdx (idx, iyMax);
      AddIdx (idx, izMin);
      AddIdx (idx, izMax);

      int write = 0;
      for (int k = 0; k < count; k++) {
         int src = idx[k];
         if (src != write)
            (arr[write], arr[src]) = (arr[src], arr[write]);
         write++;
      }

      return Welzl (arr);
   }

   /// <summary>Constructs a close-to-minimum enclosing sphere for a given set of points very quickly. (not "the" optimal min. sphere)</summary>
   /// A quick and dirty minimum enclosing sphere (not optimal) using Ritter's algorithm.
   /// Usually within 2% of optimal. On average about 0.9% larger than optimal, but 10x faster to compute.
   public static MinSphere FromQuickApprox (ReadOnlySpan<Point3> pts) {
      
      if (pts.Length == 0) return Nil;
      if (pts.Length == 1) return new MinSphere (0, pts[0]);

      // 1) Pick an arbitrary point p0.
      var p0 = pts[0];

      // 2) Find point p1 farthest from p0.
      int i1 = 0; double maxD = -1;
      for (int i = 0; i < pts.Length; i++) {
         double d = p0.DistToSq (pts[i]);
         if (d > maxD) { maxD = d; i1 = i; }
      }
      var p1 = pts[i1];

      // 3) Find point p2 farthest from p1.
      int i2 = i1; maxD = -1;
      for (int i = 0; i < pts.Length; i++) {
         double d = p1.DistToSq (pts[i]);
         if (d > maxD) { maxD = d; i2 = i; }
      }
      var p2 = pts[i2];

      // 4) Initial sphere: diameter endpoints p1, p2.
      MinSphere s = From (p1, p2);
      var c = s.Center;
      double r = s.Radius, r2 = r * r;

      // 5) Grow sphere to include all points (no shrinking => quick but not optimal).
      for (int i = 0; i < pts.Length; i++) {
         var p = pts[i];
         double d2 = c.DistToSq (p);
         if (d2 <= r2) continue;
         double d = Math.Sqrt (d2);
         // New radius is halfway between center and far point.
         double newR = (r + d) * 0.5;
         // Move center towards the point so that p lies on boundary.
         double k = (newR - r) / d;
         c = new Point3 (
            c.X + (p.X - c.X) * k,
            c.Y + (p.Y - c.Y) * k,
            c.Z + (p.Z - c.Z) * k
         );
         r = newR;
         r2 = r * r;
      }

      return new MinSphere (r, c);
   }

   // Constructs a sphere with two endpoints specifying the diameter.
   public static MinSphere From (Point3 a, Point3 b) => new (a.DistTo (b) * 0.5, (a + b) * 0.5);

   // Constructs a minimum sphere passing through three non-collinear points
   // We first calculate the unique, 'Great Circle' (at equator) of the sphere
   // and then sphere from the circle.
   public static MinSphere From (Point3 a, Point3 b, Point3 c) {
      Vector3 ba = b - a, ca = c - a;
      // Plane axes (u, v, w) in 3D space (x, y, z)
      Vector3 u = ba.Normalized (), w = (u * ca).Normalized (), v = w * u;
      // Transform points from World (X, Y, Z) => Plane (U, V) space.
      Point2 p1 = Point2.Zero, p2 = new (ba.Length, 0), p3 = new (ca.Dot (u), ca.Dot (v));
      // Compute MEC in (U, V) space
      var C = MinCircle.From (p1, p2, p3);
      // Loft center from Plane (U, V) => World (X, Y, Z) space
      return new (C.Radius, a + u * C.Center.X + v * C.Center.Y);
   }

   /// <summary>Constructs the unique minimum enclosing sphere that passes through four specified points in three-dimensional space.</summary>
   /// <remarks>This routine falls back to three and lower point variant when two or more points are coincident. 
   /// It returns and undefined sphere (OK == false) when a sphere cannot be constructed.</remarks>   
   public static MinSphere From (Point3 a, Point3 b, Point3 c, Point3 d) {
      // Circumsphere from four points
      // To make computations easier, translate points by vector '-a' so 'a' becomes (0, 0, 0).
      var ba = b - a; var ca = c - a; var da = d - a;
      var cen = GetCenter (ba.X, ba.Y, ba.Z, ca.X, ca.Y, ca.Z, da.X, da.Y, da.Z);
      MinSphere best = new (cen.DistTo (a), cen);
      // Find MES from pair of points
      ReadOnlySpan<Point3> pts = [a, b, c, d];
      for (int i = 0; i < pts.Length - 1; i++)
         for (int j = i + 1; j < pts.Length; j++)
            Consider (From (pts[i], pts[j]), pts);
      // Find MES from triplet of points
      for (int i = 0; i < pts.Length - 2; i++)
         for (int j = i + 1; j < pts.Length - 1; j++)
            for (int k = j + 1; k < pts.Length; k++)
               Consider (From (pts[i], pts[j], pts[k]), pts);
      return best;

      // Considers a candidate sphere for being the minimum enclosing sphere.
      void Consider (in MinSphere s, ReadOnlySpan<Point3> points) {
         if ((!best.OK || s.Radius < (best.Radius - Epsilon)) && s.Contains (points))
            best = s;
      }
      // Computes the center of the circumsphere from four points (with 'a' at origin).
      Point3 GetCenter (double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz) {
         var A = ax * ax + ay * ay + az * az;
         var B = bx * bx + by * by + bz * bz;
         var C = cx * cx + cy * cy + cz * cz;
         // Denominator
         var D = 2 * (ax * (by * cz - cy * bz) - ay * (bx * cz - cx * bz) + az * (bx * cy - by * cx));
         // Cramer's determinants after substitution
         var Dx = A * (by * cz - cy * bz) - ay * (B * cz - C * bz) + az * (B * cy - C * by);
         var Dy = ax * (B * cz - C * bz) - A * (bx * cz - cx * bz) + az * (bx * C - B * cx);
         var Dz = ax * (by * C - cy * B) - ay * (bx * C - cx * B) + A * (bx * cy - by * cx);
         return new (a.X + Dx / D, a.Y + Dy / D, a.Z + Dz / D);
      }
   }
   /// <summary>The uninitialized 'Nil' sphere.</summary>
   public readonly static MinSphere Nil = new (-1, Point3.Nil);
   readonly double RSqr;
}
#endregion
