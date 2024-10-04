// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ GPUTypes.cs ~ Types designed for transmitting data to GPUs
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region Types for OpenGL ---------------------------------------------------------------------------
/// <summary>2D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 8)]
public readonly record struct Vec2F (float X, float Y) {
   public static implicit operator Vec2F (Point2 pt) => new ((float)pt.X, (float)pt.Y);
   public static explicit operator Vec2F (Vector2 vec) => new ((float)vec.X, (float)vec.Y);
   public static implicit operator Vector2 (Vec2F vec) => new (vec.X, vec.Y);
   public static readonly Vec2F Zero = new (0, 0);
   public bool EQ (Vec2F b) => X.EQ (b.X) && Y.EQ (b.Y);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()}>";
}

/// <summary>3D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 12)]
public readonly record struct Vec3F (float X, float Y, float Z) {
   public Vec3F (double x, double y, double z) : this ((float)x, (float)y, (float)z) { }
   public static explicit operator Vec3F (Point3 pt) => new ((float)pt.X, (float)pt.Y, (float)pt.Z);
   public static explicit operator Vec3F (Vector3 vec) => new ((float)vec.X, (float)vec.Y, (float)vec.Z);
   public static implicit operator Vector3 (Vec3F vec) => new (vec.X, vec.Y, vec.Z);
   public static readonly Vec3F Zero = new (0, 0, 0);
   public bool EQ (Vec3F b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()},{Z.R5 ()}>";
}

[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly record struct Vec4F (float X, float Y, float Z, float W) {
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

/// <summary>3D vector of 16-bit half (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 6)]
public readonly record struct Vec3H (Half X, Half Y, Half Z) {
   public bool EQ (Vec3H v) => X.EQ (v.X) && Y.EQ (v.Y) && Z.EQ (v.Z);
   public override string ToString () => $"<{X.R3 ()},{Y.R3 ()},{Z.R3 ()}>";
}

/// <summary>4D vector of floats (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly record struct Vec4H (float X, float Y, float Z, float W) {
   public bool EQ (Vec4H b) => X.EQ (b.X) && Y.EQ (b.Y) && Z.EQ (b.Z) && W.EQ (b.W);
   public override string ToString () => $"<{X.R5 ()},{Y.R5 ()},{Z.R5 ()},{W.R5 ()}>";
}

/// <summary>2D vector of shorts (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 4)]
public readonly record struct Vec2S (short X, short Y) {
   public Vec2S (int x, int y) : this ((short)x, (short)y) { }
   public bool EQ (Vec2S b) => X == b.X && Y == b.Y;
   public static readonly Vec2S Zero = new (0, 0);
   public static readonly Vec2S Nil = new (-32768, -32768);
   public bool IsNil => X == -32768 && Y == -32768;
   public Vec2S Move (int dx, int dy) => new (X + dx, Y + dy);
   public Vec2S Midpoint (Vec2S other) => new ((X + other.X + 1) / 2, (Y + other.Y + 1) / 2);
   public override string ToString () => $"<{X},{Y}>";
}

/// <summary>4D vector of shorts (used for passing data to OpenGL)</summary>
[StructLayout (LayoutKind.Sequential, Pack = 2, Size = 8)]
public readonly record struct Vec4S (short X, short Y, short Z, short W) {
   public Vec4S (int x, int y, int z, int w) : this ((short)x, (short)y, (short)z, (short)w) { }
   public bool EQ (Vec4S b) => X == b.X && Y == b.Y && Z == b.Z && W == b.W;
   public override string ToString () => $"<{X},{Y},{Z},{W}>";
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
   public void Deconstruct (out int left, out int bottom, out int right, out int top)
      => (left, bottom, right, top) = (Left, Bottom, Right, Top);

   public RectS (float left, float bottom, float right, float top) {
      (Left, Bottom, Right, Top) = ((short)(left + 0.5f), (short)(bottom + 0.5f), (short)(right + 0.5f), (short)(top + 0.5f));
      if (right < left || top < bottom) throw new NotImplementedException ();
   }

   public readonly RectS Shifted (int x, int y) => new (Left + x, Bottom + y, Right + x, Top + y);

   public static readonly RectS Empty = new (32767, 32767, 32767, 32767);
   public bool IsEmpty => Left == 32767;

   public readonly bool Contains (Vec2S p)
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

   public static explicit operator Vec2F (RectS cell) => new (cell.Width, cell.Height);

   public readonly short Left, Bottom, Right, Top;
}

/// <summary>Represents a 'margin', with components expressed as Float</summary>
/// This might look similar to RectF at first, but there the 4 values here are essentially independent,
/// and there are no constraints that Right should be more than or equal to Left, for example. 
[StructLayout (LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly record struct MarginF (float Left, float Top, float Right, float Bottom) : IEQuable<MarginF> {
   public MarginF (float v) : this (v, v, v, v) { }
   public MarginF (double v) : this ((float)v) { }
   public MarginF (double l, double t, double r, double b) : this ((float)l, (float)t, (float)r, (float)b) { }

   public override string ToString () {
      if (Left.EQ (Right) && Left.EQ (Top) && Left.EQ (Bottom)) return Left.ToString ();
      return $"{Left},{Top},{Right},{Bottom}";
   }

   public static readonly MarginF Zero = new (0, 0, 0, 0);
   public bool EQ (MarginF b) => Left.EQ (b.Left) && Right.EQ (b.Right) && Top.EQ (b.Top) && Bottom.EQ (b.Bottom);
   public float Horz => Left + Right;
   public float Vert => Top + Bottom;

   public static MarginF Parse (string s) {
      if (s.Contains (',')) {
         var v = s.Split (',').Select (float.Parse).ToList ();
         return new (v[0], v[1], v[2], v[3]);
      }
      return new (float.Parse (s));
   }

   public static implicit operator MarginF (int n) => new (n, n, n, n);
   public static implicit operator MarginF (double f) => new (f, f, f, f);
}
#endregion
