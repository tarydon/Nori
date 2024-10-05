// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Structs.cs
// ║║║║╬║╔╣║ Various Miscellaneous structs used by the Nori application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region struct BlockTimer --------------------------------------------------------------------------
/// <summary>A simple utility class that times how long a block takes</summary>
/// Use this BlockTimer in a using statement that wraps around the block to be timed
public readonly struct BlockTimer : IDisposable {
   /// <summary>Construct a Blocktimer, given the text to display when the block finishes</summary>
   public BlockTimer (string text) => (mText, mStart) = (text, DateTime.Now);

   public void Dispose () {
      double time = (DateTime.Now - mStart).TotalMilliseconds;
      Console.WriteLine ($"{mText}: {time:F2} ms");
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
   /// <summary>Converts the color to a Vec4H with the X,Y,Z,W components mapping to R,G,B,A</summary>
   public static explicit operator Vec4H (Color4 c) => new (c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

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

#region struct NRange ------------------------------------------------------------------------------
/// <summary>Represents an 32-bit integer range (inclusive Min .. exclusive Max)</summary>
public readonly struct NRange {
   public NRange (int min, int max) {
      if (min > max) (min, max) = (max, min);
      (Min, Max) = (min, max);
   }

   public readonly bool EQ (NRange b) => Min == b.Min && Max == b.Max;

   public readonly int Min;
   public readonly int Max;
   public readonly bool IsEmpty => Min == Max;
   public static readonly NRange Empty = new (0, 0);
}
#endregion
