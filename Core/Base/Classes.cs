// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers.Text;

namespace Nori;

#region class DIBitmap -----------------------------------------------------------------------------
/// <summary>This represents a simple 'device-independent' bitmap in one of various formats</summary>
/// This is just a thin wrapper around an array of bytes holding pixel data. It
/// adds some key metadata like the width and height of the bitmap, and the 'format' by
/// which to interpret the bit values
public class DIBitmap {
   /// <summary>Construct a bitmap given the width, height, format and actual raw data</summary>
   /// Note that this format has no padding at the end of each line to align it
   /// on any 4-byte or 8-byte boundary. The data for successive lines is tightly
   /// packed. 
   public DIBitmap (int width, int height, EFormat fmt, byte[] data) {
      (Width, Height, Fmt, Data) = (width, height, fmt, data);
      Stride = Width * fmt.BytesPerPixel ();
   }
   public override string ToString () => $"DIBitmap: {Width}x{Height}, {Fmt}";

   /// <summary>Width of the bitmap in pixels</summary>
   public readonly int Width;
   /// <summary>Height of the bitmap in pixels</summary>
   public readonly int Height;
   /// <summary>Format of the bitmap</summary>
   public readonly EFormat Fmt;
   /// <summary>Stride between succsesive lines in the Data array</summary>
   public readonly int Stride;
   /// <summary>Raw data of the bitmap</summary>
   public readonly byte[] Data;

   /// <summary>Format of the bitmap</summary>
   public enum EFormat {
      Unknown,
      /// <summary>8-bit Red, Green, Blue components (24 bits per pixel)</summary>
      RGB8,
      /// <summary>8-bit Red, Green, Blue, Alpha components (32 bits per pixel)</summary>
      RGBA8,
      /// <summary>8-bit Grayscale values (8-bits per pixel)</summary>
      Gray8
   }
}
#endregion

#region class MultiDispose -------------------------------------------------------------------------
/// <summary>Helper to hold on to, and dispose, multiple IDisposables</summary>
public class MultiDispose : IDisposable {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a MultiDispose with zero or more disposables to hold on to</summary>
   public MultiDispose (params IDisposable?[] disps) => mDisposables.AddRange (disps);
   List<IDisposable?> mDisposables = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Add an additional disposable</summary>
   public void Add (IDisposable? disp) => mDisposables.Add (disp); 

   // Implement IDisposable ----------------------------------------------------
   public void Dispose () { mDisposables.ForEach (a => a?.Dispose ()); mDisposables.Clear (); }
}
#endregion

public class ByteWriter {
   public ByteWriter Put (char ch) { Grow (1); D[N++] = (byte)ch; return this; }

   public ByteWriter Put (byte[] data) {
      int n = data.Length;
      Grow (n); data.CopyTo (D, N); N += n;
      return this;
   }
   public ByteWriter Put (ReadOnlySpan<byte> data) {
      int n = data.Length;
      Grow (n); data.CopyTo (D.AsSpan (N)); N += n;
      return this;
   }

   public ByteWriter Put (double f) {
      Grow (32);
      Utf8Formatter.TryFormat (f, D.AsSpan (N), out int cb); 
      N += cb; return this; 
   }

   public ByteWriter Put (int n) {
      Grow (32);
      Utf8Formatter.TryFormat (n, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public ByteWriter Put (float f) {
      Grow (32);
      Utf8Formatter.TryFormat (f, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public ByteWriter Put (bool v) {
      Grow (32);
      Utf8Formatter.TryFormat (v, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public ByteWriter Put (uint n, bool hex) {
      Grow (32);
      Utf8Formatter.TryFormat (n, D.AsSpan (N), out int cb, new System.Buffers.StandardFormat ('X'));
      N += cb; return this;
   }

   public ByteWriter Put (string s) {
      Grow (Encoding.UTF8.GetByteCount (s));
      int cb = Encoding.UTF8.GetBytes (s, D.AsSpan (N));
      N += cb; return this; 
   }

   void Grow (int required) {
      while (N + required >= D.Length)
         Array.Resize (ref D, D.Length * 2);
   }

   public byte[] Trimmed () {
      Array.Resize (ref D, N);
      return D;
   }

   byte[] D = new byte[256];
   int N;
}
