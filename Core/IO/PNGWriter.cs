using System.IO.Compression;

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
   protected enum EFormat { Palette = 1, Color = 2, Alpha = 4 };

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

   public byte[] Write () {
      foreach (var b in mSign) mStm.WriteByte (b);
      WriteIHDR ();
      WriteIDAT ();
      WriteIEND ();      
      return mStm.Data;
   }

   void WriteIDAT () {
      U32 (1000);    // Don't know the actual length, so just write some dummy value
      int n = (int)mStm.Position;
      U32 (EChunk.IDAT);
      U8 (24); U8 (87);
      int stride = mBmp.Stride;
      using (var ds = new DeflateStream (mStm, CompressionLevel.SmallestSize, true)) {
         for (int i = 0; i < mBmp.Height; i++) {
            ReadOnlySpan<byte> row = mBmp.Data.AsSpan (i * stride, stride);
            ds.WriteByte (0); ds.Write (row);
         }
      }
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
      U32 (13);
      int n = (int)mStm.Position;
      U32 (EChunk.IHDR); I32 (mBmp.Width); I32 (mBmp.Height); U8 (8);
      U8 ((byte)EFormat.Color); U8 (0); U8 (0); U8 (0);
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
      Stride = Width * 3;
   }

   public readonly int Width;
   public readonly int Height;
   public readonly EFormat Fmt;
   public readonly byte[] Data;
   public readonly int Stride;

   public enum EFormat {
      RGB8,
      RGBA8,
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

