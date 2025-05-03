// ────── ╔╗
// ╔═╦╦═╦╦╬╣ UTFWriter.cs
// ║║║║╬║╔╣║ Implements UTFWriter, an alternative to TextWriter that writes UTF8 to a byte-array
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using System.Buffers.Text;
namespace Nori;

#region class UTFWriter ----------------------------------------------------------------------------
/// <summary>UTFWriter is an alternative to TextWriter that produces an UTF8 stream directly</summary>
/// When using TextWriter, we need to convert most primitives like doubles, ints etc to
/// strings first (using their ToString() methods), and then write those out to the stream.
/// This creates a lot of small, short-lived strings. UTF8Writer is an alternative that
/// helps to avoid these allocations. It has methods that can write out all primitives types
/// directly to a growing array of bytes without intermediate conversion to string. This
/// is done using the .Net Core System.Buffers.UTF8Formatter class.
///
/// Most of the methods return the UTFWriter so it is easy to chain them together in a
/// fluent manner like wr.Write (pt.X).Write (',').Write (pt.Y)
public class UTFWriter {
   // Methods ------------------------------------------------------------------
   /// <summary>Do a 'backspace' by one character (effectively erase the last character we wrote)</summary>
   public UTFWriter Back () { N--; return this; }

   /// <summary>Called after writing an entire stream to perform indentation</summary>
   /// This is called to 'pretty up' the stream for better readability. Even without
   /// this call, the result is a well formed CURL file, though difficult to read and
   /// spanning a large number of lines. This pretty-prints the CURL with proper indentation
   /// and compacting up the data so small classes and arrays fit on one line rather than
   /// spanning multiple lines
   public byte[] IndentAndReturn () {
      Stack<int> starts = [];
      // First, try to see if all contents between any matching pair of [] or {}
      // can be put on a single line
      for (int i = 0; i < N; i++) {
         // Skip past comments, and skip past content within a string
         if (Bypass ((byte)';', ref i, (byte)'\n')) continue;
         if (Bypass ((byte)'"', ref i, (byte)'"')) continue;

         byte b = D[i];
         if (Starter (b)) starts.Push (i);
         else if (Ender (b)) {
            // Found a block. If it's less than 80 chars long, replace all \n
            // in this block with spaces
            int st = starts.Pop (), len = i - st;
            if (len < 80)
               D.AsSpan (st, len).Replace ((byte)'\n', (byte)' ');
         }
      }

      // Next, for each block delimited by [] or {} that spans multiple lines,
      // indent all the contents of the intermediate lines
      int cLevel = 0, cDone = 0;
      List<byte> output = [];
      for (int i = 0; i < N; i++) {
         if (Bypass ((byte)';', ref i, (byte)'\n')) continue;
         if (Bypass ((byte)'"', ref i, (byte)'"')) continue;
         byte b = D[i];
         if (Starter (b)) cLevel += 2;
         else if (Ender (b)) cLevel -= 2;
         else if (b == '\n') {
            if (cLevel > 0 && Ender (D[i + 1])) {
               DoCopy (); cLevel -= 2; i++;
               Indent ();
               continue;
            }
            DoCopy (); Indent ();
         }

         // Helpers ........................................
         void DoCopy () { while (cDone <= i) output.Add (D[cDone++]); }
         void Indent () { for (int j = 0; j < cLevel; j++) output.Add ((byte)' '); }
      }

      D = [.. output]; N = D.Length;
      return D;

      // Helpers ...........................................
      bool Bypass (byte starter, ref int idx, byte ender) {
         if (D[idx] == starter) {
            while (D[++idx] != ender) { }
            return true;
         }
         return false;
      }
      static bool Starter (byte b) => b == '{' || b == '[' || b == '<';
      static bool Ender (byte b) => b == '}' || b == ']' || b == '>';
   }

   /// <summary>Writes a NewLine character ('\n') to the stream, avoiding multiple \n</summary>
   /// If the last written character was a newline, this does not output another
   /// newline
   public void NewLine () {
      if (N > 0 && D[N - 1] != '\n') Write ('\n');
   }

   // Writers ------------------------------------------------------------------
   /// <summary>Write a boolean to the stream (writes True of False)</summary>
   public UTFWriter Write (bool value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write an array of bytes to the stream</summary>
   /// The input value is typically a string or a char encoded using
   /// Encoding.UTF8.GetBytes
   public UTFWriter Write (byte[] value) {
      EnsureSize (mDelta = value.Length);
      value.CopyTo (D, N); return Bump ();
   }

   /// <summary>Write one character to a UF8 stream</summary>
   /// If the character is less than 128, it takes only one byte and we can write
   /// it out directly (byte value same as char value). Otherwise, we use the UTF8
   /// encoding to convert it to a series of bytes
   public UTFWriter Write (char value) {
      if (value < 128) { EnsureSize (1); D[N++] = (byte)value; return this;  }
      return Write (Encoding.UTF8.GetBytes ([value]));
   }

   /// <summary>Write a DateTime to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (DateTime value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta, sDateFmt)) Grow ();
      return Bump ();
   }
   static StandardFormat sDateFmt = new ('O');

   /// <summary>Write a double to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (double value) => Write (value, default);
   /// <summary>Write a double to a UTF8 stream with specified formatting</summary>
   public UTFWriter Write (double value, StandardFormat fmt) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta, fmt)) Grow ();
      return Bump ();
   }

   /// <summary>Write a float to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (float value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write a Guid to a UTF8 stream</summary>
   public UTFWriter Write (Guid value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write an integer to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (int value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write a 16-bit integer to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (short value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write a 64-bit integer to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (long value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write a readonly span of bytes to the stream</summary>
   public UTFWriter Write (ReadOnlySpan<byte> value) {
      EnsureSize (mDelta = value.Length);
      value.CopyTo (D.AsSpan (N)); return Bump ();
   }

   /// <summary>Write a string to the stream</summary>
   /// If the string contains one of the special characters, it is quoted.
   /// Double quotes in the string are replaced with single quotes (this is temporary,
   /// and later we will implement a proper 'escape sequence mechanism' to handle this.
   public UTFWriter Write (string s) {
      bool quote = s.Any (a => a is < (char)33 or > (char)128) || s.Any (mSpl.Contains) || s == "";
      if (quote) {
         s = s.Replace ('"', '\'');
         if (quote) s = $"\"{s}\"";
      }
      EnsureSize (Encoding.UTF8.GetByteCount (s));
      int cb = Encoding.UTF8.GetBytes (s, D.AsSpan (N));
      N += cb; return this;
   }
   static SearchValues<char> mSpl = SearchValues.Create (" \'\":[{(<>)}]=");

   /// <summary>Write a TimeSpan to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (TimeSpan value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write an unsigned integer to the stream in default or hexadecimal formatting</summary>
   /// If hexadecimal formatting is used, this does not write out any leading
   /// specifiers like # or \x - you need to write those out explicitly if needed
   public UTFWriter Write (uint n, bool hex) {
      while (!Utf8Formatter.TryFormat (n, D.AsSpan (N), out mDelta, hex ? sHexFormat : default)) Grow ();
      return Bump ();
   }
   static StandardFormat sHexFormat = new ('X');

   /// <summary>Write a 16-bit unsigned integer to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (ushort value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   /// <summary>Write a 64-bit unsigned integer to a UTF8 stream using default formatting</summary>
   public UTFWriter Write (ulong value) {
      while (!Utf8Formatter.TryFormat (value, D.AsSpan (N), out mDelta)) Grow ();
      return Bump ();
   }

   // Implementation -----------------------------------------------------------
   // Called to bump up the write pointer by the variable mDelta (which is set
   // by most of the Write routines to indicate how many bytes have been written)
   UTFWriter Bump () {
      N += mDelta;
      if (N > D.Length) throw new NotImplementedException ();
      return this;
   }
   int mDelta;

   // Grows the buffer if the required number of bytes is not available
   // in the buffer
   void EnsureSize (int required) {
      while (N + required >= D.Length)
         Array.Resize (ref D, D.Length * 2);
   }

   // Double the size of the buffer
   void Grow () => Array.Resize (ref D, D.Length * 2);

   public ReadOnlySpan<byte> Trimmed () => D.AsSpan (0, N);

   byte[] D = new byte[256];  // The byte-array that we grow as needed
   int N;                     // The 'write-pointer' into that array
}
#endregion
