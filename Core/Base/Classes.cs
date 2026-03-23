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

   /// <summary>Checks if this bitmap is identical to another, within a given threshold</summary>
   public bool Identical (DIBitmap other, byte threshold = 0) {
      if (Width != other.Width || Height != other.Height || Fmt != other.Fmt || Data.Length != other.Data.Length)
         return false;

      for (int i = 0; i < Data.Length; i++) {
         byte a = Data[i], b = other.Data[i];
         if (a == b) continue;
         if (threshold == 0 || Math.Abs (a - b) > threshold) return false;
      }
      return true;
   }

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
   readonly List<IDisposable?> mDisposables = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Add an additional disposable</summary>
   public void Add (IDisposable? disp) => mDisposables.Add (disp);

   // Implement IDisposable ----------------------------------------------------
   public void Dispose () { mDisposables.ForEach (a => a?.Dispose ()); mDisposables.Clear (); }
}
#endregion
