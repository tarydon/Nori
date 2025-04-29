// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UTFReader.cs
// ║║║║╬║╔╣║ Implements UTFReader, an alternative to TextReader that reads UTF8 from a byte-array
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using System.Buffers.Text;
namespace Nori;

#region clss UTFReader -----------------------------------------------------------------------------
/// <summary>UTFReader is an alternative to TextReader to read from a UTF8 stream directly</summary>
/// Using a TextReader (by reading text with File.ReadAllText or File.ReadAllLines) involves 
/// converting text from UTF8 (in which most files are encoded) into UTF16 (which is how 
/// chars are rprsented). We can avoid that cost by using UTFReader, which directly converts
/// from UTF8 byte sequences into types like double, int etc. It uses UTF8Parser to do the 
/// actual parsing
public class UTFReader {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a UTFReader given an array of bytes</summary>
   public UTFReader (byte[] data) => Max = (D = data).Length;

   /// <summary>Construct a UTFReader by reading data from a file</summary>
   public UTFReader (string file) : this (File.ReadAllBytes (file)) => mFile = file;
   string? mFile;

   // Properties ---------------------------------------------------------------
   /// <summary>Matches and discards a given byte</summary>
   /// If the given byte is not found as the next character, throws an exception
   public UTFReader Match (char b) {
      SkipSpace ();
      if (D[mN++] != b) Fatal ($"Expecting '{b}', found '{(char)D[--mN]}'");
      return this;
   }

   /// <summary>Gets a span of bytes (starting at the given position, and of given length)</summary>
   public ReadOnlySpan<byte> GetSpan (int start, int length) => D.AsSpan (start, length);

   /// <summary>Peeks at the next character in the stream, skipping past whitespace</summary>
   /// If we are already at the end of the stream, this throws an exception
   public byte Peek { get { SkipSpace (); return D[mN]; } }

   public bool TryPeek (out byte b) {
      SkipSpace ();
      if (mN < Max) { b = D[mN]; return true; }
      b = 0; return false;
   }

   /// <summary>Read a boolean value from the stream (skips past leading whitespace)</summary>
   public bool ReadBoolean () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out bool value, out int delta)) Fatal ("Expecting bool value");
      mN += delta; return value;
   }

   /// <summary>Reads a DateTime value from the stream (skips past leading whitespace)</summary>
   public DateTime ReadDateTime () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out DateTime value, out int delta)) Fatal ("Expecting DateTime value");
      mN += delta; return value;
   }

   /// <summary>Read a double value from the stream (skips past leading whitespace)</summary>
   public double ReadDouble () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out double value, out int delta)) Fatal ("Expecting double value");
      mN += delta; return value;
   }

   /// <summary>Reads a Guid value from the stream (skips past leading whitespace)</summary>
   public Guid ReadGuid () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out Guid value, out int delta)) Fatal ("Expecting Guid value");
      mN += delta; return value; 
   }

   /// <summary>Reads an Int16 value from the stream (skips past leading whitespace)</summary>
   public short ReadInt16 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out short value, out int delta)) Fatal ("Expecting short value");
      mN += delta; return value;
   }

   /// <summary>Read an Int32 value from the stream (skips past leading whitespace)</summary>
   public int ReadInt32 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out int value, out int delta)) Fatal ("Expecting int value");
      mN += delta; return value;
   }

   /// <summary>Read an Int64 value from the stream (skips past leading whitespace)</summary>
   public long ReadInt64 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out long value, out int delta)) Fatal ("Expecting long value");
      mN += delta; return value;
   }

   /// <summary>Reads a Single value from the stream (skips past leading whitespace)</summary>
   public float ReadSingle () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out float value, out int delta)) Fatal ("Expecting float value");
      mN += delta; return value;
   }

   /// <summary>Reads a string from the stream (if the string is "quoted", removes the quotes)</summary>
   public string ReadString () {
      SkipSpace ();
      if (Peek == '"') {
         mN++;
         var s = Encoding.UTF8.GetString (TakeUntil (sQuote, false));
         mN++; return s;
      }
      return Encoding.UTF8.GetString (TakeUntil (sSpace, false));
   }
   static readonly SearchValues<byte> sQuote = SearchValues.Create ((byte)'"');
   
   /// <summary>Reads a TimeSpan value from the stream (skips past leading whitespace)</summary>
   public TimeSpan ReadTimeSpan () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out TimeSpan value, out int delta)) Fatal ("Expecting TimeSpan value");
      mN += delta; return value; 
   }

   /// <summary>Reads an ushort value from the stream (skips past leading whitespace)</summary>
   public ushort ReadUInt16 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out ushort value, out int delta)) Fatal ("Expecting ushort value");
      mN += delta; return value;
   }

   /// <summary>Read an UInt32 value from the stream, in decimal or hexadecimal format (skips past leading whitespace)</summary>
   public uint ReadUInt32 (bool hex) {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out uint value, out int delta, hex ? 'X' : '\0')) Fatal ("Expecting uint value");
      mN += delta; return value;
   }

   /// <summary>Read an UInt64 value from the stream (skips past leading whitespace)</summary>
   public ulong ReadUInt64 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out ulong value, out int delta)) Fatal ("Expecting ulong value");
      mN += delta; return value;
   }

   /// <summary>Skip one character</summary>
   public UTFReader Skip () { mN++; return this; }

   /// <summary>Skip past any of the values</summary>
   public UTFReader Skip (SearchValues<byte> noise) {
      for (; ; ) {
         if (mN == Max) return this;
         if (!noise.Contains (D[mN++])) break;
      }
      mN--;
      return this; 
   }

   /// <summary>Skips past any whitespace</summary>
   public UTFReader SkipSpace () {
      while (mN < Max && sSpace.Contains (D[mN])) mN++;
      return this;
   }
   static readonly SearchValues<byte> sSpace = SearchValues.Create (9, 10, 11, 13, 32);

   /// <summary>SKips until the given character is found (and consumes that character)</summary>
   public void SkipTo (char b) { while (D[mN++] != b) { } }

   /// <summary>Tries to match the given character, if it is found</summary>
   /// If the next character in the stream (skipping past whitespace) is the given 
   /// character, this consumes that character and returns true. Otherwise, it leaves the
   /// character unread, and returns false
   public bool TryMatch (char b) {
      if (Peek == b) { Skip (); return true; }
      return false;
   }

   /// <summary>This reads characters until one of the given 'stop' characters is found</summary>
   /// This returns that set of characters as a ReadOnlySpan. The stop character
   /// itself is not read in (since it could be any one of the stopper characters)
   public ReadOnlySpan<byte> TakeUntil (SearchValues<byte> stopper, bool skipSpace) {
      if (skipSpace) SkipSpace (); int start = mN;
      while (!stopper.Contains (D[mN++])) { }
      return D.AsSpan (start, --mN - start);
   }

   /// <summary>This reads characters until the given stop character is found</summary>
   /// This returns the set of characters as a ReadOnlySpan. Unlike the other
   /// TakeUntil method, this consumes the stop character itself.
   public ReadOnlySpan<byte> TakeUntil (char b) {
      int start = mN; SkipTo (b);
      return D.AsSpan (start, mN - start - 1);
   }

   // Implementation -----------------------------------------------------------
   [DoesNotReturn]
   void Fatal (string s) {
      // Convert the current position into a Line,Column within the text
      int nLine = D.Take (mN).Count (a => a == '\n') + 1, nColumn = mN + 1;
      if (nLine > 0) 
         for (int n = mN - 1; n >= 0; n--) if (D[n] == '\n') { nColumn = mN - n; break; }
      var sb = new StringBuilder ();
      sb.Append ($"At ({nLine},{nColumn})");
      if (mFile != null) sb.Append ($" of {mFile}");
      sb.Append ($": {s}");
      Except.Parse (sb.ToString ());
   }

   public override string ToString () {
      int length = Math.Min (Max - mN - 1, 100);
      return Encoding.UTF8.GetString (D.AsSpan (mN, length));
   }

   readonly byte[] D;
   readonly int Max;
   int mN;
}
#endregion
