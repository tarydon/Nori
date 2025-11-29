// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GPUTypes.cs
// ║║║║╬║╔╣║ Types designed for transmitting data to GPUs
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region Types for OpenGL ---------------------------------------------------------------------------
/// <summary>2D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 8)]
public readonly record struct Vec2F (float X, float Y) : IEQuable<Vec2F> {
   public Vec2F (double x, double y) : this ((float)x, (float)y) { }
   public static implicit operator Vec2F (Point2 pt) => new ((float)pt.X, (float)pt.Y);
   public static explicit operator Point2 (Vec2F vec) => new (vec.X, vec.Y);
   public static explicit operator Vec2F (Vector2 vec) => new ((float)vec.X, (float)vec.Y);
   public static implicit operator Vector2 (Vec2F vec) => new (vec.X, vec.Y);
   public static readonly Vec2F Zero = new (0, 0);
   public bool EQ (Vec2F b) => X.EQ (b.X) && Y.EQ (b.Y);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()}>";
}

/// <summary>2D vector of short-ints (used to represent viewport sizes etc)</summary>
public readonly record struct Vec2S (short X, short Y) : IEQuable<Vec2S> {
   public Vec2S (int x, int y) : this ((short)x, (short)y) { }
   public bool EQ (Vec2S b) => X == b.X && Y == b.Y;
   public override string ToString () => $"<{X},{Y}>";
   public static readonly Vec2S Zero = new (0, 0);
}

/// <summary>3D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 12)]
public readonly record struct Vec3F : IEQuable<Vec3F> {
   public Vec3F (float x, float y, float z) { X = x; Y = y; Z = z; }
   public Vec3F (double x, double y, double z) : this ((float)x, (float)y, (float)z) { }
   public static explicit operator Vec3F (Point3 pt) => new ((float)pt.X, (float)pt.Y, (float)pt.Z);
   public static explicit operator Vec3F (Vector3 vec) => new ((float)vec.X, (float)vec.Y, (float)vec.Z);
   public static implicit operator Vector3 (Vec3F vec) => new (vec.X, vec.Y, vec.Z);
   public static explicit operator Point3 (Vec3F vec) => new (vec.X, vec.Y, vec.Z);
   public static readonly Vec3F Zero = new (0, 0, 0);
   public bool EQ (Vec3F b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()},{Z.R5 ()}>";
   public readonly float X, Y, Z;
}

/// <summary>3D vector of 16-bit half (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 6)]
public readonly record struct Vec3H (Half X, Half Y, Half Z) : IEQuable<Vec3H> {
   public static explicit operator Vec3H (Vector3 vec) => new ((Half)vec.X, (Half)vec.Y, (Half)vec.Z);
   public static explicit operator Vector3 (Vec3H vec) => new ((double)vec.X, (double)vec.Y, (double)vec.Z);
   public bool EQ (Vec3H v) => X.EQ (v.X) && Y.EQ (v.Y) && Z.EQ (v.Z);
   public override string ToString () => $"<{X.R3 ()},{Y.R3 ()},{Z.R3 ()}>";
}

/// <summary>4D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly record struct Vec4F (float X, float Y, float Z, float W) : IEQuable<Vec4F> {
   public Vec4F (double x, double y, double z, double w) : this ((float)x, (float)y, (float)z, (float)w) { }
   public int CompareTo (Vec4F b) {
      int n = X.CompareTo (b.X); if (n != 0) return n;
      n = Y.CompareTo (b.Y); if (n != 0) return n;
      n = Z.CompareTo (b.Z); if (n != 0) return n;
      return W.CompareTo (b.W);
   }
   public bool EQ (Vec4F b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z) && W.EQ (b.W);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()},{Z.R5 ()},{W.R5 ()}>";
}

/// <summary>4D vector of 16-bit half (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly struct Vec4H (float x, float y, float z, float w) : IEQuable<Vec4H> {
   public bool EQ (Vec4H b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z) && W.EQ (b.W);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()},{Z.R5 ()},{W.R5 ()}>";
   public readonly float X = x, Y = y, Z = z, W = w;
}

/// <summary>4D vector of shorts (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 8)]
public readonly record struct Vec4S (short X, short Y, short Z, short W) {
   public Vec4S (int x, int y, int z, int w) : this ((short)x, (short)y, (short)z, (short)w) { }
   public bool EQ (Vec4S b) => X == b.X && Y == b.Y && Z == b.Z && W == b.W;
   public override string ToString () => $"<{X},{Y},{Z},{W}>";
}

/// <summary>4x4 transformation matrix, with float components</summary>
[StructLayout (LayoutKind.Sequential, Size = 64, Pack = 4)]
public readonly struct Mat4F {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Mat4f, given the 12 components</summary>
   public Mat4F (float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33, float x, float y, float z) {
      (M11, M12, M13, M14) = (m11, m12, m13, 0);
      (M21, M22, M23, M24) = (m21, m22, m23, 0);
      (M31, M32, M33, M34) = (m31, m32, m33, 0);
      (X, Y, Z, M44) = (x, y, z, 1);
   }

   public override string ToString ()
      => $"[{M11},{M12},{M13},{M14}, {M21},{M22},{M23},{M24}, {M31},{M32},{M33},{M34}, {X},{Y},{Z},{M44}]";

   public static readonly Mat4F Identity = new (1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);
   public static readonly Mat4F Zero = new (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

   // Properties ---------------------------------------------------------------
   public readonly float M11, M12, M13, M14;
   public readonly float M21, M22, M23, M24;
   public readonly float M31, M32, M33, M34;
   public readonly float X, Y, Z, M44;

   // Conversions --------------------------------------------------------------
   /// <summary>Convert a Matrix3 to a Mat4f</summary>
   public static explicit operator Mat4F (Matrix3 m)
      => new ((float)m.M11, (float)m.M12, (float)m.M13,
              (float)m.M21, (float)m.M22, (float)m.M23,
              (float)m.M31, (float)m.M32, (float)m.M33,
              (float)m.DX, (float)m.DY, (float)m.DZ);

   /// <summary>Returns true if two Mat4f are exactly the same</summary>
   public bool EQ (ref Mat4F b)
      => M11 == b.M11 && M12 == b.M12 && M13 == b.M13 && M14 == b.M14 && 
         M21 == b.M21 && M22 == b.M22 && M23 == b.M23 && M24 == b.M24 && 
         M31 == b.M31 && M32 == b.M32 && M33 == b.M33 && M34 == b.M34 && 
         X == b.X && Y == b.Y && Z == b.Z && M44 == b.M44;
}

/// <summary>An axis-aligned pixel-rectangle (components are shorts)</summary>
/// This follows OpenGL sign conventions : (0,0) is the bottom left corner of the screen,
/// and +X is right, +Y is up
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 8)]
public readonly struct RectS : IEQuable<RectS> {
   public RectS (int left, int bottom, int right, int top) {
      (Left, Bottom, Right, Top) = ((short)left, (short)bottom, (short)right, (short)top);
      if (right < left || top < bottom) throw new NotImplementedException ();
   }

   public RectS (float left, float bottom, float right, float top) {
      (Left, Bottom, Right, Top) = ((short)(left + 0.5f), (short)(bottom + 0.5f), (short)(right + 0.5f), (short)(top + 0.5f));
      if (right < left || top < bottom) throw new NotImplementedException ();
   }

   public RectS Shifted (int x, int y) => new (Left + x, Bottom + y, Right + x, Top + y);

   public static readonly RectS Empty = new (32767, 32767, 32767, 32767);
   public bool IsEmpty => Left == 32767;

   public bool Contains (Vec2S p)
      => Left <= p.X && p.X <= Right && Bottom <= p.Y && p.Y <= Top;

   public int Width => Right - Left;
   public int Height => Top - Bottom;
   public Vec2S Midpoint => new ((Left + Right) / 2, (Top + Bottom) / 2);
   public Vec2S BottomLeft => new (Left, Bottom);
   public Vec2S TopRight => new (Right, Top);
   public override string ToString () => $"[{Width}x{Height} @ {Left},{Bottom}]";

   public int CompareTo (RectS b) {
      int n = Left.CompareTo (b.Left); if (n != 0) return n;
      n = Bottom.CompareTo (b.Bottom); if (n != 0) return n;
      n = Right.CompareTo (b.Right); if (n != 0) return n;
      return Top.CompareTo (b.Top);
   }

   public bool EQ (RectS b)
      => Left == b.Left && Bottom == b.Bottom && Right == b.Right && Top == b.Top;

   public readonly short Left, Bottom, Right, Top;
}
#endregion
