﻿using System.IO.Compression;

namespace Nori;

#region class PNGCore ------------------------------------------------------------------------------
/// <summary>
/// PNGCore is the base class for both PNGReader and PNGWriter
/// </summary>
abstract public class PNGCore {
   // Internal types -----------------------------------------------------------
   protected enum EChunk : uint {
      IHDR = 0x49484452, sRGB = 0x73524742, gAMA = 0x67414D41, PLTE = 0x504C5445,
      pHYs = 0x70485973, IDAT = 0x49444154, IEND = 0x49454E44,
   };

   [Flags]
   protected enum EFormat { Gray = 0, Palette = 1, Color = 2, Alpha = 4 };

   // Implementation -----------------------------------------------------------
   protected uint ComputeCRC (ReadOnlySpan<byte> data) {
      // If the CRC seed table has not yet been computed, compute that first
      if (mCRCTable == null) {
         mCRCTable = new uint[256];
         for (uint n = 0; n < 256; n++) {
            uint c = n;
            for (int k = 0; k < 8; k++) {
               if ((c & 1) != 0)
                  c = 0xEDB88320 ^ (c >> 1);
               else
                  c = c >> 1;
            }
            mCRCTable[n] = c;
         }
      }

      uint crc = 0xFFFFFFFF;
      foreach (var b in data) 
         crc = mCRCTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
      return crc ^ 0xFFFFFFFF;
   }
   static uint[]? mCRCTable;

   // Private data -------------------------------------------------------------
   protected static readonly byte[] mSign = [137, 80, 78, 71, 13, 10, 26, 10]; // PNG file signature
}
#endregion

/*
class PNGWriter (Stream stm, DIBitmap bmp) : PNGCore (stm) {
   public void Save () {
      for (int i = 0; i < 8; i++) W (mSign[i]);
   }

   // Implementation -----------------------------------------------------------
   void W (byte b) => mStm.WriteByte (b);

   // Private data -------------------------------------------------------------
   readonly DIBitmap mBmp = bmp;
}
*/

public class PNGWriter : PNGCore {
   public PNGWriter (DIBitmap bmp) {
      mBmp = bmp; mStm = new ();
   }

   public void Write (string file) {
      File.WriteAllBytes (file, Write ());
   }

   public byte[] Write () {
      foreach (var b in mSign) mStm.WriteByte (b);
      WriteIHDR ();
      WriteIDAT ();
      WriteIEND ();      
      return mStm.Data;
   }

   void WriteIDAT () {
      // We don't know the actual length (of the compressed data), so just write some
      // dummy value now - we'll come back and update this at the end of this function
      U32 (1000);
      int n = (int)mStm.Position;
      U32 (EChunk.IDAT);
      U8 (24); U8 (87);    // CompressionMethod and Flags for Deflate (always the same?)
      int stride = mBmp.Stride, bpp = mBmp.Fmt.BytesPerPixel (), size = 4 + stride;

      byte[] prior = new byte[size], current = new byte[size];
      byte[][] filtered = [current, new byte[size], new byte[size]];
      int[] cost = new int[3];

      using (var ds = new DeflateStream (mStm, CompressionLevel.SmallestSize, true)) {
         for (int i = 0; i < mBmp.Height; i++) {
            // Copy one row of data into 'row' (starting at offset 4, so that we don't have to
            // do any special case handling for the leftmost pixel)
            Array.Copy (mBmp.Data, i * stride, current, 4, stride);

            // For each row, we are going to compute a good filter type by computing all the 
            // possible filter values, and evaluating which would be best. Since the 'best' 
            // means the minimum data after compression, it is not easy to evaluate accurately
            // without actually compressing (which we don't want to do). As an approximation, we
            // are going to simply 'sum' the filtered values and pick the one with the minimum sum.
            // (This is the heuristic recommended in the PNG specification). Note that we are only
            // trying the NONE, SUB and UP filters for simplicity. In practice, I could not find
            // any PNG samples where adding the AVG or PAETH filters improved things noticeably.

            // The data unmodified is filter type NONE.
            // cost[0] is going to be the cost for the NONE filter
            cost[0] = Sum (current);

            // Compute the SUB filter, and its cost
            byte[] sub = filtered[1];
            for (int x = 4; x < size; x++) sub[x] = (byte)(current[x] - current[x - bpp]);
            cost[1] = Sum (sub);

            // Compute the UP filter, and its cost
            byte[] up = filtered[2];
            for (int x = 4; x < size; x++) up[x] = (byte)(current[x] - prior[x]);
            cost[2] = Sum (up);

            int filter = cost.MinIndex ();
            byte[] towrite = filtered[filter];
            ds.WriteByte ((byte)filter); ds.Write (towrite.AsSpan (4, stride));

            // Now store the 'current' row as the 'prior' row and continue
            (prior, current) = (current, prior);
            filtered[0] = current;

            // Helper ............................
            static int Sum (byte[] data) {
               int sum = 0;
               foreach (var b in data) sum += b;
               return sum;
            }
         }
      }

      // Now that we've finished writing the compressed data, we know the compressed
      // length so we can go back and update that in the chunk header
      int length = (int)mStm.Position - n - 4;  
      U32 (ComputeCRC (mStm.WorkBuffer.AsSpan (n, length + 4)));
      mStm.Position = n - 4; I32 (length);
      mStm.Position = mStm.Length;

   }

   void WriteIEND () {
      U32 ((uint)0);
      int n = (int)mStm.Position;
      U32 (EChunk.IEND);
      U32 (ComputeCRC (mStm.WorkBuffer.AsSpan (n, 4))); 
   }

   void WriteIHDR () {
      U32 (13);      // Length of IHDR is always 13 bytes
      int n = (int)mStm.Position;
      U32 (EChunk.IHDR); I32 (mBmp.Width); I32 (mBmp.Height); U8 (8);
      var fmt = mBmp.Fmt switch {
         DIBitmap.EFormat.RGB8 => EFormat.Color,
         DIBitmap.EFormat.RGBA8 => EFormat.Color | EFormat.Alpha,
         DIBitmap.EFormat.Gray8 => EFormat.Gray,
         _ => throw new BadCaseException (mBmp.Fmt),
      };
      U8 ((byte)fmt); U8 (0); U8 (0); U8 (0);
      U32 (ComputeCRC (mStm.WorkBuffer.AsSpan (n, 17))); // 13 byte data, 4 byte chunk-type
   }

   void U8 (uint b) => mStm.WriteByte ((byte)b);
   void U32 (uint v) { U8 (v >> 24); U8 (v >> 16); U8 (v >> 8); U8 (v); }
   void I32 (int v) => U32 ((uint)v);
   void U32 (EChunk v) => U32 ((uint)v);

   readonly WriteStm mStm;
   readonly DIBitmap mBmp;
}

public class DIBitmap {
   public DIBitmap (int width, int height, EFormat fmt, byte[] data) {
      (Width, Height, Fmt, Data) = (width, height, fmt, data);
      Stride = Width * fmt.BytesPerPixel ();
   }

   public override string ToString () => $"DIBitmap: {Width}x{Height}, {Fmt}";

   public readonly int Width;
   public readonly int Height;
   public readonly EFormat Fmt;
   public readonly byte[] Data;
   public readonly int Stride;

   public enum EFormat {
      Unknown,
      RGB8,
      RGBA8,
      Gray8,
   }
}

/// <summary>
/// Implements a stream that writes to a memory-buffer
/// </summary>
/// This is similar to a MemoryStream, but provides access to the underlying 
class WriteStm : Stream {
   public override bool CanRead => false;
   public override bool CanSeek => false;
   public override bool CanWrite => true;

   public override long Length => mLength;

   public override long Position { get => mPosition; set => mPosition = (int)value; }
   public override void Flush () { }

   public override int Read (byte[] buffer, int offset, int count) => throw new NotImplementedException ();
   public override long Seek (long offset, SeekOrigin origin) => throw new NotImplementedException ();
   public override void SetLength (long value) => throw new NotImplementedException ();

   public override void Write (byte[] buffer, int offset, int count) {
      while (mPosition + count >= mData.Length) Array.Resize (ref mData, mData.Length * 2);
      Array.Copy (buffer, offset, mData, mPosition, count);
      mPosition += count; mLength = Math.Max (mPosition, mLength);
   }

   public override void WriteByte (byte value) {
      if (mPosition + 1 >= mData.Length) Array.Resize (ref mData, mData.Length * 2);
      mData[mPosition++] = value; mLength = Math.Max (mPosition, mLength);
   }

   public byte[] WorkBuffer => mData;
   byte[] mData = new byte[1024];

   int mLength = 0, mPosition = 0; 

   public byte[] Data { get { Array.Resize (ref mData, mLength); return mData; } }
}
