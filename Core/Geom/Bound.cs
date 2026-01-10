// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Bound.cs
// ║║║║╬║╔╣║ Bounds in 1-D (span), 2-D (rectangle), 3-D (cuboid)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region struct Bound1 ------------------------------------------------------------------------------
/// <summary>Represents a bound in 1 dimension (simply a Min .. Max value, stored as floats)</summary>
public readonly struct Bound1 : IEQuable<Bound1> {
   // Constructors -------------------------------------------------------------
   /// <summary>Constructs an empty Bound1</summary>
   /// Note that the default for Bound1 (if new is never called to invoke the constructor)
   /// is a Bound1 that is initialized with all zeroes - which is *not* an empty bound,
   /// but a valid bound extending from 0 .. 0.
   public Bound1 () => (Min, Max) = (float.MaxValue, float.MinValue);

   /// <summary>Constructs a non-empty bound that encompasses a single value (Min = Max = v)</summary>
   public Bound1 (double v) => Min = Max = (float)v;

   /// <summary>Constructs a bound that encompasses a and b (a and b need not be ordered)</summary>
   public Bound1 (double a, double b) => (Min, Max) = ((float)Min (a, b), (float)Max (a, b));

   /// <summary>Deconstruct a Bound1 into min and max values</summary>
   public void Deconstruct (out float min, out float max) => (min, max) = (Min, Max);

   public override string ToString () => IsEmpty ? "Empty" : $"{Min.S5 ()}~{Max.S5 ()}";

   // Properties ---------------------------------------------------------------
   /// <summary>The minimum value of the bound (inclusive)</summary>
   public readonly float Min;
   /// <summary>The maximum value of the bound (inclusive)</summary>
   /// If Max is _less than_ min, that is an empty bound
   public readonly float Max;
   /// <summary>Length of the bound (Min .. Max)</summary>
   public double Length => Max - Min;
   /// <summary>Is this an empty bound?</summary>
   public bool IsEmpty => Min > Max;
   /// <summary>Midpoint value of the Bound1</summary>
   public double Mid => (Min + Max) / 2;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the value clamped to this Bound1</summary>
   public double Clamp (double f) => f.Clamp (Min, Max);
   /// <summary>Returns the value clamped to this Bound1</summary>
   /// Note that this handles correctly the situations where Min or Max (or both)
   /// could be NaN, in which case there is effectively no limit on that side of the
   /// range
   public float Clamp (float f) => f.Clamp (Min, Max);

   /// <summary>Returns true if f lies within the specific Bound1</summary>
   public bool Contains (double f) => Min <= f && f <= Max;
   /// <summary>Returns true if f lies within the specific Bound1 and within specified threshold</summary>
   public bool Contains (double f, double threshold) => Min - threshold <= f && f <= Max + threshold;
   /// <summary>Returns true if f lies within the specific Bound1</summary>
   public bool Contains (float f) => Min <= f && f <= Max;
   /// <summary>Returns true if b lies within the specified Bound1</summary>
   public bool Contains (Bound1 b) => Contains (b.Min) && Contains (b.Max);

   /// <summary>Compares two Bound1 for equality</summary>
   public bool EQ (Bound1 other) => Min.EQ (other.Min) && Max.EQ (other.Max);

   /// <summary>Returns a Bound1 inflated by a given factor (about the midpoint)</summary>
   public Bound1 InflatedF (double factor) {
      if (IsEmpty) return new ();
      var (mp, w) = (Mid, factor * Length / 2);
      return new (mp - w, mp + w);
   }
   /// <summary>Returns a Bound1 padded by a given margin on either side</summary>
   public Bound1 InflatedL (double delta) {
      if (IsEmpty) return new ();
      return new (Min - delta, Max + delta);
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Implicit conversion from a tuple of two double to a Bound1</summary>
   /// This makes it much simpler to construct Bound1 objects on the fly where needed by
   /// just enclosing a pair of numbers in parentheses.
   public static implicit operator Bound1 ((double Min, double Max) value) => new (value.Min, value.Max);

   /// <summary>Returns a Bound1 expanded to include the given value</summary>
   public static Bound1 operator + (Bound1 b, double v) => new (Min (b.Min, (float)v), Max (b.Max, (float)v));
   /// <summary>Returns the Bound1 expanded to include another Bound1</summary>
   public static Bound1 operator + (Bound1 a, Bound1 b) {
      float min = Min (a.Min, b.Min), max = Max (a.Max, b.Max);
      return new (min, max);
   }

   /// <summary>Finds the intersection of two Bound1</summary>
   public static Bound1 operator * (Bound1 a, Bound1 b) {
      double min = Max (a.Min, b.Min), max = Min (a.Max, b.Max);
      return min > max ? new () : new (min, max);
   }
}
#endregion

#region struct Bound2 ------------------------------------------------------------------------------
/// <summary>Represents a bound in 2 dimensions (a bounding rectangle)</summary>
[AuPrimitive]
public readonly struct Bound2 : IEQuable<Bound2> {
   // Constructors -------------------------------------------------------------
   /// <summary>Constructs an empty Bound2</summary>
   /// Note that the default for Bound2 (if new is never called to invoke the constructor)
   /// is a Bound2 that is initialized with all zeroes - which is *not* an empty bound,
   /// but a valid bound extending from 0 .. 0 in both X and Y.
   public Bound2 () => (X, Y) = (new (), new ());

   /// <summary>Constructs a Bound2 that encompasses just a single point (min == max in X and Y)</summary>
   public Bound2 (double x, double y) => (X, Y) = (new (x), new (y));

   /// <summary>Constructs a Bound2 that encompasses a single point (min == max in X and Y)</summary>
   public Bound2 (Point2 pt) => (X, Y) = (new (pt.X), new (pt.Y));

   /// <summary>Constructs a Bound2 from two Bound1 structs (one in X and one in Y)</summary>
   public Bound2 (Bound1 x, Bound1 y) => (X, Y) = (x, y);

   /// <summary>Constructs a Bound2 that uses the two given points x1,y1 and x2,y2 as diagonals</summary>
   /// No particular ordering is required between x1 and x2 or between y1 and y2. For
   /// example, x1 may be less than x2, while y1 may be more than y2.
   public Bound2 (double x1, double y1, double x2, double y2) => (X, Y) = (new (x1, x2), new (y1, y2));

   /// <summary>Construct a Bound2 that encompasses all the given points</summary>
   public Bound2 (IEnumerable<Point2> pts) {
      (X, Y) = (new (), new ());
      foreach (var p in pts) { X += p.X; Y += p.Y; }
   }

   /// <summary>Construct a Bound2  that encompasses all the given Bound2 (union)</summary>
   public Bound2 (IEnumerable<Bound2> bounds) {
      (X, Y) = (new (), new ());
      foreach (var b in bounds) { X += b.X; Y += b.Y; }
   }

   public override string ToString () => IsEmpty ? "Empty" : $"({X},{Y})";

   [Used]
   internal void Write (UTFWriter buf)
      => buf.Write (X.Min).Write (',').Write (Y.Min).Write (',').Write (X.Max).Write (',').Write (Y.Max);

   // Properties ---------------------------------------------------------------
   /// <summary>Area of the Bound2</summary>
   public double Area => X.Length * Y.Length;

   /// <summary>Height is the Y-span of the Bound2</summary>
   public double Height => Y.Length;

   /// <summary>Center point of Bound2 (a Point2)</summary>
   public Point2 Midpoint => new (X.Mid, Y.Mid);

   /// <summary>Width is the X-span of the Bound2</summary>
   public double Width => X.Length;

   /// <summary>The extent of the Bound2 in X (a Bound1)</summary>
   public readonly Bound1 X;

   /// <summary>The extent of the Bound2 in Y (a Bound1)</summary>
   public readonly Bound1 Y;

   /// <summary>Length of the diagonal of the Bound2</summary>
   public double Diagonal {
      get {
         double dx = X.Length, dy = Y.Length;
         return Sqrt (dx * dx + dy * dy);
      }
   }

   /// <summary>Is this an empty Bound2?</summary>
   /// An Empty Bound2 is created using `var b2 = new Bound2 ();`
   public bool IsEmpty => X.IsEmpty || Y.IsEmpty;

   // Methods ------------------------------------------------------------------
   /// <summary>Check if a Bound2 contains a given 2D point</summary>
   /// It returns true if the X and Y coordinates of the bound lie within the span
   /// the bound covers in X and Y (as in tennis, values lying exactly on the boundary
   /// are considered _in_).
   public bool Contains (Point2 pt) => X.Contains (pt.X) && Y.Contains (pt.Y);

   /// <summary>Check if a Bound2 contains a given 2D point within specified threshold</summary>
   /// This is the same as expanding the Bound2 by the given threshold in all 4 directions and then
   /// checking if that expanded Bound2 contains the given point
   public bool Contains (Point2 pt, double threshold) => X.Contains (pt.X, threshold) && Y.Contains (pt.Y, threshold);

   /// <summary>Checks if a Bound2 contains another bound (exact overlap is treated as containment)</summary>
   public bool Contains (Bound2 bound) => X.Contains (bound.X) && Y.Contains (bound.Y);

   /// <summary>Check if the a Bound2 contains the given 2D point</summary>
   public bool Contains (Vec2F pt) => X.Contains (pt.X) && Y.Contains (pt.Y);

   /// <summary>Compares two Bound2 for equality</summary>
   public bool EQ (Bound2 other) => X.EQ (other.X) && Y.EQ (other.Y);

   /// <summary>Returns a Bound2 inflated by a given factor about the midpoint</summary>
   public Bound2 InflatedF (double factor) => new (X.InflatedF (factor), Y.InflatedF (factor));

   /// <summary>Returns a Bound2 padded by a given linear margin on all sides</summary>
   public Bound2 InflatedL (double delta) => new (X.InflatedL (delta), Y.InflatedL (delta));

   /// <summary>Scales the rectangle by a given scale factor, about a given center of scaling</summary>
   public Bound2 InflatedF (double factor, Point2 pm) {
      if (IsEmpty) return new ();
      double left = (X.Min - pm.X) * factor, right = (X.Max - pm.X) * factor;
      double top = (Y.Max - pm.Y) * factor, bottom = (Y.Min - pm.Y) * factor;
      return new (pm.X + left, pm.Y + bottom, pm.X + right, pm.Y + top);
   }

   /// <summary>Computes a Bound2 (by calling the given delegate) if it is not already computed</summary>
   /// If the given Bound2 is empty, then the provided computer function is c alled
   /// to compute the bound. This is a convenience function that makes it easy to
   /// implement a `Bound` property with code like this:
   ///   public Bound2 Bound
   ///     => Bound2.Update (ref mBound, () => new (mPts));
   ///   Bound2 mBound = new ();
   public static Bound2 Cached (ref Bound2 bound, Func<Bound2> computer) {
      if (bound.IsEmpty) bound = computer ();
      return bound;
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Returns a Bound2 expanded to include the given Point2</summary>
   /// So a way to get the bounding rectangle of a set of points would be:
   /// Bound2 b = new();
   /// foreach (Point2 pt in pts) b += pt;
   /// Of course, a much easier way to do this would be to use the appropriate
   /// constructor:
   /// Bound2 b = new (pts);
   public static Bound2 operator + (Bound2 b, Point2 p) => new (b.X + p.X, b.Y + p.Y);

   /// <summary>Returns the intersection of two Bound2 (could be empty)</summary>
   public static Bound2 operator * (Bound2 a, Bound2 b) => new (a.X * b.X, a.Y * b.Y);

   /// <summary>Returns the union of two bounds</summary>
   public static Bound2 operator + (Bound2 a, Bound2 b) => new (a.X + b.X, a.Y + b.Y);

   /// <summary>Transform a bound by the given Xfm</summary>
   /// Note that this just takes the 4 corner points, applies the transform, and computes
   /// a new bound. If the transform contains a rotation, the resulting bound might be
   /// too conservative (not tight)
   public static Bound2 operator * (Bound2 a, Matrix2 m) {
      Bound2 b = new ();
      Span<Point2> pts = [new (a.X.Min, a.Y.Min), new (a.X.Max, a.Y.Min), new (a.X.Min, a.Y.Max), new (a.X.Max, a.Y.Max)];
      foreach (var pt in pts) b += pt * m;
      return b;
   }
}
#endregion

#region struct Bound3 ------------------------------------------------------------------------------
/// <summary>Represents a bound in 3 dimensions (a bounding cuboid)</summary>
[AuPrimitive]
public readonly struct Bound3 : IEQuable<Bound3> {
   // Constructors -------------------------------------------------------------
   public Bound3 () => (X, Y, Z) = (new (), new (), new ());
   public Bound3 (double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
      => (X, Y, Z) = (new (xmin, xmax), new (ymin, ymax), new (zmin, zmax));
   public Bound3 (Bound1 x, Bound1 y, Bound1 z) => (X, Y, Z) = (x, y, z);
   public override string ToString () => IsEmpty ? "Empty" : $"({X},{Y},{Z})";

   public Bound3 (IEnumerable<Point3f> pts) {
      (X, Y, Z) = (new (), new (), new ());
      foreach (var p in pts) { X += p.X; Y += p.Y; Z += p.Z; }
   }
   public Bound3 (IEnumerable<Point3> pts) {
      (X, Y, Z) = (new (), new (), new ());
      foreach (var p in pts) { X += p.X; Y += p.Y; Z += p.Z; }
   }
   /// <summary>Construct a Bound3  that encompasses all the given Bound3 (union)</summary>
   public Bound3 (IEnumerable<Bound3> bounds) {
      (X, Y, Z) = (new (), new (), new ());
      foreach (var b in bounds) { X += b.X; Y += b.Y; Z += b.Z; }
   }

   public static Bound3 Read (UTFReader r) {
      r.Match ('"').Read (out double x0).Match (',').Read (out double y0).Match (',').Read (out double z0)
         .Match (':').Read (out double x1).Match (',').Read (out double y1).Match (',').Read (out double z1).Match ('"');
      return new (x0, y0, z0, x1, y1, z1);
   }

   // Properties ---------------------------------------------------------------
   public readonly Bound1 X, Y, Z;
   /// <summary>Width is the X-span of the Bound3</summary>
   public double Width => X.Length;
   /// <summary>Height is the Y-span of the Bound3</summary>
   public double Height => Y.Length;
   /// <summary>Depth is the Z-span of the Bound3</summary>
   public double Depth => Z.Length;
   public bool IsEmpty => X.IsEmpty || Y.IsEmpty || Z.IsEmpty;
   public Point3 Midpoint => new (X.Mid, Y.Mid, Z.Mid);

   public double Diagonal {
      get {
         double dx = X.Length, dy = Y.Length, dz = Z.Length;
         return Sqrt (dx * dx + dy * dy + dz * dz);
      }
   }

   /// <summary>
   /// The diagonal vector of this Bound3
   /// </summary>
   public Vector3 DiagVector => new (X.Length, Y.Length, Z.Length);

   // Methods ------------------------------------------------------------------
      /// <summary>Check if a Bound3 contains a given 3D point</summary>
   public bool Contains (Point3 pt) => X.Contains (pt.X) && Y.Contains (pt.Y) && Z.Contains (pt.Z);

   /// <summary>Checks if a Bound3 contains another bound (exact overlap is treated as containment)</summary>
   public bool Contains (Bound3 bound) => X.Contains (bound.X) && Y.Contains (bound.Y) && Z.Contains (bound.Z);

   /// <summary>Compares two Bound3 for equality</summary>
   public bool EQ (Bound3 other) => X.EQ (other.X) && Y.EQ (other.Y) && Z.EQ (other.Z);

   /// <summary>Returns a Bound3 inflated by a given factor about the midpoint</summary>
   public Bound3 InflatedF (double factor) => new (X.InflatedF (factor), Y.InflatedF (factor), Z.InflatedF (factor));
   /// <summary>Returns a Bound3 padded by a given linear margin on all sides</summary>
   public Bound3 InflatedL (double delta) => new (X.InflatedL (delta), Y.InflatedL (delta), Z.InflatedL (delta));

   /// <summary>Computes a Bound3 (by calling the given delegate) if it is not already computed</summary>
   /// If the given Bound3 is empty, then the provided computer function is c alled
   /// to compute the bound. This is a convenience function that makes it easy to
   /// implement a `Bound` property with code like this:
   ///   public Bound3 Bound
   ///     => Bound3.Update (ref mBound, () => new (mPts));
   ///   Bound3 mBound = new ();
   public static Bound3 Cached (ref Bound3 bound, Func<Bound3> computer) {
      if (bound.IsEmpty) bound = computer ();
      return bound;
   }

   /// <summary>Write the Bound3 to UTF8, in a format like "1,2,3:4,5,6"</summary>
   /// Here, (1,2,3) is the X,Y,Z lower min point and (4,5,6) is upper max point
   public void Write (UTFWriter w) {
      if (Lib.Testing)
         w.Write ('"').Write (X.Min.R5 ()).Write (',').Write (Y.Min.R5 ()).Write (',').Write (Z.Min.R5 ()).Write (':')
         .Write (X.Max.R5 ()).Write (',').Write (Y.Max.R5 ()).Write (',').Write (Z.Max.R5 ()).Write ('"');
      else
         w.Write ('"').Write (X.Min).Write (',').Write (Y.Min).Write (',').Write (Z.Min).Write (':')
         .Write (X.Max).Write (',').Write (Y.Max).Write (',').Write (Z.Max).Write ('"');
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Returns a Bound3 expanded to include the given Point3</summary>
   public static Bound3 operator + (Bound3 b, Point3 p) => new (b.X + p.X, b.Y + p.Y, b.Z + p.Z);
   /// <summary>Returns a Bound3 expanded to include the given Vec3F</summary>
   public static Bound3 operator + (Bound3 b, Point3f v) => new (b.X + v.X, b.Y + v.Y, b.Z + v.Z);
   /// <summary>Returns a Bound3 that is the union of two Bound3</summary>
   public static Bound3 operator + (Bound3 a, Bound3 b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
   /// <summary>Returns the intersection of two Bound3 (could be empty)</summary>
   public static Bound3 operator * (Bound3 a, Bound3 b) => new (a.X * b.X, a.Y * b.Y, a.Z * b.Z);
}
#endregion
