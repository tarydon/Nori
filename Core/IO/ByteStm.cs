// ────── ╔╗
// ╔═╦╦═╦╦╬╣ ByteStm.cs
// ║║║║╬║╔╣║ Implements type ByteStm (BinaryReader-style wrapper around byte-array)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class ByteStm ------------------------------------------------------------------------------
/// <summary>A wrapper over a byte array that allows us to read it in a stream-like fashion</summary>
public class ByteStm {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a ByteStm, given the raw array of bytes to work with</summary>
   public ByteStm (byte[] data) { mData = data; Position = 0; }
   #endregion

   #region Read .Net primitives -----------------------------------
   /// <summary>Reads a bool</summary>
   public unsafe bool ReadBoolean () { bool b; Read (&b, 1); return b; }
   /// <summary>Reads a char</summary>
   public unsafe char ReadChar () { char c; Read (&c, 2); return c; }
   /// <summary>Reads a DateTime</summary>
   public DateTime ReadDateTime () => DateTime.FromBinary (ReadInt64 ());
   /// <summary>Reads an Int16</summary>
   public unsafe short ReadInt16 () { short n; Read (&n, 2); return n; }
   /// <summary>Read an Int32</summary>
   public unsafe int ReadInt32 () { int n; Read (&n, 4); return n; }
   /// <summary>Reads an Int64</summary>
   public unsafe long ReadInt64 () { long n; Read (&n, 8); return n; }
   /// <summary>Reads a UInt16</summary>
   public unsafe ushort ReadUInt16 () { ushort n; Read (&n, 2); return n; }
   /// <summary>Reads a UInt32</summary>
   public unsafe uint ReadUInt32 () { uint n; Read (&n, 4); return n; }
   /// <summary>Reads a UInt64</summary>
   public unsafe ulong ReadUInt64 () { ulong n; Read (&n, 8); return n; }
   /// <summary>Reads a byte</summary>
   public unsafe byte ReadByte () { byte b; Read (&b, 1); return b; }
   /// <summary>Reads a signed-byte</summary>
   public unsafe sbyte ReadSByte () { sbyte b; Read (&b, 1); return b; }
   /// <summary>Reads a float</summary>
   public unsafe float ReadSingle () { float f; Read (&f, 4); return f; }
   /// <summary>Reads a double</summary>
   public unsafe double ReadDouble () { double f; Read (&f, 8); return f; }

   /// <summary>Reads a byte-array</summary>
   public byte[]? ReadByteArray () {
      int n = ReadIntV (); if (n == -1) return null;
      byte[] data = new byte[n];
      Array.Copy (mData, Position, data, 0, n); Position += n;
      return data;
   }
   /// <summary>Read a Guid</summary>
   public Guid ReadGuid () {
      byte[] tmp = new byte[16];
      Array.Copy (mData, Position, tmp, 0, 16); Position += 16;
      return new Guid (tmp);
   }
   /// <summary>Reads a string</summary>
   public string? ReadString () {
      int n = ReadIntV (); if (n == -1) return null;
      string s = Encoding.UTF8.GetString (mData, Position, n); Position += n;
      return s;
   }
   /// <summary>Reads an integer stored using a variable number of bytes (see WriteIntV for details)</summary>
   public unsafe int ReadIntV () {
      int n = 0; Read (&n, 1);
      if ((n & 128) == 0) return n - 1;
      int m = 0; Read (&m, 1); n |= (m & 127) << 7;
      if ((m & 128) == 0) return n - 1;
      Read (&m, 1); n |= (m & 127) << 14;
      if ((m & 128) == 0) return n - 1;
      Read (&m, 1); n |= (m & 127) << 21;
      if ((m & 128) == 0) return n - 1;
      Read (&m, 1); n |= (m & 127) << 28;
      return n - 1;
   }
   #endregion

   // Private data -------------------------------------------------------------
   readonly byte[] mData;
   public int Position;

   #region Implementation -----------------------------------------
   // Read N bytes from this ByteStm into the given destination
   public unsafe void Read (void* dest, int cb) {
      byte* pdest = (byte*)dest;
      for (int i = 0; i < cb; i++) pdest[i] = mData[Position++];
   }
}
#endregion
