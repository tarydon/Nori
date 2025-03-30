// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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
      Gray8,
   }
}
#endregion

#region class MultiDispose -------------------------------------------------------------------------
/// <summary>Helper to hold on to, and dispose, multiple IDisposables</summary>
public class MultiDispose (params IDisposable?[] disps) : IDisposable {
   public void Dispose () => mDisposables.ForEach (a => a?.Dispose ());
   IDisposable?[] mDisposables = disps;
}
#endregion
