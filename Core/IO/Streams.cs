// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Streams.cs
// ║║║║╬║╔╣║ Implements some special-purpose Stream classes (and related utilities)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class WriteStm -----------------------------------------------------------------------------
/// <summary>Implements a stream that writes to a memory-buffer</summary>
/// This is similar to a MemoryStream, but provides access to the underlying work buffer
/// of bytes. A typical use-case is where we use this for writing PNG files. 
/// 
/// A PNG file contains a set of chunks, each followed by a 32-bit CRC checksum. The
/// PNG writer uses this method when writing the chunks:
/// - At the start of every chunk, record where we are in the WriteStm (Position)
/// - Write the actual data (consists of integers, bytes, etc)
/// - Finally, use the WorkBuffer[] to fetch the range of bytes that were written out
///   as part of this chunk, and compute the CRC checksum.
/// This would be messy if we did not have access to the data that was written out 
/// as an array of bytes
/// 
/// Why not just use a simple growable array of bytes to write PNG files? The reason
/// is that some chunks are compressed (using the Deflate algorithm). The Stream interface
/// makes it trivial to slap a DeflateStream compressor on top of WriteStm temporarily,
/// and write the data through it (which causes it to be compressed on the way down to
/// WriteStm). Then, we can easily get the checksum of the _compressed_ data since that
/// is what is now present in WorkBuffer[]
class WriteStm : Stream {
   // Properties ---------------------------------------------------------------
   public override bool CanWrite => true;
   public override long Length => mLength;
   public override long Position { get => mPosition; set => mPosition = (int)value; }

   /// <summary>After the data is all written, read this property to get it captured into a byte[]</summary>
   /// The array is resized so it contains exactly as many bytes as were written
   public byte[] Data { get { Array.Resize (ref mData, mLength); return mData; } }

   /// <summary>Read this at any point to get a work-buffer that contains the data that has been written</summary>
   /// Note that this buffer may be longer than the number of bytes written so far,
   /// and you should use the Length property to figure out how many bytes of this buffer
   /// contain actual written data.
   public byte[] WorkBuffer => mData;
   byte[] mData = new byte[1024];

   // Methods ------------------------------------------------------------------
   public override void Write (byte[] buffer, int offset, int count) {
      while (mPosition + count >= mData.Length) Array.Resize (ref mData, mData.Length * 2);
      Array.Copy (buffer, offset, mData, mPosition, count);
      mPosition += count; mLength = Math.Max (mPosition, mLength);
   }
   int mLength, mPosition;

   public override void WriteByte (byte value) {
      if (mPosition + 1 >= mData.Length) Array.Resize (ref mData, mData.Length * 2);
      mData[mPosition++] = value; mLength = Math.Max (mPosition, mLength);
   }

   // Unimplemented methods and properties -------------------------------------
   public override bool CanRead => false;
   public override bool CanSeek => false;
   public override void Flush () { }
   public override int Read (byte[] buffer, int offset, int count) => throw new NotImplementedException ();
   public override long Seek (long offset, SeekOrigin origin) => throw new NotImplementedException ();
   public override void SetLength (long value) => throw new NotImplementedException ();
}
#endregion
