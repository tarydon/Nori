﻿// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Point.cs
// ║║║║╬║╔╣║ Various point classes (in 2D and 3D)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region struct Point2 ------------------------------------------------------------------------------
/// <summary>Point in 2 dimensions, 64-bit double components</summary>
public readonly struct Point2 : IEQuable<Point2> {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Point2 given the X and Y ordinates</summary>
   public Point2 (double x, double y) => (X, Y) = (x, y);

   // Properties ---------------------------------------------------------------
   /// <summary>X ordinate of the Point2</summary>
   public readonly double X;
   /// <summary>Y ordinate of the Point2</summary>
   public readonly double Y;

   /// <summary>The origin in 2D space</summary>
   public static readonly Point2 Zero = new (0, 0);
   /// <summary>The 'Nil' point</summary>
   public static readonly Point2 Nil = new (double.NaN, double.NaN);

   /// <summary>Returns true if this is a Nil Point2</summary>
   public bool IsNil => double.IsNaN (X) || double.IsNaN (Y);

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the heading between this point and the given point pt</summary>
   /// The heading is like the compass heading so 0 is EAST, 90 is NORTH etc.
   /// So a point lying exactly to the right of this given point wil have a heading
   /// of 0
   public double AngleTo (Point2 b) {
      double dx = b.X - X, dy = b.Y - Y;
      if (dx == 0 && dy == 0) return 0;
      return Lib.NormalizeAngle (Atan2 (dy, dx));
   }

   public Point2 CardinalMoved (double r, EDir dir) 
      => dir switch {
            EDir.E => new (X + r, Y), EDir.W => new (X - r, Y),
            EDir.S => new (X, Y - r), _ => new (X, Y + r)
         };

   /// <summary>Returns a point clamped to a given range</summary>
   public readonly Point2 Clamped (Bound2 bound)
      => new (bound.X.Clamp (X), bound.Y.Clamp (Y));

   /// <summary>Distance between this point and another</summary>
   public double DistTo (Point2 b) => Sqrt (DistToSq (b));
   /// <summary>Returns the perpendicular distance between this point and the inifinite line a..b</summary>
   public double DistToLine (Point2 a, Point2 b) => DistTo (SnappedToLine (a, b));
   /// <summary>Returns the closest distance between this point and the finite line segment a..b</summary>
   public double DistToLineSeg (Point2 a, Point2 b) => DistTo (SnappedToLineSeg (a, b));
   /// <summary>Square of the distance between this point and another</summary>
   public double DistToSq (Point2 b) { double dx = b.X - X, dy = b.Y - Y; return dx * dx + dy * dy; }

   /// <summary>Compares two points are equal to within EPSILON</summary>
   public bool EQ (Point2 b) => X.EQ (b.X) && Y.EQ (b.Y);

   /// <summary>Gets the lie of this point on the given line segment a..b</summary>
   /// This is accurate only if the point actually lies on the infinite line through a..b
   public double GetLieOn (Point2 a, Point2 b) {
      double dx = b.X - a.X, dy = b.Y - a.Y;
      if (Abs (dx) > Abs (dy)) return (X - a.X) / dx;
      else return (Y - a.Y) / dy;
   }

   /// <summary>Returns true if this point lies to the left of the given line a..b</summary>
   /// If the point is ON the line (to within epsilon), this return false
   public bool LeftOf (Point2 a, Point2 b) => Side (a, b) > 0;

   /// <summary>Returns the reflection of this point along an arbitrary mirror line</summary>
   public Point2 Mirrored (Point2 a, Point2 b) {
      var cen = SnappedToLine (a, b);
      return new (2 * cen.X - X, 2 * cen.Y - Y);
   }

   /// <summary>Returns the point lying halfway between this point and another</summary>
   public Point2 Midpoint (Point2 b) => new ((X + b.X) / 2, (Y + b.Y) / 2);

   /// <summary>Returns a point translated by the given dx, dy</summary>
   public Point2 Moved (double dx, double dy) => new (X + dx, Y + dy);

   /// <summary>Returns the point at a given polar (r-theta) distance from this one</summary>
   public Point2 Polar (double r, double theta) {
      var (sin, cos) = SinCos (theta);
      return new (X + r * cos, Y + r * sin);
   }

   /// <summary>Returns the point with ordinates rounded off to 6 decimals</summary>
   public Point2 R6 () => new (X.R6 (), Y.R6 ());

   /// <summary>Rotate a point about the origin, by the given angle (in radians)</summary>
   public Point2 Rotated (double theta) {
      var (sin, cos) = SinCos (theta);
      return new (X * cos - Y * sin, X * sin + Y * cos);
   }
   /// <summary>Rotate a point about an arbitrary center point, by the given angle (in radians)</summary>
   public Point2 Rotated (Point2 c, double theta)
      => Moved (-c.X, -c.Y).Rotated (theta).Moved (c.X, c.Y);

   /// <summary>Scales the point about the origin</summary>
   public readonly Point2 Scaled (double scale) => new (X * scale, Y * scale);

   /// <summary>Scales the point about an arbitrary center of scaling</summary>
   public Point2 Scaled (Point2 cen, double fScale) {
      double x = (X - cen.X) * fScale, y = (Y - cen.Y) * fScale;
      return new (x + cen.X, y + cen.Y);
   }

   /// <summary>Returns +1 (LEFT), 0 (ON) or -1 (RIGHT) depending on whether the point lies on the line a..b (to within epsilon)</summary>
   public int Side (Point2 a, Point2 b) {
      double cross = (b.X - a.X) * (Y - a.Y) - (X - a.X) * (b.Y - a.Y);
      if (cross > Lib.Epsilon) return 1;
      if (cross < -Lib.Epsilon) return -1;
      return 0;
   }

   /// <summary>Returns the closest point on the given line a..b</summary>
   /// If the points a and b are the same, this just returns a
   public Point2 SnappedToLine (Point2 a, Point2 b) => SnapHelper (a, b, false);
   /// <summary>Returns the closest point to the given _finite_ line segment a..b</summary>
   public Point2 SnappedToLineSeg (Point2 a, Point2 b) => SnapHelper (a, b, true);

   /// <summary>A copy of this point, with just the X ordinate changed</summary>
   public Point2 WithX (double x) => new (x, Y);
   /// <summary>A copy of this point, with just the Y ordinate changed</summary>
   public Point2 WithY (double y) => new (X, y);

   // Operators ----------------------------------------------------------------
   /// <summary>Returns the displaced point got by adding a Vector2 to a Point2</summary>
   public static Point2 operator + (Point2 a, Vector2 b) => new (a.X + b.X, a.Y + b.Y);
   /// <summary>Returns the displaced point got by subtracting a Vector2 from a Point2</summary>
   public static Point2 operator - (Point2 a, Vector2 b) => new (a.X - b.X, a.Y - b.Y);
   /// <summary>Returns the Vector2 (displacement) between two points</summary>
   public static Vector2 operator - (Point2 a, Point2 b) => new (a.X - b.X, a.Y - b.Y);
   /// <summary>Scales a Point2 uniformly by a scalar</summary>
   public static Point2 operator * (Point2 a, double f) => new (a.X * f, a.Y * f);

   /// <summary>Convert a Point2 to a Vector2</summary>
   public static explicit operator Vector2 (Point2 a) => new (a.X, a.Y);

   // Implementation -----------------------------------------------------------
   // Helper used by SnappedToLine and SnappedToLineSeg
   Point2 SnapHelper (Point2 a, Point2 b, bool clamp) {
      var (dx, dy) = (b.X - a.X, b.Y - a.Y);
      double scale = 1 / (dx * dx + dy * dy);
      if (double.IsInfinity (scale)) return a;
      // Use the parametric form of the line equation, and compute
      // the 'parameter t' of the closest point
      double t = ((X - a.X) * dx + (Y - a.Y) * dy) * scale;
      if (clamp) t = t.Clamp ();
      return new (a.X + t * dx, a.Y + t * dy);
   }

   public override string ToString () => $"({X.S6 ()},{Y.S6 ()})";
}
#endregion

#region struct Point3 ------------------------------------------------------------------------------
/// <summary>Point in 2 dimensions, 64-bit double components</summary>
public readonly struct Point3 : IEquatable<Point3> {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Point3 given the X, Y, Z, ordinates</summary>
   public Point3 (double x, double y, double z) => (X, Y, Z) = (x, y, z);

   // Properties ---------------------------------------------------------------
   /// <summary>The X ordinate of the Point3</summary>
   public readonly double X;
   /// <summary>The Y ordinate of the Point3</summary>
   public readonly double Y;
   /// <summary>The Z ordinate of the Point3</summary>
   public readonly double Z;

   /// <summary>The origin in 3D space</summary>
   public static readonly Point3 Zero = new (0, 0, 0);
   /// <summary>The 'Nil' point</summary>
   public static readonly Point3 Nil = new (double.NaN, double.NaN, double.NaN);
   /// <summary>Is this point Nil (similar to NaN for double)</summary>
   public bool IsNil => double.IsNaN (X);

   // Methods ------------------------------------------------------------------
   /// <summary>Distance between this point and another</summary>
   public double DistTo (Point3 b) => Sqrt (DistToSq (b));
   /// <summary>Returns the perpendicular distance between this point and the inifinite line a..b</summary>
   public double DistToLine (Point3 a, Point3 b) => DistTo (SnappedToLine (a, b));
   /// <summary>Returns the closest distance between this point and the finite line segment a..b</summary>
   public double DistToLineSeg (Point3 a, Point3 b) => DistTo (SnappedToLineSeg (a, b));

   /// <summary>Square of the distance between this point and another</summary>
   public double DistToSq (Point3 b) {
      double dx = b.X - X, dy = b.Y - Y, dz = b.Z - Z; 
      return dx * dx + dy * dy + dz * dz; 
   }

   /// <summary>Compares two points are equal to within EPSILON</summary>
   public bool EQ (Point3 b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z);

   /// <summary>Unboxed version of Equals.</summary>
   public bool Equals (Point3 other) => EQ (other);

   /// <summary>Compares two Point3 for equality</summary>
   public override bool Equals ([NotNullWhen (true)] object? obj) {
      if (obj is not Point3 other) return false;
      return EQ (other);
   }

   /// <summary>Returns the Hash-code of the Point3 (based on their rounded-off approximations)</summary>
   public override int GetHashCode () {
      var pt = R6 ();
      return (pt.X, pt.Y, pt.Z).GetHashCode ();
   }

   /// <summary>Gets the lie of this point on the given line segment a..b</summary>
   /// This is accurate only if the point actually lies on the infinite line through a..b
   public double GetLieOn (Point3 a, Point3 b) {
      double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
      double adx = Abs (dx), ady = Abs (dy), adz = Abs (dz);
      if (adx > ady && adx > adz) return (X - a.X) / dx;    // X is the largest
      if (ady > adz) return (Y - a.Y) / dy;
      return (Z - a.Z) / dz;
   }

   /// <summary>Returns the point lying halfway between this point and another</summary>
   public Point3 Midpoint (Point3 b) => new ((X + b.X) / 2, (Y + b.Y) / 2, (Z + b.Z) / 2);

   /// <summary>Returns a point translated by the given dx, dy</summary>
   public Point3 Moved (double dx, double dy, double dz) => new (X + dx, Y + dy, Z + dz);

   /// <summary>Returns the point with ordinates rounded off to 6 decimals</summary>
   public Point3 R6 () => new (X.R6 (), Y.R6 (), Z.R6 ());

   /// <summary>Returns the closest point on the given line a..b</summary>
   /// If the points a and b are the same, this just returns a
   public Point3 SnappedToLine (Point3 a, Point3 b) => SnapHelper (a, b, false);
   /// <summary>Returns the closest point to the given _finite_ line segment a..b</summary>
   public Point3 SnappedToLineSeg (Point3 a, Point3 b) => SnapHelper (a, b, true);

   /// <summary>A copy of this Point3, with just the X ordinate changed</summary>
   public Point3 WithX (double x) => new (x, Y, Z);
   /// <summary>A copy of this Point3, with just the Y ordinate changed</summary>
   public Point3 WithY (double y) => new (X, y, Z);
   /// <summary>A copy of this Point3, with just the Z ordinate changed</summary>
   public Point3 WithZ (double z) => new (X, Y, z);

   // Operators ----------------------------------------------------------------
   /// <summary>Returns the displaced point got by adding a Vector2 to a Point2</summary>
   public static Point3 operator + (Point3 a, Vector3 b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
   /// <summary>Returns the displaced point got by subtracting a Vector2 from a Point2</summary>
   public static Point3 operator - (Point3 a, Vector3 b) => new (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
   /// <summary>Returns the Vector2 (displacement) between two points</summary>
   public static Vector3 operator - (Point3 a, Point3 b) => new (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
   /// <summary>Scales a Point2 uniformly by a scalar</summary>
   public static Point3 operator * (Point3 a, double f) => new (a.X * f, a.Y * f, a.Z * f);

   /// <summary>Convert a Point3 to a Vector3</summary>
   public static explicit operator Vector3 (Point3 a) => new (a.X, a.Y, a.Z);

   // Implementation -----------------------------------------------------------
   // Helper used by SnappedToLine and SnappedToLineSeg
   Point3 SnapHelper (Point3 a, Point3 b, bool clamp) {
      var (dx, dy, dz) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
      double scale = 1 / (dx * dx + dy * dy + dz * dz);
      if (double.IsInfinity (scale)) return a;
      // Use the parametric form of the line equation, and compute
      // the 'parameter t' of the closest point
      double t = ((X - a.X) * dx + (Y - a.Y) * dy + (Z - a.Z) * dz) * scale;
      if (clamp) t = t.Clamp ();
      return new (a.X + t * dx, a.Y + t * dy, a.Z + t * dz);
   }

   public override string ToString () => $"({X.S6 ()},{Y.S6 ()},{Z.S6 ()})";
}
#endregion
