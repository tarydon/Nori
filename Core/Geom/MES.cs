// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MEs.cs
// ║║║║╬║╔╣║ Implements minimum enclosing circle and sphere
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static Nori.Lib;
namespace Nori;

#region struct MinCircle ---------------------------------------------------------------------------
// A minimal circle with a radius and center.
public readonly struct MinCircle (double radius, Point2 center) {
   // Center of the circle.
   readonly public Point2 Center = center;
   // Radius of the circle.
   readonly public double Radius = radius;
   // Is this a valid circle?
   public bool OK => Radius > Epsilon;
   // Checks if a circle contains a given point
   public bool Contains (in Point2 pt) => OK && Center.DistToSq (pt) < RSqr;
   
   // Checks if a circle contains a set of points. 
   // Like Contains (Point2), it avoid Sqrt for better performance.
   public bool Contains (ReadOnlySpan<Point2> pts) {
      if (!OK) return false;
      for (int i = 0; i < pts.Length; i++)
         if (Center.DistToSq (pts[i]) > RSqr)
            return false;
      return true;
   }

   // Is this circle equal the other circle?
   public bool EQ (in MinCircle other) => Radius.EQ (other.Radius) && Center.EQ (other.Center);

   public override string ToString () => $"({Radius.R6 ()}, {Center})";

   // Constructs a minimum-enclosing-cicle from a given set of points.
   public static MinCircle From (ReadOnlySpan<Point2> pts) {
      return MEC (pts, []);

      static MinCircle MEC (ReadOnlySpan<Point2> pts, ReadOnlySpan<Point2> outer) {
         MinCircle c = outer.Length switch {
            2 => From (outer[0], outer[1]), // Circle from diameter
            3 => From (outer[0], outer[1], outer[2]), // Circum-circle
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
               (2, false) => From (outer[0], outer[1], pt), // Circum-circle
               (2, _) => MEC (pts[..i], [outer[0], outer[1], pt]),
               _ => throw new Exception ("Not expecting this")
            };
         }
         return c;
      }
   }

   // Constructs a circle with endpoints specifying the diameter.
   public static MinCircle From (Point2 a, Point2 b) =>
      new (a.DistTo (b) / 2, new ((a.X + b.X) / 2, (a.Y + b.Y) / 2));

   // Constructs the circumcircle from three non-collinear points
   internal static MinCircle From (Point2 a, Point2 b, Point2 c) {
      // To make computations easier, translate points by vector '-a' so 'a' becomes (0, 0).
      var ba = b - a; var ca = c - a;
      var cen = GetCenter (ba.X, ba.Y, ca.X, ca.Y);
      return new (cen.DistTo (a), cen);

      Point2 GetCenter (double bx, double by, double cx, double cy) {
         var B = bx * bx + by * by;
         var C = cx * cx + cy * cy;
         // Denominator
         var D = bx * cy - by * cx;
         return new Point2 (a.X + (cy * B - by * C) / (2 * D), a.Y + (bx * C - cx * B) / (2 * D));
      }
   }

   public readonly static MinCircle Nil = new (-1, Point2.Nil);
   readonly double RSqr = radius * radius + Epsilon;
}
#endregion

#region struct MinSphere ---------------------------------------------------------------------------
// A minimal sphere with a radius and center.
public readonly struct MinSphere (double radius, Point3 center) {
   // Center of the sphere.
   readonly public Point3 Center = center;
   // Radius of the sphere.
   readonly public double Radius = radius;
   // Is this a valid sphere?
   public bool OK => Radius > Epsilon;
   // Checks if a sphere contains a given point
   public bool Contains (in Point3 pt) => OK && Center.DistToSq (pt) < RSqr;

   // Checks if a sphere contains a set of points. 
   // Like Contains (Point3), it avoids Sqrt for better performance.
   public bool Contains (ReadOnlySpan<Point3> pts) {
      if (!OK) return false;
      for (int i = 0; i < pts.Length; i++)
         if (Center.DistToSq (pts[i]) > RSqr)
            return false;
      return true;
   }

   // Is this sphere equal the other sphere?
   public bool EQ (in MinSphere other) => Radius.EQ (other.Radius) && Center.EQ (other.Center);

   public override string ToString () => $"({Radius.R6 ()}, {Center})";

   // Constructs a mimimum-enclosing-cicle from a given set of points.
   public static MinSphere From (ReadOnlySpan<Point3> pts) {
      return MES (pts, []);

      static MinSphere MES (ReadOnlySpan<Point3> pts, ReadOnlySpan<Point3> outer) {
         MinSphere s = outer.Length switch {
            2 => From (outer[0], outer[1]), // Sphere from two boundary points
            3 => From (outer[0], outer[1], outer[2]), // Sphere from 3 boundary points
            4 => From (outer[0], outer[1], outer[2], outer[3]), // Circumsphere from boundary points
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
               (3, false) => From (outer[0], outer[1], outer[2], pt), // Circum-sphere 
               (3, _) => MES (pts[..i], [outer[0], outer[1], outer[2], pt]),
               _ => throw new Exception ("Not expecting this")
            };
         }
         return s;
      }
   }

   // Constructs a sphere with two endpoints specifying the diameter.
   public static MinSphere From (Point3 a, Point3 b) =>
      new (a.DistTo (b) / 2, new ((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2));

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
      var C = MEC ([p1, p2, p3]);
      // Loft center from Plane (U, V) => World (X, Y, Z) space
      return new (C.Radius, a + u * C.Center.X + v * C.Center.Y);

      // Searches for the Minimum Enclosing Circle (MEC) for the given set of points.
      static MinCircle MEC (ReadOnlySpan<Point2> pts) {
         if (pts.Length > 3) throw new Exception ("Not expecting this");
         MinCircle best = MinCircle.Nil;
         // Find MEC from pair of points
         for (int i = 0; i < pts.Length - 1; i++)
            for (int j = i + 1; j < pts.Length; j++)
               Consider (MinCircle.From (pts[i], pts[j]), pts);
         
         Consider (MinCircle.From (pts[0], pts[1], pts[2]), pts);
         return best;

         void Consider (in MinCircle c, ReadOnlySpan<Point2> pts) {
            if ((!best.OK || c.Radius < (best.Radius + Epsilon)) && c.Contains (pts)) {
               best = c;
            }
         }
      }
   }

   // Constructs the circumsphere from four non-coplanar points
   static MinSphere From (Point3 a, Point3 b, Point3 c, Point3 d) {
      // To make computations easier, translate points by vector '-a' so 'a' becomes (0, 0, 0).
      var ba = b - a; var ca = c - a; var da = d - a;
      var cen = GetCenter (ba.X, ba.Y, ba.Z, ca.X, ca.Y, ca.Z, da.X, da.Y, da.Z);
      return new (cen.DistTo (a), cen);

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

   public readonly static MinSphere Nil = new (-1, Point3.Nil);
   readonly double RSqr = (radius * radius) + Epsilon;
}
#endregion
