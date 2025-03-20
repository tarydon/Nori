// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Structs.cs
// ║║║║╬║╔╣║ Various Miscellaneous structs used by the Nori application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region struct BlockTimer --------------------------------------------------------------------------
/// <summary>A simple utility class that times how long a block takes</summary>
/// Use this BlockTimer in a using statement that wraps around the block to be timed
public readonly struct BlockTimer : IDisposable {
   /// <summary>Construct a Blocktimer, given the text to display when the block finishes</summary>
   public BlockTimer (string text) => (mText, mStart) = (text, DateTime.Now);

   public void Dispose () {
      double time = (DateTime.Now - mStart).TotalMilliseconds;
      Lib.Trace ($"{mText}: {time:F2} ms\n");
   }

   readonly string mText;
   readonly DateTime mStart;
}
#endregion

#region struct Color -------------------------------------------------------------------------------
/// <summary>Represents a 32-bit color value</summary>
public readonly struct Color4 : IEQuable<Color4> {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a color with given R, G, B values (from 0..255), and alpha 0xFF</summary>
   public Color4 (int r, int g, int b) => (R, G, B, A) = ((byte)r, (byte)g, (byte)b, 255);
   /// <summary>Construct a color with given A, R, G, B and A values</summary>
   public Color4 (int a, int r, int g, int b) => (A, R, G, B) = ((byte)a, (byte)r, (byte)g, (byte)b);
   /// <summary>Construct a color from a 32-bit uint value, where the bits (from MSB on) are packed like AARRGGBB</summary>
   /// That is, the most-significant 8 bits are the alpha value, and the least-significant 8 bits are the
   /// Blue value
   public Color4 (uint v) { A = (byte)(v >> 24); R = (byte)(v >> 16); G = (byte)(v >> 8); B = (byte)v; }
   public void Deconstruct (out int r, out int g, out int b, out int a) => (r, g, b, a) = (R, G, B, A);

   /// <summary>Parses a string to a Color4</summary>
   /// These formats are supported:
   /// - #AARRGGBB : 8 digit hex, 2 digits each for Alpha, Red, Green, Blue
   /// - #RRGGBB : 6 digit hex, 2 digits each for Red, Green, Blue. Alpha set to 0xFF
   /// - #RGB : 3 digit hex, expands into #RRGGBB (similar to 3 digit hex codes for HTML colors)
   /// - Named values like Red, Transparent, Blue etc
   public static Color4 Parse (string s) {
      BuildMap ();
      if (sParse.TryGetValue (s, out var c)) return c;
      if (s.Length is 4 or 7 or 9 && s[0] == '#') {
         Span<char> inp = stackalloc char[8];
         inp[0] = inp[1] = 'F';
         switch (s.Length) {
            case 4: for (int i = 0; i < 3; i++) inp[i * 2 + 2] = inp[i * 2 + 3] = s[i + 1]; break;
            case 7: for (int i = 0; i < 6; i++) inp[i + 2] = s[i + 1]; break;
            default: for (int i = 0; i < 8; i++) inp[i] = s[i + 1]; break;
         }
         return new (uint.Parse (inp, System.Globalization.NumberStyles.HexNumber));
      }
      throw new ParseException (s, typeof (Color4));
   }

   // Properties ---------------------------------------------------------------
   public readonly byte R, G, B, A;
   public static readonly Color4 Nil = new (0, 0, 0, 0);
   public static readonly Color4 Transparent = new (0, 255, 255, 255);
   public static readonly Color4 Black = new (0, 0, 0);
   public static readonly Color4 Red = new (255, 0, 0);
   public static readonly Color4 Green = new (0, 255, 0);
   public static readonly Color4 Blue = new (0, 0, 255);
   public static readonly Color4 Yellow = new (255, 255, 0);
   public static readonly Color4 Magenta = new (255, 0, 255);
   public static readonly Color4 Cyan = new (0, 255, 255);
   public static readonly Color4 White = new (255, 255, 255);

   public static Color4 Random => new (mRand.Next (256), mRand.Next (256), mRand.Next (256));
   public static Color4 RandomLight => new (mRand.Next (128) + 128, mRand.Next (128) + 128, mRand.Next (128) + 128);
   public static Color4 RandomDark => new (mRand.Next (128), mRand.Next (128), mRand.Next (128));

   public bool IsTransparent => A == 0;
   public bool IsNil => A == 0 && R == 0 && G == 0 && B == 0;
   public uint Value => (uint)((A << 24) | (B << 16) | (G << 8) | R);

   // Methods ------------------------------------------------------------------
   /// <summary>Compares two color4 for equality</summary>
   public bool EQ (Color4 other) => R == other.R && G == other.G && B == other.B && A == other.A;

   /// <summary>Constructs a Gray color with value 0..255</summary>
   public static Color4 Gray (int v) => new (v, v, v);

   public override string ToString () {
      BuildMap ();
      if (sNames.TryGetValue (this, out var s)) return $"{s}";
      s = $"{(uint)((A << 24) | (R << 16) | (G << 8) | B):X8}";
      if (s.StartsWith ("FF")) s = s[2..];
      if (s.Length == 6 && s[0] == s[1] && s[2] == s[3] && s[4] == s[5])
         s = $"{s[0]}{s[2]}{s[4]}";
      return $"#{s}";
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Converts the color to a Vec4f with the X,Y,Z,W components mapping to R,G,B,A</summary>
   public static explicit operator Vec4F (Color4 c) => new (c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

   // Implementation -----------------------------------------------------------
   // Builds the maps that convert 'known' Color4 values to names like Red / Blue etc
   // and the reverse map that converts such strings into name values
   static void BuildMap () {
      if (sNames.Count == 0) {
         foreach (var fi in typeof (Color4).GetFields (BindingFlags.Public | BindingFlags.Static)) {
            Color4 color = (Color4)fi.GetValue (null)!;
            sNames[color] = fi.Name; sParse[fi.Name] = color;
         }
      }
   }
   static Dictionary<Color4, string> sNames = [];
   static Dictionary<string, Color4> sParse = new (StringComparer.OrdinalIgnoreCase);
   static Random mRand = new ();
}
#endregion

#region struct CoordSystem -------------------------------------------------------------------------
/// <summary>Defines a CoordSystem in 3-D space (given by origin point, x-vector and y-vector)</summary>
/// The Z-vector of the coordinate system is defined using the right-hand rule: Z = X.Cross(Y)
public readonly struct CoordSystem {
   // Constructors -------------------------------------------------------------
   /// <summary>Constructor to make a CoordSystem given the origin, X and Y axes</summary>
   public CoordSystem (Point3 org, Vector3 vecx, Vector3 vecy) {
      (Org, VecX, VecY) = (org, vecx.Normalized (), vecy.Normalized ());
      if (!VecX.CosineToAlreadyNormalized (VecY).IsZero ())
         throw new InvalidOperationException ("CoordSystem basis vectors are not orthogonal");
   }

   /// <summary>A new coordinate system that has only a shift of origin (alignment is the same as world)</summary>
   public CoordSystem (Point3 org)
      => (Org, VecX, VecY) = (org, Vector3.XAxis, Vector3.YAxis);

   // Properties ---------------------------------------------------------------
   /// <summary>Is this similar to the world coordinate system?</summary>
   public bool IsWorld => Org.EQ (Point3.Zero) && VecX.EQ (Vector3.XAxis) && VecY.EQ (Vector3.YAxis);
   /// <summary>Is tihs the 'nil' coordinate system (uninitialized)</summary>
   public bool IsNil => Org.IsNil;

   /// <summary>The plane-def of the XY plane of the CoordSystem</summary>
   public PlaneDef PlaneDef => new (Org, VecZ);

   /// <summary>Origin of the CoordSystem</summary>
   public readonly Point3 Org;
   /// <summary>X-direction of the CoordSystem</summary>
   public readonly Vector3 VecX;
   /// <summary>Y-Direction of the CoordSystem</summary>
   public readonly Vector3 VecY;

   /// <summary>The Z-direction of the CoordSystem (computed)</summary>
   public Vector3 VecZ => VecX * VecY;

   /// <summary>This is the 'world' coordinate system (origin at 0,0,0, X and Y axes canonical)</summary>
   public static readonly CoordSystem World = new (Point3.Zero);
   /// <summary>The 'nil' coordinate system (uninitialized)</summary>
   public static readonly CoordSystem Nil = new (Point3.Nil);

   // Operators ----------------------------------------------------------------
   /// <summary>Shift a coordinate system by a given amount without rotating it</summary>
   public static CoordSystem operator + (CoordSystem cs, Vector3 vec)
      => new (cs.Org + vec, cs.VecX, cs.VecY);

   public override string ToString ()
      => $"CoordSystem:{Org.R6 ()},{VecX.R6 ()},{VecY.R6 ()}";
}
#endregion

#region struct PlaneDef ----------------------------------------------------------------------------
/// <summary>Represents the equation of a plane (using coefficients A,B,C,D)</summary>
public readonly struct PlaneDef {
   // Constructors -------------------------------------------------------------
   /// <summary>Creates a plane-def passing through 3 points, if possible</summary>
   public PlaneDef (Point3 p1, Point3 p2, Point3 p3) {
      A = p1.Y * (p2.Z - p3.Z) + p2.Y * (p3.Z - p1.Z) + p3.Y * (p1.Z - p2.Z);
      B = p1.Z * (p2.X - p3.X) + p2.Z * (p3.X - p1.X) + p3.Z * (p1.X - p2.X);
      C = p1.X * (p2.Y - p3.Y) + p2.X * (p3.Y - p1.Y) + p3.X * (p1.Y - p2.Y);
      D = -p1.X * (p2.Y * p3.Z - p3.Y * p2.Z) - p2.X * (p3.Y * p1.Z - p1.Y * p3.Z) - p3.X * (p1.Y * p2.Z - p2.Y * p1.Z);
      double f = Sqrt (A * A + B * B + C * C);
      if (f.IsZero ()) throw new InvalidOperationException ("Cannot create a PlaneDef with 3 collinear points");
      A /= f; B /= f; C /= f; D /= f;
   }

   /// <summary>Computes a plane-def given a point a normal</summary>
   public PlaneDef (Point3 pt, Vector3 normal) {
      normal = normal.Normalized ();
      A = normal.X; B = normal.Y; C = normal.Z;
      D = -(A * pt.X + B * pt.Y + C * pt.Z);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Coefficients of the plane equation</summary>
   /// These are normalized such that A^2 + B^2 + C^2 = 1
   public readonly double A, B, C, D;

   /// <summary>This is the normal vector of this plane</summary>
   public Vector3 Normal => new (A, B, C);

   /// <summary>Represents the XY plane (with normal facing in the +Z direction)</summary>
   public static readonly PlaneDef XY = new (Point3.Zero, Vector3.ZAxis);
   /// <summary>Represents the YZ plane (with normal facing in +X direction)</summary>
   public static readonly PlaneDef YZ = new (Point3.Zero, Vector3.XAxis);
   /// <summary>Represents the ZX plane (with normal facing in the +Y direction)</summary>
   public static readonly PlaneDef XZ = new (Point3.Zero, Vector3.YAxis);

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the absolute distance of a point from a plane</summary>
   public double Dist (Point3 pt) => Abs (SignedDist (pt));

   /// <summary>Get the line of intersection of two plane-defs</summary>
   /// If the two plane-defs are parallel, this returns false
   public bool Intersect (PlaneDef other, out Point3 pt, out Vector3 vec) {
      vec = Normal * other.Normal;
      if (vec.Length < 1e-6) { pt = Point3.Zero; return false; }
      vec = vec.Normalized ();

      // If we get here, we know that the line-of-intersection is parallel to v.
      // We just need to now find any one point on that line. We solve this by 
      // setting Z=0, then Y=0, then X=0, and solving the resulting pair of linear
      // equations - at least two of the three below should result in a solution
      _ = Lib.SolveLinearPair (A, B, D, other.A, other.B, other.D, out double x, out double y)
         || Lib.SolveLinearPair (A, C, D, other.A, other.C, other.D, out x, out _)
         || Lib.SolveLinearPair (B, C, D, other.B, other.C, other.D, out y, out _);
      pt = new (x, y, 0);
      return true;
   }

   /// <summary>Gets the intersection between a planedef and an infinite line</summary>
   /// <returns>The point of interesection, or Point3.Nil otherwise</returns>
   public Point3 Intersect (Point3 p1, Point3 p2) {
      double dx = p2.X - p1.X, dy = p2.Y - p1.Y, dz = p2.Z - p1.Z;
      double a = A * dx + B * dy + C * dz;
      if (a.IsZero ()) return Point3.Nil;
      double fLie = -(A * p1.X + B * p1.Y + C * p1.Z + D) / a;
      return new (p1.X + fLie * dx, p1.Y + fLie * dy, p1.Z + fLie * dz);
   }

   /// <summary>Snaps the given point to the closest point on the planedef</summary>
   public Point3 Snap (Point3 p) {
      double dist = -SignedDist (p);
      var pt = new Point3 (p.X + dist * A, p.Y + dist * B, p.Z + dist * C);
      return pt;
   }

   /// <summary>Given a point, returns the signed distance (+ve means to the left, -ve means to the right)</summary>
   public double SignedDist (Point3 pt) => A * pt.X + B * pt.Y + C * pt.Z + D;

   public override string ToString ()
      => $"PlaneDef:{A.R6 ()},{B.R6 ()},{C.R6 ()},{D.R6 ()}";
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
         throw new ArgumentException ("Value cannot be zero", nameof (axis));
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
