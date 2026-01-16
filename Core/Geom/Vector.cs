// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Vector.cs
// ║║║║╬║╔╣║ Various vector classes (in 2D and 3D)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region struct Vector2 -----------------------------------------------------------------------------
/// <summary>Vector in 2D, with 64-bit double components</summary>
public readonly struct Vector2 : IEQuable<Vector2> {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Vector2 given the X and Y components</summary>
   public Vector2 (double x, double y) => (X, Y) = (x, y);

   /// <summary>Returns the unit vector2 along a given cardinal direction </summary>
   public static Vector2 Along (EDir d)
      => d switch { EDir.N => new (0, 1), EDir.W => new (-1, 0), EDir.S => new (0, -1), _ => new (1, 0) };

   /// <summary>Returns the unit-vector along a given angle</summary>
   public static Vector2 UnitVec (double angle) { var (sin, cos) = SinCos (angle); return new (cos, sin); }

   // Properties ---------------------------------------------------------------
   /// <summary>X component of the Vector2</summary>
   public readonly double X;
   /// <summary>Y component of the Vector2</summary>
   public readonly double Y;

   /// <summary>Returns true if the Vector2 is zero to within Epsilon</summary>
   public bool IsZero => X.IsZero () && Y.IsZero ();
   /// <summary>Length of the Vector</summary>
   public double Length => Sqrt (LengthSq);
   /// <summary>Square of the length of the vector</summary>
   public double LengthSq => X * X + Y * Y;
   /// <summary>The 'slope' is the heading of this vector (0=east, pi/2=north etc)</summary>
   public double Heading => Atan2 (Y, X);

   /// <summary>Unit vector, aligned to the X axis</summary>
   public static readonly Vector2 XAxis = new (1, 0);
   /// <summary>Unit vector, aligned to the Y axis</summary>
   public static readonly Vector2 YAxis = new (0, 1);
   /// <summary>The Zero vector</summary>
   public static readonly Vector2 Zero = new (0, 0);

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the angle between this vector and another vector</summary>
   /// CosineTo is considerably faster, use that if that value is adequate
   public double AngleTo (Vector2 v2) => Lib.Acos (CosineTo (v2));

   /// <summary>Returns the cosine of the angle between this vector and another</summary>
   /// If either of the vectors has zero length, this returns 0
   public double CosineTo (Vector2 v2) {
      double fNumer = X * v2.X + Y * v2.Y, fDenom = Length * v2.Length;
      if (Abs (fDenom) < 1e-12) return 0;
      return fNumer / fDenom;
   }

   /// <summary>Deconstructs a Vector2 to a (double, double)</summary>
   public void Deconstruct (out double dx, out double dy) => (dx, dy) = (X, Y);

   /// <summary>Returns the dot product of this vector with another</summary>
   public double Dot (Vector2 v) => X * v.X + Y * v.Y;

   /// <summary>Checks if two Vector2 are equal to within Epsilon</summary>
   public bool EQ (Vector2 b) => X.EQ (b.X) && Y.EQ (b.Y);

   /// <summary>Returns this vector, normalized</summary>
   public Vector2 Normalized () {
      double f = Length; if (Abs (f) < 1e-12) return XAxis;
      return this / f;
   }

   public bool Opposing (Vector2 v) => Dot (v) < 0;

   /// <summary>Gets a vector perpendicular to this one (rotated +90 to this vector)</summary>
   public Vector2 Perpendicular () => new (-Y, X);

   /// <summary>Rotates a vector about a given angle</summary>
   public Vector2 Rotated (double angle) {
      var (sin, cos) = SinCos (angle);
      return new Vector2 (X * cos - Y * sin, X * sin + Y * cos);
   }

   /// <summary>A copy of this vector, with just the X component changed</summary>
   public Vector2 WithX (double x) => new (x, Y);
   /// <summary>A copy of this vector, with just the Y component changed</summary>
   public Vector2 WithY (double y) => new (X, y);

   // Operators ----------------------------------------------------------------
   /// <summary>Adds a Vector2 to another</summary>
   public static Vector2 operator + (Vector2 a, Vector2 b) => new (a.X + b.X, a.Y + b.Y);
   /// <summary>Subtracts one Vector2 from another</summary>
   public static Vector2 operator - (Vector2 a, Vector2 b) => new (a.X - b.X, a.Y - b.Y);
   /// <summary>Multiply a Vector2 by a scalar</summary>
   public static Vector2 operator * (Vector2 a, double f) => new (a.X * f, a.Y * f);
   /// <summary>Divide a Vector2 by a scalar</summary>
   public static Vector2 operator / (Vector2 a, double f) => new (a.X / f, a.Y / f);
   /// <summary>Returns the negative (inverse) of a Vector2</summary>
   public static Vector2 operator - (Vector2 a) => new (-a.X, -a.Y);

   /// <summary>Convert a Vector2 to a Point2</summary>
   public static explicit operator Point2 (Vector2 a) => new (a.X, a.Y);
   /// <summary>Convert a Vector2 to a Vector3.</summary>
   public static explicit operator Vector3 (Vector2 a) => new (a.X, a.Y, 0);
   // Implementation -----------------------------------------------------------
   public override string ToString () => $"<{X.R6 ()},{Y.R6 ()}>";
}
#endregion

#region struct Vector3 -----------------------------------------------------------------------------
/// <summary>Vector in 3 dimensions, with 64-bit double components</summary>
[AuPrimitive]
public readonly struct Vector3 : IEQuable<Vector3> {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Vector3 given the X, Y, Z components</summary>
   public Vector3 (double x, double y, double z) => (X, Y, Z) = (x, y, z);

   /// <summary>Read a Point2 from a UTF8 stream</summary>
   public static Vector3 Read (UTFReader R) {
      R.Read (out double x).Match (',').Read (out double y).Match (',').Read (out double z);
      return new (x, y, z);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The X component of the Vector3</summary>
   public readonly double X;
   /// <summary>The Y component of the Vector3</summary>
   public readonly double Y;
   /// <summary>The Z component of the Vector3</summary>
   public readonly double Z;

   /// <summary>Is a Vector3 zero to within Epsilon</summary>
   public bool IsZero => X.IsZero () && Y.IsZero () && Z.IsZero ();
   /// <summary>The Length of the Vector3</summary>
   public double Length => Sqrt (LengthSq);
   /// <summary>Square of the length of the Vector3</summary>
   public double LengthSq => X * X + Y * Y + Z * Z;

   /// <summary>Unit vector, aligned to the X axis</summary>
   public static readonly Vector3 XAxis = new (1, 0, 0);
   /// <summary>Unit vector, aligned to the Y axis</summary>
   public static readonly Vector3 YAxis = new (0, 1, 0);
   /// <summary>Unit vector, aligned to the Z axis</summary>
   public static readonly Vector3 ZAxis = new (0, 0, 1);
   /// <summary>The Zero vector</summary>
   public static readonly Vector3 Zero = new (0, 0, 0);

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the angle between this vector and another vector</summary>
   /// CosineTo is considerably faster, use that if that value is adequate
   public double AngleTo (Vector3 v2) => Lib.Acos (CosineTo (v2));

   /// <summary>A vector3 whose components are the absolute values</summary>
   public Vector3 Abs () => new (Math.Abs (X), Math.Abs (Y), Math.Abs (Z));

   /// <summary>Returns the cosine of the angle between this Vector3 and another</summary>
   public double CosineTo (Vector3 v2) {
      double n = X * v2.X + Y * v2.Y + Z * v2.Z, d = Length * v2.Length;
      if (d < 1e-12) return 0;
      return (n / d).Clamp (-1, 1);
   }

   /// <summary>Similar to CosineTo, but works for already-normalized vectors</summary>
   public double CosineToAlreadyNormalized (Vector3 v2) => X * v2.X + Y * v2.Y + Z * v2.Z;

   /// <summary>Deconstructs a Vector3 to a (double, double, double)</summary>
   public void Deconstruct (out double dx, out double dy, out double dz) => (dx, dy, dz) = (X, Y, Z);

   /// <summary>Returns the dot product of this vector with another</summary>
   public double Dot (Vector3 b) => X * b.X + Y * b.Y + Z * b.Z;

   /// <summary>Returns true if two Vector3 are equal to within Epsilon</summary>
   public bool EQ (Vector3 b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z);

   /// <summary>Compares two Vector3 for equality</summary>
   public override bool Equals ([NotNullWhen (true)] object? obj) => obj is Vector3 v && EQ (v);
   /// <summary>Returns the Hash-code of the Vector3 (based on their rounded-off approximations)</summary>
   public override int GetHashCode () => HashCode.Combine (X.R6 (), Y.R6 (), Z.R6 ());

   /// <summary>Returns true if this vector opposes the other</summary>
   public bool Opposing (Vector3 b) => Dot (b) < 0;

   /// <summary>Normalizes this vector and returns it</summary>
   public Vector3 Normalized () {
      double f = Length; if (Math.Abs (f) < 1e-12) return XAxis;
      return new (X / f, Y / f, Z / f);
   }

   /// <summary>Rotates a vector about a given axis, and returns a copy</summary>
   /// <param name="a">The cardinal axis to rotate about</param>
   /// <param name="angle">The angle to rotate, in radians</param>
   /// <returns>The rotated copy of the input vector</returns>
   public Vector3 Rotated (EAxis a, double angle) {
      var (sin, cos) = SinCos (angle); 
      return a switch {
         EAxis.X => new (X, Y * cos - Z * sin, Y * sin + Z * cos),
         EAxis.Y => new (Z * sin + X * cos, Y, Z * cos - X * sin),
         _ => new (X * cos - Y * sin, X * sin + Y * cos, Z)
      };
   }

   /// <summary>Returns the Vector3 with components rounded off to 6 decimals</summary>
   public Vector3 R6 () => new (X.R6 (), Y.R6 (), Z.R6 ());

   /// <summary>A copy of this vector, with just the X component changed</summary>
   public Vector3 WithX (double x) => new (x, Y, Z);
   /// <summary>A copy of this vector, with just the Y component changed</summary>
   public Vector3 WithY (double y) => new (X, y, Z);
   /// <summary>A copy of this vector, with just the Z component changed</summary>
   public Vector3 WithZ (double z) => new (X, Y, z);

   /// <summary>Write a Vector3 to a UTF8 stream</summary>
   public void Write (UTFWriter B) => B.Write (X.R6 ()).Write (',').Write (Y.R6 ()).Write (',').Write (Z.R6 ());

   // Operators ----------------------------------------------------------------
   /// <summary>Returns the cross-product of two Vector3</summary>
   public static Vector3 operator * (Vector3 a, Vector3 b)
      => new (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
   /// <summary>Adds one Vector3 to another</summary>
   public static Vector3 operator + (Vector3 a, Vector3 b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
   /// <summary>Subtract one Vector3 to another</summary>
   public static Vector3 operator - (Vector3 a, Vector3 b) => new (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
   /// <summary>Multiply a Vector3 with a scalar</summary>
   public static Vector3 operator * (Vector3 a, double f) => new (a.X * f, a.Y * f, a.Z * f);
   /// <summary>Divide a Vector3 by a scalar</summary>
   public static Vector3 operator / (Vector3 a, double f) => new (a.X / f, a.Y / f, a.Z / f);
   /// <summary>Returns the negative (inverse) of a Vector3</summary>
   public static Vector3 operator - (Vector3 a) => new (-a.X, -a.Y, -a.Z);

   /// <summary>Convert a Vector3 to a Point3</summary>
   public static explicit operator Point3 (Vector3 a) => new (a.X, a.Y, a.Z);

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"<{X.R6 ()},{Y.R6 ()},{Z.R6 ()}>";
}
#endregion

#region struct Vector3f ----------------------------------------------------------------------------
/// <summary>Vector in 3 dimensions, 32-bit float components</summary>
public readonly struct Vector3f {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Vector3f given 3 doubles</summary>
   public Vector3f (double x, double y, double z) => (X, Y, Z) = ((float)x, (float)y, (float)z);
   /// <summary>Construct a Vector3f given 3 floats</summary>
   public Vector3f (float x, float y, float z) => (X, Y, Z) = (x, y, z);

   // Properties ---------------------------------------------------------------
   /// <summary>The X ordinate of the Point3</summary>
   public readonly float X;
   /// <summary>The Y ordinate of the Point3</summary>
   public readonly float Y;
   /// <summary>The Z ordinate of the Point3</summary>
   public readonly float Z;

   public double Length => Math.Sqrt (X * X + Y * Y + Z * Z);

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the dot product of this vectorf with another</summary>
   public double Dot (Vector3f b) => X * b.X + Y * b.Y + Z * b.Z;

   /// <summary>Returns this vector normalized to length 1</summary>
   public Vector3f Normalized () { double len = Length; return new (X / len, Y / len, Z / len); }

   // Operators ----------------------------------------------------------------
   /// <summary>Converts a Point3f to a Point3</summary>
   public static explicit operator Vector3 (Vector3f a) => new (a.X, a.Y, a.Z);
   /// <summary>Converts a Point3 to a Point3f</summary>
   public static explicit operator Vector3f (Vector3 a) => new (a.X, a.Y, a.Z);

   /// <summary>Adds two Vector3f together</summary>
   public static Vector3f operator + (Vector3f a, Vector3f b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

   /// <summary>Returns the cross-product of two Vector3f</summary>
   public static Vector3f operator * (Vector3f a, Vector3f b)
      => new (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

   public override string ToString () => $"<{X.S5 ()},{Y.S5 ()},{Z.S5 ()}>";
}
#endregion

