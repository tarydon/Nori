// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Matrix.cs
// ║║║║╬║╔╣║ Implements 2D and 3D Matrices
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Matrix2 ------------------------------------------------------------------------------
/// <summary>A Matrix in 2 dimensions</summary>
public class Matrix2 (double m11, double m12, double m21, double m22, double x, double y) {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a translation matrix</summary>
   public static Matrix2 Translation (Vector2 vec) => Translation (vec.X, vec.Y);
   /// <summary>Create a translation matrix</summary>
   public static Matrix2 Translation (double x, double y) => new (1, 0, 0, 1, x, y);

   /// <summary>Create a uniform scaling matrix (same scale in X, Y) about the origin</summary>
   public static Matrix2 Scaling (double scale) => new (scale, 0, 0, scale, 0, 0);
   /// <summary>Creates a scaling matrix (with different scales about X and Y) about the origin</summary>
   public static Matrix2 Scaling (double xScale, double yScale) => new (xScale, 0, 0, yScale, 0, 0);
   /// <summary>Creates a matrix, scaling about a specified center</summary>
   public static Matrix2 Scaling (Point2 center, double xScale, double yScale)
      => new (xScale, 0, 0, yScale, center.X - xScale * center.X, center.Y - yScale * center.Y);

   /// <summary>Create a rotation matrix about the center</summary>
   public static Matrix2 Rotation (double angle) => Rotation (Point2.Zero, angle);
   /// <summary>Create a matrix, rotating about an arbitrary center</summary>
   public static Matrix2 Rotation (Point2 center, double angle) {
      if (angle.IsZero ()) return Identity;
      var (s, c) = (Sin (angle), Cos (angle));
      var (dx, dy) = (center.X * (1 - c) + center.Y * s, center.Y * (1 - c) - center.X * s);
      return new (c, s, -s, c, dx, dy);
   }

   /// <summary>Creates a mirror matrix about the line specified by the two points</summary>
   public static Matrix2 Mirror (Point2 p1, Point2 p2) {
      // Note: Simplified impl. Not considering mirror axis passing through origin! (Rare alignment)
      double dx = p2.X - p1.X;
      if (dx.IsZero ()) // Arbitrary vertical axis
         return Translation (-p1.X, 0) * HMirror * Translation (p1.X, 0);

      double dy = p2.Y - p1.Y;
      if (dy.IsZero ()) // Arbitrary horizontal axis
         return Translation (0, -p1.Y) * VMirror * Translation (0, p1.Y);

      double fAng = Atan2 (dy, dx); // Note: fAng != 0 (by design)
      Matrix2 mat = Translation (-p1.X, -p1.Y) * Rotation (-fAng);
      return mat * VMirror * mat.GetInverse ();
   }

   public override string ToString () => $"[{M11.R6 ()},{M12.R6 ()} | {M21.R6 ()},{M22.R6 ()} | {DX.R6 ()},{DY.R6 ()}]";

   // Properties ---------------------------------------------------------------
   public readonly double M11 = m11, M12 = m12, M21 = m21, M22 = m22, DX = x, DY = y;
   public static readonly Matrix2 Identity = new (1, 0, 0, 1, 0, 0);
   /// <summary>A matrix that mirrors about Y (horizontally)</summary>
   public static readonly Matrix2 HMirror = new (-1, 0, 0, 1, 0, 0);
   /// <summary>A matrix that mirrors about X (vertically)</summary>
   public static readonly Matrix2 VMirror = new (1, 0, 0, -1, 0, 0);

   /// <summary>The 'scaling factor' of this matrix (assuming equal scaling in all axes)</summary>
   public double ScaleFactor => (Vector2.XAxis * this).Length;

   // Methods ------------------------------------------------------------------
   /// <summary>Computes the inverse of a matrix (throws an exception for a singular matrix)</summary>
   public Matrix2 GetInverse () {
      double d = M11 * M22 - M12 * M21;     // The determinant
      if (d == 0) throw new Exception ("Inverting a singular matrix");
      d = 1 / d;
      return new (M22 * d, -M12 * d, -M21 * d, M11 * d, (M21 * DY - M22 * DX) * d, (M12 * DX - M11 * DY) * d);
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Multiply a Point2 by a Matrix</summary>
   public static Point2 operator * (Point2 p, Matrix2 m)
      => new (m.M11 * p.X + m.M21 * p.Y + m.DX, m.M12 * p.X + m.M22 * p.Y + m.DY);
   /// <summary>Multiply a Vec2F by a Matrix (returns a Vec2F)</summary>
   public static Vec2F operator * (Vec2F p, Matrix2 m)
      => new (m.M11 * p.X + m.M21 * p.Y + m.DX, m.M12 * p.X + m.M22 * p.Y + m.DY);
   /// <summary>Multiply a Vector2 by a Matrix</summary>
   public static Vector2 operator * (Vector2 v, Matrix2 m)
      => new (m.M11 * v.X + m.M21 * v.Y, m.M12 * v.X + m.M22 * v.Y);

   /// <summary>Multiply two matrices together</summary>
   public static Matrix2 operator * (Matrix2 a, Matrix2 b) =>
      new (a.M11 * b.M11 + a.M12 * b.M21, a.M11 * b.M12 + a.M12 * b.M22,
           a.M21 * b.M11 + a.M22 * b.M21, a.M21 * b.M12 + a.M22 * b.M22,
           a.DX * b.M11 + a.DY * b.M21 + b.DX, a.DX * b.M12 + a.DY * b.M22 + b.DY);

   /// <summary>Convert a Matrix2 to an equivalent Matrix3</summary>
   public static explicit operator Matrix3 (Matrix2 m)
      => new (m.M11, m.M12, 0, m.M21, m.M22, 0, 0, 0, 1, m.DX, m.DY, 0);
}
#endregion

#region class Matrix3 ------------------------------------------------------------------------------
/// <summary>Matrix working in 3 dimensions</summary>
public class Matrix3 {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Matrix3 given the 12 components</summary>
   /// Since we use this only to support affine matrices, some of the 4x4 components are
   /// implicit : M14 = M24 = M34 = 0, and M44 = 1. This variant of the Matrix3 constructor
   /// calls ComputeFlags to set the Flags. The Flags are useful to identify special cases like
   /// pure translation, pure rotation etc (for performance improvement)
   public Matrix3 (double m11, double m12, double m13, double m21, double m22, double m23,
      double m31, double m32, double m33, double dx, double dy, double dz) {
      M11 = m11; M12 = m12; M13 = m13; M21 = m21; M22 = m22; M23 = m23;
      M31 = m31; M32 = m32; M33 = m33; DX = dx; DY = dy; DZ = dz;
      Flags = EFlag.Rotate; Flags = ComputeFlags ();
   }

   /// <summary>The Identity matrix</summary>
   public static readonly Matrix3 Identity
      = new (1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, EFlag.Zero);

   /// <summary>Construct a matrix to map a given 2-D bound to opengl clip-space</summary>
   /// This first adjusts the bound so that it matches the aspect ratio of the given
   /// viewport. Then, it constructs a matrix to map this bound to the OpenGL clip space,
   /// which extends from -1 to +1 in every direction.
   public static Matrix3 Map (Bound2 window, Vec2S viewport) {
      Point2 mid = window.Midpoint;
      double dx = Max (window.Width / 2.0, 1), dy = Max (window.Height / 2.0, 1);
      double aspect = Max (viewport.X, 1.0) / Max (viewport.Y, 1.0);
      if (dx / dy > aspect) dy = dx / aspect;
      else dx = dy * aspect;

      // Now, dx is half the width of the viewport, and dy is half the height of the
      // viewport. We want to first translate the midpoint to 0,0 and then scale the
      // viewport so the X/Y span is from -1 to +1
      return Translation (-mid.X, -mid.Y, 0) * Scaling (1 / dx, 1 / dy, 1);
   }

   /// <summary>Creates an orthographic projection matrix</summary>
   /// This maps the world-space cuboid (designated by Bound3) to the OpenGL clip space
   /// which extends from -1 to +1 in every direction. It is up to the caller to ensure that
   /// the X and Y aspect of the Bound3 passed in match that of the viewport, else distortion
   /// will occur in X and Y
   public static Matrix3 Orthographic (Bound3 bound) {
      double dx = 1 / bound.X.Length, dy = 1 / bound.Y.Length, dz = 1 / bound.Z.Length;
      Point3 mid = bound.Midpoint;
      return new (2 * dx,          0,               0,
                  0,               2 * dy,          0,
                  0,               0,               -2 * dz,
                  -2 * dx * mid.X, -2 * dy * mid.Y, -2 * dz * mid.Z,
                  EFlag.Translate | EFlag.Scale | EFlag.Mirror);
   }

   /// <summary> Compose a rotation matrix about one of the 3 basic axes</summary>
   public static Matrix3 Rotation (EAxis a, double angle) {
      if (angle.IsZero ()) return Identity;
      double c = Cos (angle), s = Sin (angle);
      return a switch {
         EAxis.X => new Matrix3 (1, 0, 0, 0, c, s, 0, -s, c, 0, 0, 0, EFlag.Rotate),
         EAxis.Y => new Matrix3 (c, 0, -s, 0, 1, 0, s, 0, c, 0, 0, 0, EFlag.Rotate),
         _ => new Matrix3 (c, s, 0, -s, c, 0, 0, 0, 1, 0, 0, 0, EFlag.Rotate)
      };
   }

   /// <summary>Matrix of rotation about an arbitrary axis passing through the origin</summary>
   public static Matrix3 Rotation (Vector3 axis, double angle) {
      if (angle.IsZero ()) return Identity;
      Vector3 v = axis.Normalized ();
      double c = Cos (-angle), s = Sin (-angle), f = 1 - c;
      return new Matrix3 (
         f * v.X * v.X + c, f * v.X * v.Y - s * v.Z, f * v.X * v.Z + s * v.Y,
         f * v.X * v.Y + s * v.Z, f * v.Y * v.Y + c, f * v.Y * v.Z - s * v.X,
         f * v.X * v.Z - s * v.Y, f * v.Y * v.Z + s * v.X, f * v.Z * v.Z + c,
         0, 0, 0, EFlag.Rotate
      );
   }

   /// <summary>Matrix of rotation about an arbitary axis (not necessarily passing through the origin)</summary>
   public static Matrix3 Rotation (Point3 a, Point3 b, double angle) {
      if (angle.IsZero ()) return Identity;
      return Translation (-a.X, -a.Y, -a.Z) *
             Rotation (b - a, angle) *
             Translation (a.X, a.Y, a.Z);
   }

   /// <summary>Construct a rotation matrix corresponding to the given Quaternion</summary>
   public static Matrix3 Rotation (Quaternion q) {
      if (q.Angle.IsZero ()) return Identity;
      double x = q.X, y = q.Y, z = q.Z, w = q.W;
      return new (
         1 - 2 * y * y - 2 * z * z, 2 * x * y + 2 * w * z, 2 * x * z - 2 * w * y,
         2 * x * y - 2 * w * z, 1 - 2 * x * x - 2 * z * z, 2 * y * z + 2 * w * x,
         2 * x * z + 2 * w * y, 2 * y * z - 2 * w * x, 1 - 2 * x * x - 2 * y * y,
         0, 0, 0, EFlag.Rotate
      );
   }

   /// <summary>Compose a uniform scaling matrix</summary>
   public static Matrix3 Scaling (double s)
      => new (s, 0, 0, 0, s, 0, 0, 0, s, 0, 0, 0);

   /// <summary>Compose a non-uniform scaling matrix (separate scaling factors in X, Y, Z)</summary>
   public static Matrix3 Scaling (double xs, double ys, double sz)
      => new (xs, 0, 0, 0, ys, 0, 0, 0, sz, 0, 0, 0, EFlag.Scale);

   /// <summary>Compose a translation matrix, given the 3 components</summary>
   public static Matrix3 Translation (double dx, double dy, double dz)
      => Translation (new Vector3 (dx, dy, dz));

   /// <summary>Compose a translation matrix, given the vector of translation</summary>
   public static Matrix3 Translation (Vector3 v)
      => v.Length.IsZero () ? Identity : new (1, 0, 0, 0, 1, 0, 0, 0, 1, v.X, v.Y, v.Z, EFlag.Translate);

   // Properties ---------------------------------------------------------------
   /// <summary>Is this an identity matrix</summary>
   public bool IsIdentity => (Flags & EFlag.All) == 0;

   /// <summary>Is this a pure translation matrix</summary>
   public bool IsTranslation => (Flags & EFlag.All) == EFlag.Translate;

   /// <summary>Is this a pure rotation matrix</summary>
   public bool IsRotation => (Flags & EFlag.All) == EFlag.Rotate;

   /// <summary>Does this matrix include mirroring?</summary>
   public bool HasMirroring => (Flags & EFlag.Mirror) != 0;

   /// <summary>The 'scaling factor' of this matrix (assuming equal scaling in all axes)</summary>
   public double ScaleFactor => (Vector3.XAxis * this).Length;

   // Methods ------------------------------------------------------------------
   public Matrix3 ExtractRotation () => new (M11, M12, M13, M21, M22, M23, M31, M32, M33, 0, 0, 0);

   /// <summary>Composes a matrix to go FROM the given coordinate system to the World</summary>
   public static Matrix3 From (in CoordSystem cs) {
      GetRotations (cs.VecX, cs.VecY, out double xRot, out double yRot, out double zRot);
      return Translation (-(Vector3)cs.Org)
           * Rotation (Vector3.YAxis, yRot)
           * Rotation (Vector3.ZAxis, zRot)
           * Rotation (Vector3.XAxis, xRot);
   }

   /// <summary>Returns the inverse of this matrix</summary>
   public Matrix3 GetInverse () {
      // Trivial cases first
      if (IsIdentity) return this;
      if (IsTranslation) return Translation (new (-DX, -DY, -DZ));
      if (IsRotation) return new (M11, M21, M31, M12, M22, M32, M13, M23, M33, 0, 0, 0, EFlag.Rotate);

      // Inverse of a translation + rotation matrix can be simply computed:
      if (Flags == (EFlag.Translate | EFlag.Rotate))
         return new (M11, M21, M31, M12, M22, M32, M13, M23, M33,
                     -(DX * M11 + DY * M12 + DZ * M13), -(DX * M21 + DY * M22 + DZ * M23), -(DX * M31 + DY * M32 + DZ * M33),
                     EFlag.Translate | EFlag.Rotate);

      // We support only affine matrices, so the inverse computation is a bit more smple
      double m11 = M22 * M33 - M32 * M23, m12 = M32 * M13 - M12 * M33, m13 = M12 * M23 - M22 * M13;
      double d = M31 * m13 + M21 * m12 + M11 * m11;
      if (Abs (d) < 1e-24) d = 1e-24;   // TODO: Trying to invert a singular matrix
      double a = 1.0 / d;
      double m21 = M31 * M23 - M21 * M33, m22 = M11 * M33 - M31 * M13, m23 = M21 * M13 - M11 * M23;
      double m31 = M21 * M32 - M31 * M22, m32 = M31 * M12 - M11 * M32, m33 = M11 * M22 - M21 * M12;
      double a5 = M11 * DY - DX * M12, a3 = M21 * DY - DX * M22, a2 = M31 * DY - DX * M32;
      double dx = M33 * a3 - DZ * m31 - M23 * a2, dy = M13 * a2 - M33 * a5 - DZ * m32, dz = M23 * a5 - DZ * m33 - M13 * a3;
      return new (m11 * a, m12 * a, m13 * a, m21 * a, m22 * a, m23 * a, m31 * a, m32 * a, m33 * a, dx * a, dy * a, dz * a, Flags);
   }

   /// <summary>Composes a matrix to go TO the given coordinate-system from the World</summary>
   public static Matrix3 To (in CoordSystem cs) {
      GetRotations (cs.VecX, cs.VecY, out double xRot, out double yRot, out double zRot);
      return Rotation (Vector3.XAxis, -xRot)
           * Rotation (Vector3.ZAxis, -zRot)
           * Rotation (Vector3.YAxis, -yRot)
           * Translation ((Vector3)cs.Org);
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Multiply two matrices together</summary>
   public static Matrix3 operator * (Matrix3 a, Matrix3 b) {
      // First handle the simpler special cases:
      // 1. One of them is an identity matrix
      if (a.IsIdentity) return b;
      if (b.IsIdentity) return a;

      // 2. Both are translation
      if (a.IsTranslation && b.IsTranslation)
         return Translation (a.DX + b.DX, a.DY + b.DY, a.DZ + b.DZ);

      // 3. Left is translation, right is rotation
      if (a.IsTranslation && b.IsRotation)
         return new (b.M11, b.M12, b.M13, b.M21, b.M22, b.M23, b.M31, b.M32, b.M33, a.DX * b.M11 + a.DY * b.M21 + a.DZ * b.M31,
                     a.DX * b.M12 + a.DY * b.M22 + a.DZ * b.M32, a.DX * b.M13 + a.DY * b.M23 + a.DZ * b.M33, EFlag.Translate | EFlag.Rotate);

      // 4. Left is rotation, and right is translation matrix
      if (a.IsRotation && b.IsTranslation)
         return new (a.M11, a.M12, a.M13, a.M21, a.M22, a.M23, a.M31, a.M32, a.M33,
                     a.DX + b.DX, a.DY + b.DY, a.DZ + b.DZ, EFlag.Rotate | EFlag.Translate);

      // 5. Both are rotation matrices
      if (a.IsRotation && b.IsRotation)
         return new (a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
                     a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
                     a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
                     a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
                     a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
                     a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
                     a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
                     a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
                     a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33,
                     0, 0, 0, EFlag.Rotate);

      // No simple cases found, handle the general case
      var flag = a.Flags | b.Flags;
      if (a.HasMirroring && b.HasMirroring) flag &= ~EFlag.Mirror;
      return new (a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
                  a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
                  a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
                  a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
                  a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
                  a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
                  a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
                  a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
                  a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33,
                  a.DX * b.M11 + a.DY * b.M21 + a.DZ * b.M31 + 1 * b.DX,
                  a.DX * b.M12 + a.DY * b.M22 + a.DZ * b.M32 + 1 * b.DY,
                  a.DX * b.M13 + a.DY * b.M23 + a.DZ * b.M33 + 1 * b.DZ,
                  flag);
   }

   /// <summary>Multiply a Point3 by a Matrix</summary>
   public static Point3 operator * (Point3 p, Matrix3 m) {
      if (m.IsIdentity) return p;
      if (m.IsTranslation) return new (p.X + m.DX, p.Y + m.DY, p.Z + m.DZ);
      double x = p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.DX;
      double y = p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.DY;
      double z = p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.DZ;
      return new (x, y, z);
   }
   /// <summary>Multiply a Vector3 by a Matrix</summary>
   public static Vector3 operator * (Vector3 v, Matrix3 m) {
      if (m.IsIdentity || m.IsTranslation) return v;
      double x = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31;
      double y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32;
      double z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33;
      return new (x, y, z);
   }

   // Nested types -------------------------------------------------------------
   // These are possible values for the Flags enumeration
   [Flags]
   enum EFlag {
      Zero = 0, Translate = 1, Rotate = 2, Mirror = 4, Scale = 8,
      All = Translate | Rotate | Mirror | Scale
   }

   // Implementation -----------------------------------------------------------
   // Helper used to construct a Matrix3 when the Flags values are known
   Matrix3 (double m11, double m12, double m13, double m21, double m22, double m23,
      double m31, double m32, double m33, double dx, double dy, double dz, EFlag flags) {
      M11 = m11; M12 = m12; M13 = m13; M21 = m21; M22 = m22; M23 = m23;
      M31 = m31; M32 = m32; M33 = m33; DX = dx; DY = dy; DZ = dz; Flags = flags;
   }

   /// <summary>Given an arbitrary vector vx and another one vy, computes rotations to align them to a coordinate system</summary>
   /// This computes the xRot, yRot, zRot which must be applied in Y-Z-X order to align the
   /// given vectors with the reference system; vx goes to X vector, vy goes to the +ve XY plane.
   /// If vx and vy were mutually perpendicular to start with, vy will then go to the Y vector
   static void GetRotations (Vector3 vx, Vector3 vy, out double xRot, out double yRot, out double zRot) {
      xRot = yRot = zRot = 0;
      // Rotate about Y to bring pb into the XY plane (pb.Z = 0)
      if (Abs (vx.Z) > 1e-12 || Abs (vx.X) > 1e-12) {
         yRot = Lib.HalfPI - Atan2 (vx.X, vx.Z);
         vx = vx.Rotated (EAxis.Y, yRot); vy = vy.Rotated (EAxis.Y, yRot);
      }
      // Next, rotate about Z to align pb with the positive X axis (pb.Y = 0)
      if (Abs (vx.Y) > 1e-12 || Abs (vx.X) > 1e-12) {
         zRot = -Atan2 (vx.Y, vx.X);
         vy = vy.Rotated (EAxis.Z, zRot);
      }
      // Next, rotate about X to align p3 with the +ve Y plane
      if (Abs (vy.Z) > 1e-12 || Abs (vy.Y) > 1e-12) {
         xRot = -Atan2 (vy.Z, vy.Y);
      }
   }

   // Helper used to compute the flags
   EFlag ComputeFlags () {
      EFlag tmp = 0;
      if (!DX.IsZero () || !DY.IsZero () || !DZ.IsZero ()) tmp |= EFlag.Translate;
      Vector3 vx = Vector3.XAxis * this, vy = Vector3.YAxis * this, vz = Vector3.ZAxis * this;
      if (!vx.Length.EQ (1.0)) tmp |= EFlag.Scale;
      vx = vx.Normalized (); vy = vy.Normalized (); vz = vz.Normalized ();
      if (!vx.EQ (Vector3.XAxis) || !vy.EQ (Vector3.YAxis)) tmp |= EFlag.Rotate;
      if ((vx * vy).Opposing (vz)) tmp |= EFlag.Mirror;
      return tmp;
   }

   public override string ToString ()
      => string.Format ("[{0},{1},{2}, {3},{4},{5}, {6},{7},{8}, {9},{10},{11}]",
            M11.S6 (), M12.S6 (), M13.S6 (), M21.S6 (), M22.S6 (), M23.S6 (),
            M31.S6 (), M32.S6 (), M33.S6 (), DX.S6 (), DY.S6 (), DZ.S6 ());

   // Private data -------------------------------------------------------------
   internal readonly double M11, M12, M13, M21, M22, M23, M31, M32, M33, DX, DY, DZ;
   readonly EFlag Flags;
}
#endregion
