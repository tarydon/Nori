// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Bound.cs
// ║║║║╬║╔╣║ Bounds in 1-D (span), 2-D (rectangle), 3-D (cuboid)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region struct Bound1 ------------------------------------------------------------------------------
/// <summary>Represents a bound in 1 dimension (simply a Min .. Max value, stored as floats)</summary>
public readonly struct Bound1 : IEQuable<Bound1> {
   // Constructors -------------------------------------------------------------
   public Bound1 () => (Min, Max) = (float.MaxValue, float.MinValue);
   public Bound1 (double v) => Min = Max = (float)v;
   public Bound1 (double a, double b) => (Min, Max) = ((float)Min (a, b), (float)Max (a, b));
   public override string ToString () => IsEmpty ? "Empty" : $"{Min.R5 ()}▸{Max.R5 ()}";

   // Properties ---------------------------------------------------------------
   public static readonly Bound1 NaN = new (double.NaN);
   public readonly float Min, Max;
   public double Length => Max - Min;
   public bool IsEmpty => Min > Max;
   public double Mid => (Min + Max) / 2;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the value clamped to this Bound1</summary>
   public readonly double Clamp (double f) => f.Clamp (Min, Max);
   /// <summary>Returns the value clamped to this Bound1</summary>
   /// Note that this handles correctly the situations where Min or Max (or both)
   /// could be NaN, in which case there is effectively no limit on that side of the
   /// range
   public readonly float Clamp (float f) => f.Clamp (Min, Max);

   /// <summary>Returns true if f lies within the specific Bound1</summary>
   public readonly bool Contains (double f) => Min <= f && f <= Max;

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
public readonly struct Bound2 {
   // Constructors -------------------------------------------------------------
   public Bound2 () => (X, Y) = (new (), new ());
   public Bound2 (double x, double y) => (X, Y) = (new (x), new (y));
   public Bound2 (double xmin, double ymin, double xmax, double ymax) => (X, Y) = (new (xmin, xmax), new (ymin, ymax));
   public Bound2 (Bound1 x, Bound1 y) => (X, Y) = (x, y);

   public Bound2 (IEnumerable<Point2> pts) {
      (X, Y) = (new (), new ());
      foreach (var p in pts) { X += p.X; Y += p.Y; }
   }

   public Bound2 (IEnumerable<Bound2> bounds) {
      (X, Y) = (new (), new ());
      foreach (var b in bounds) { X += b.X; Y += b.Y; }
   }

   public override string ToString () => IsEmpty ? "Empty" : $"({X},{Y})";

   // Properties ---------------------------------------------------------------
   public readonly Bound1 X, Y;
   public double Width => X.Length;
   public double Height => Y.Length;
   public bool IsEmpty => X.IsEmpty || Y.IsEmpty;
   public Point2 Midpoint => new (X.Mid, Y.Mid);

   public double Diagonal {
      get {
         double dx = X.Length, dy = Y.Length;
         return Sqrt (dx * dx + dy * dy);
      }
   }

   // Methods ------------------------------------------------------------------
   public readonly bool Contains (Point2 pt) => X.Contains (pt.X) && Y.Contains (pt.Y);
   /// <summary>Returns a Bound2 inflated by a given factor about the midpoint</summary>
   public readonly Bound2 InflatedF (double factor) => new (X.InflatedF (factor), Y.InflatedF (factor));
   /// <summary>Returns a Bound2 padded by a given linear margin on all sides</summary>
   public readonly Bound2 InflatedL (double delta) => new (X.InflatedL (delta), Y.InflatedL (delta));

   /// <summary>Scales the rectangle by a given scale factor, about a given center of scaling</summary>
   public readonly Bound2 InflatedF (double factor, Point2 pm) {
      if (IsEmpty) return new ();
      double left = (X.Min - pm.X) * factor, right = (X.Max - pm.X) * factor;
      double top = (Y.Max - pm.Y) * factor, bottom = (Y.Min - pm.Y) * factor;
      return new (pm.X + left, pm.Y + bottom, pm.X + right, pm.Y + top);
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Returns a Bound2 expanded to include the given Point2</summary>
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
public readonly struct Bound3 {
   // Constructors -------------------------------------------------------------
   public Bound3 () => (X, Y, Z) = (new (), new (), new ());
   public Bound3 (double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
      => (X, Y, Z) = (new (xmin, xmax), new (ymin, ymax), new (zmin, zmax));
   public Bound3 (Bound1 x, Bound1 y, Bound1 z) => (X, Y, Z) = (x, y, z);
   public override string ToString () => IsEmpty ? "Empty" : $"({X},{Y},{Z})";

   public Bound3 (IEnumerable<Vec3F> pts) {
      (X, Y, Z) = (new (), new (), new ());
      foreach (var p in pts) { X += p.X; Y += p.Y; Z += p.Z; }
   }

   // Properties ---------------------------------------------------------------
   public readonly Bound1 X, Y, Z;
   public double Width => X.Length;
   public double Height => Y.Length;
   public double Depth => Z.Length;
   public bool IsEmpty => X.IsEmpty || Y.IsEmpty || Z.IsEmpty;
   public Point3 Midpoint => new (X.Mid, Y.Mid, Z.Mid);

   public double Diagonal {
      get {
         double dx = X.Length, dy = Y.Length, dz = Z.Length;
         return Sqrt (dx * dx + dy * dy + dz * dz);
      }
   }

   // Methods ------------------------------------------------------------------
   public readonly bool Contains (Point3 pt) => X.Contains (pt.X) && Y.Contains (pt.Y) && Z.Contains (pt.Z);
   /// <summary>Returns a Bound3 inflated by a given factor about the midpoint</summary>
   public readonly Bound3 InflatedF (double factor) => new (X.InflatedF (factor), Y.InflatedF (factor), Z.InflatedF (factor));
   /// <summary>Returns a Bound3 padded by a given linear margin on all sides</summary>
   public readonly Bound3 InflatedL (double delta) => new (X.InflatedL (delta), Y.InflatedL (delta), Z.InflatedL (delta));

   // Operators ----------------------------------------------------------------
   /// <summary>Returns a Bound3 expanded to include the given Point3</summary>
   public static Bound3 operator + (Bound3 b, Point3 p) => new (b.X + p.X, b.Y + p.Y, b.Z + p.Z);
   /// <summary>Returns a Bound3 that is the union of two Bound3</summary>
   public static Bound3 operator + (Bound3 a, Bound3 b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
   /// <summary>Returns the intersection of two Bound3 (could be empty)</summary>
   public static Bound3 operator * (Bound3 a, Bound3 b) => new (a.X * b.X, a.Y * b.Y, a.Z * b.Z);
}
#endregion

#region struct Quaternion4 -------------------------------------------------------------------------
/// <summary>Represents a Quaternion of rotation</summary>
public readonly struct Quaternion : IEQuable<Quaternion> {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a quaternion given the 4 components</summary>
   public Quaternion (double x, double y, double z, double w) => (X, Y, Z, W) = (x, y, z, w);

   /// <summary>Makes a quaternion given 3 axis rotations (in radians)</summary>
   public static Quaternion FromAxisRotations (double xRot, double yRot, double zRot) {
      Quaternion q1 = FromAxisAngle (Vector3.XAxis, xRot), q2 = FromAxisAngle (Vector3.YAxis, yRot), q3 = FromAxisAngle (Vector3.ZAxis, zRot);
      return q1 * q2 * q3;
   }

   /// <summary>Construct a Quaternion4 from a rotation axis, and an angle (in radians)</summary>
   public static Quaternion FromAxisAngle (Vector3 axis, double angle) {
      angle = Lib.NormalizeAngle (angle);
      double length = axis.Length;
      if (length.IsZero ())
         throw new ArgumentException ("Quaternion with zero axis", nameof (axis));
      Vector3 vec = axis * (Sin (0.5 * angle) / length);
      return new (vec.X, vec.Y, vec.Z, Cos (0.5 * angle));
   }

   /// <summary>Returns the identity quaternion</summary>
   public static readonly Quaternion Identity = new (0, 0, 0, 1);

   /// <summary>Parse a Quaternion from a string of the form X,Y,Z:Deg</summary>
   /// X,Y,Z specify the rotation axis as a 3-component vector, and Deg
   /// is the rotation angle (in degrees)
   public static Quaternion Parse (string input) {
      var w = input.Split (',', ':').Select (a => a.ToDouble ()).ToList ();
      return FromAxisAngle (new (w[0], w[1], w[2]), w[3].D2R ());
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the angle of rotation (in radians)</summary>
   public readonly double Angle {
      get {
         double y = Sqrt (X * X + Y * Y + Z * Z), x = W;
         return Atan2 (y, x) * 2;
      }
   }

   /// <summary>Returns the (normalized) axis of rotation</summary>
   public readonly Vector3 Axis => new Vector3 (X, Y, Z).Normalized ();
   /// <summary>Is this an identity quaternion?</summary>
   public readonly bool IsIdentity => Angle.IsZero ();
   /// <summary>The components of the quaternion</summary>
   public readonly double X, Y, Z, W;

   // Methods ------------------------------------------------------------------
   /// <summary>This constructs a Quaternion from a string in this form: "X,Y,Z:Deg"</summary>
   /// <summary>Returns true if two quaternions are nearly equal</summary>
   public readonly bool EQ (Quaternion other)
      => X.EQ (other.X) && Y.EQ (other.Y) && Z.EQ (other.Z) && W.EQ (other.W);

   /// <summary>Expresses the Quaternion in this form: "X,Y,Z:Deg"</summary>
   /// The first 3 numbers provide the axis of rotation, and the 4th is the angle of
   /// rotation in degrees
   public readonly override string ToString () {
      var (a, g) = (Axis, Angle.R2D ());
      return $"{a.X.R6 ()},{a.Y.R6 ()},{a.Z.R6 ()}:{g.R6 ()}";
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Composes a composite rotation of two quaternions</summary>
   public static Quaternion operator * (Quaternion a, Quaternion b) {
      double x = a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y;
      double y = a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z;
      double z = a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X;
      double w = a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z;
      return new (x, y, z, w);
   }
}
#endregion
