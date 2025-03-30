// ────── ╔╗ Nori™
// ╔═╦╦═╦╦╬╣ Copyright © 2025 Arvind
// ║║║║╬║╔╣║ PNGReader.cs ~ Implements a reader for PNG files
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.IO.Compression;
namespace Nori;

#region class PNGReader ----------------------------------------------------------------------------
/// <summary>Decoder for PNG files (converts them into NImage)</summary>
public class PNGReader : PNGCore {
   public PNGReader (byte[] data) => mStm = new (data);
   public PNGReader (string filename) : this (File.ReadAllBytes (filename)) { }

   // Methods ------------------------------------------------------------------
   /// <summary>Loads the actual PNG data from the stream</summary>
   public void Load () {
      for (int i = 0; i < 8; i++) 
         if (U8() != mSign[i]) Fatal ("Invalid header");

      // Then, loop over each chunk and load in the raw data
      for (; ; ) {
         int length = I32 ();                   // Chunk length
         uint crc1 = ComputeCRC (mStm.WorkBuffer.AsSpan (N, length + 4));
         EChunk chunk = (EChunk)U32 ();         // Chunk type
         ReadChunk (chunk, length);             // Actual chunk data
         uint crc2 = U32 ();
         Debug.Assert (crc1 == crc2);
         if (chunk == EChunk.IEND) break;
      }

      ApplyFilters ();
      ApplyPalette ();
   }
   readonly ReadStm mStm;
   int N => (int)mStm.Position;

   // Implementation -----------------------------------------------------------
   // Applies the per-line PNG filters
   void ApplyFilters () {
      //// Normally, each line would take mWidth * mBPP bytes (width * bytes-per-pixel).
      //// However, in the mRaw buffer, we have each line prefixed by a 1-byte 'filter policy'
      //// so the stride there is 1 more than that. The stride in the 'filtered' buffer is
      //// actually this width * byte-per-pixel value, which we call below as cbLine
      //int cbLine = mRawStride - 1;
      //mFilterStride = cbLine.RoundUp (4);
      //mFiltered = new byte[mHeight * mFilterStride];
      //for (int y = 0; y < mHeight; y++) {
      //   int src = y * mRawStride + 1;    // Start of source data for scanline y
      //   int dst = (mHeight - y - 1) * mFilterStride;     // Start of destination data for scanline y
      //   int filter = mData[src - 1];      // Filter policy for this scanline
      //   switch (filter) {
      //      case 0:     // None
      //         for (int x = 0; x < cbLine; x++)
      //            mFiltered[dst + x] = mData[src + x];
      //         break;
      //      case 1:     // Sub (each pixel relative to one on the left)
      //         for (int x = 0; x < mBPP; x++)
      //            mFiltered[dst + x] = mData[src + x];
      //         for (int x = mBPP; x < cbLine; x++)
      //            mFiltered[dst + x] = (byte)(mData[src + x] + mFiltered[dst + x - mBPP]);
      //         break;
      //      case 2:     // Up (each pixel relative to the one above
      //         for (int x = 0; x < cbLine; x++)
      //            mFiltered[dst + x] = (byte)(mData[src + x] + mFiltered[dst + x + cbLine]);
      //         break;
      //      case 3:     // 'Average' algorithm
      //         for (int x = 0; x < mBPP; x++) {
      //            int dst1 = dst + x;
      //            byte b1 = 0, b2 = mFiltered[dst1 + cbLine];
      //            mFiltered[dst1] = (byte)(mData[src + x] + (b1 + b2) / 2);
      //         }
      //         for (int x = mBPP; x < cbLine; x++) {
      //            int dst1 = dst + x;
      //            byte b1 = mFiltered[dst1 - mBPP], b2 = mFiltered[dst1 + cbLine];
      //            mFiltered[dst1] = (byte)(mData[src + x] + (b1 + b2) / 2);
      //         }
      //         break;
      //      default:
      //         throw new NotImplementedException ();
      //   }
      //}
   }

   // If this is a palette-based format, apply the palette (converting the palette indices
   // into actual color values)
   void ApplyPalette () {
      if ((mFormat & EFormat.Palette) == 0) return;
      int stride = (mWidth * 3).RoundUp (4);
      byte[] data = new byte[stride * mHeight];
      for (int y = 0; y < mHeight; y++) {
         int src = mFilterStride * y, dst = stride * y;
         for (int x = 0; x < mWidth; x++) {
            int entry = mFiltered[src++];    // Pallet entry to use
            for (int c = 0; c < 3; c++)
               data[dst++] = mPalette[entry * 3 + c];
         }
      }
      mFormat &= ~EFormat.Palette;
      mFiltered = data; mFilterStride = stride;
   }

   // Discard N number of bytes from the stream
   void Discard (int n) { mStm.Position += n; }

   // Aborts processing with a PNGRead exception
   static void Fatal (string message) => throw new PNGReadException (message);

   // Reads the next chunk fromthe PNG file and processes it
   void ReadChunk (EChunk chunk, int length) {
      switch (chunk) {
         case EChunk.IHDR: ReadIHDR (); break;
         case EChunk.IDAT: ReadIDat (length); break;
         case EChunk.PLTE: ReadPLTE (length); break;
         default: Discard (length); break;
      }
   }

   // Reads an IDAT chunk (actual pixel data, compressed with Deflate)
   void ReadIDat (int length) {
      int position = (int)mStm.Position;
      byte method = U8 (), flags = U8 ();    // Compression method, and flags
      if ((flags & 32) != 0) Discard (4);    // If bit 5 of flags is set, read and discard the DICTID
      var ds = new DeflateStream (mStm, CompressionMode.Decompress, true);
      mRawRead += ds.Read (mRaw, mRawRead, mRaw.Length - mRawRead);
      mStm.Position = position + length;
   }

   // Reads an IHDR chunk (header information)
   void ReadIHDR () {
      mWidth = I32 (); mHeight = I32 (); mBits = U8 (); mFormat = (EFormat)U8 ();
      if (mBits != 8 || (U8 () + U8 () + U8 () != 0)) Fatal ("Unsupported PNG format");
      mBPP = ((mFormat & EFormat.Alpha) != 0) ? 4 : 3;      // Each pixel has RGB or RGBA components
      if ((mFormat & EFormat.Palette) != 0) mBPP = 1;       // Each pixel is a palette index
      mRawStride = mWidth * mBPP + 1;
      mRaw = new byte[mRawStride * mHeight];
   }
   byte[] mRaw = [];
   int mRawRead;

   // Reads a PLTE chunk (pallet entries)
   void ReadPLTE (int length) {
      throw new NotImplementedException ();
   }

   // Reads a uint from the current location, advances stream by 1
   uint U32 () => (uint)((U8 () << 24) + (U8 () << 16) + (U8 () << 8) + U8 ());
   // Reads a byte from the current location, advances stream by 1
   byte U8 () => (byte)mStm.ReadByte ();
   // Reads a int from the current location, advances stream by 4
   int I32 () => (int)U32 ();

   // Private data -------------------------------------------------------------
   int mWidth;             // Image width, in pixels
   int mHeight;            // Image height, in pixels
   int mBits;              // Bits per component
   EFormat mFormat;        // Color format
   int mBPP;               // Bytes-per-pixel
   int mRawStride;         // Stride between lines in the mRaw buffer
   int mFilterStride;      // Stride between lines in mFiltered buffer 
   byte[] mFiltered = [];  // Filtered buffer (after applying filters)
   byte[] mPalette = [];   // Pallet entries (each 3 bytes defines one RGB)
}
#endregion

#region class PNGReadException ---------------------------------------------------------------------
/// <summary>Exception thrown if we encounter issues (or unsupported formats) when reading PNG files</summary>
public class PNGReadException (string message) : Exception (message) { }
#endregion

public class ReadStm : Stream {
   public ReadStm (byte[] data) => mData = data;
   readonly byte[] mData;
   int mUsed;

   public override bool CanRead => true;
   public override bool CanSeek => false;
   public override bool CanWrite => false;

   public override long Length => mData.Length;

   public override long Position { get => mUsed; set => mUsed = (int)value; }

   public override void Flush () { }

   public override int Read (byte[] buffer, int offset, int count) {
      int toread = Math.Min (count, mData.Length - mUsed - 1);
      Array.Copy (mData, mUsed, buffer, offset, toread); mUsed += toread;
      return toread;
   }

   public override int ReadByte () {
      if (mUsed >= mData.Length) return -1;
      return mData[mUsed++];
   }

   public override long Seek (long offset, SeekOrigin origin) => throw new NotImplementedException ();
   public override void SetLength (long value) => throw new NotImplementedException ();
   public override void Write (byte[] buffer, int offset, int count) => throw new NotImplementedException ();

   public byte[] WorkBuffer => mData;
}