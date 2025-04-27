// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
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

public class UTFReader {
   public UTFReader (string file) => D = File.ReadAllBytes (file);

   readonly byte[] D;
   int mN;
   public int N => mN;

   public byte Peek { get { SkipSpace (); return D[mN]; } }

   public ReadOnlySpan<byte> GetSpan (int start, int length) => D.AsSpan (start, length);

   public UTFReader Match (char b) {
      SkipSpace ();
      if (D[mN++] != b) Fatal ($"Expecting '{b}', found '{(char)D[--mN]}'");
      return this;
   }

   public bool ReadBoolean () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out bool value, out int delta)) Fatal ("Expecting boolean");
      mN += delta; return value;
   }

   public int ReadInt32 () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out int value, out int delta)) Fatal ("Expecting int");
      mN += delta; return value;
   }

   public uint ReadUInt32 (bool hex) {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out uint value, out int delta, hex ? 'X' : '\0')) Fatal ("Expecting uint");
      mN += delta; return value;
   }

   public float ReadSingle () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out float value, out int delta)) Fatal ("Expecting float");
      mN += delta; return value;
   }

   public double ReadDouble () {
      SkipSpace ();
      if (!Utf8Parser.TryParse (D.AsSpan (mN), out double value, out int delta)) Fatal ("Expecting double");
      mN += delta; return value;
   }

   public string ReadString () {
      SkipSpace ();
      if (Peek == '"') {
         mN++;
         var s = Encoding.UTF8.GetString (TakeUntil (mQuote));
         mN++; return s;
      }
      return Encoding.UTF8.GetString (TakeUntil (mSpace));
   }
   static readonly SearchValues<byte> mQuote
      = SearchValues.Create ((byte)'"');

   public bool TryMatch (char b) {
      if (Peek == b) { Skip (); return true; }
      return false;
   }

   public void SkipTo (char b) {
      while (D[mN++] != b) { }
   }

   public ReadOnlySpan<byte> TakeUntil (SearchValues<byte> stopper) {
      SkipSpace (); int start = mN;
      while (!stopper.Contains (D[mN++])) { }
      return D.AsSpan (start, --mN - start);
   }

   public UTFReader SkipSpace () {
      while (mSpace.Contains (D[mN])) mN++;
      return this;
   }
   static readonly SearchValues<byte> mSpace
      = SearchValues.Create (9, 10, 11, 13, 32);

   public ReadOnlySpan<byte> Upto (char b) {
      int start = mN; SkipTo (b);
      return D.AsSpan (start, mN - start - 1);
   }

   void Fatal (string s) => throw new Exception (s);

   public override string ToString () {
      int length = Math.Min (D.Length - mN - 1, 100);
      return Encoding.UTF8.GetString (D.AsSpan (mN, length));
   }

   /// <summary>
   /// Skip one character
   /// </summary>
   public UTFReader Skip () { mN++; return this; }
}

public class UTFWriter {
   public UTFWriter Put (char ch) { Grow (1); D[N++] = (byte)ch; return this; }

   public UTFWriter Del () { N--; return this; }

   public byte[] Indented () {
      Stack<int> starts = [];
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
               MemoryExtensions.Replace (D.AsSpan (st, len), (byte)'\n', (byte)' ');
         }
      }

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

         void DoCopy () { while (cDone <= i) output.Add (D[cDone++]); }
         void Indent () { for (int j = 0; j < cLevel; j++) output.Add ((byte)' '); }
      }
      D = [.. output]; N = D.Length;
      return D;

      // Helpers .................................
      bool Bypass (byte starter, ref int idx, byte ender) {
         if (D[idx] == starter) {
            while (D[++idx] != ender) { }
            return true; 
         }
         return false;
      }

      static bool Starter (byte b) => b == '{' || b == '[';
      static bool Ender (byte b) => b == '}' || b == ']';
   }

   public UTFWriter Put (byte[] data) {
      int n = data.Length;
      Grow (n); data.CopyTo (D, N); N += n;
      return this;
   }
   public UTFWriter Put (ReadOnlySpan<byte> data) {
      int n = data.Length;
      Grow (n); data.CopyTo (D.AsSpan (N)); N += n;
      return this;
   }

   public void NL () {
      if (D[N - 1] != '\n') Put ('\n');
   }

   public UTFWriter Put (double f) {
      Grow (32);
      Utf8Formatter.TryFormat (f, D.AsSpan (N), out int cb); 
      N += cb; return this; 
   }

   public UTFWriter Put (int n) {
      Grow (32);
      Utf8Formatter.TryFormat (n, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public UTFWriter Put (float f) {
      Grow (32);
      Utf8Formatter.TryFormat (f, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public UTFWriter Put (bool v) {
      Grow (32);
      Utf8Formatter.TryFormat (v, D.AsSpan (N), out int cb);
      N += cb; return this; 
   }

   public UTFWriter Put (uint n, bool hex) {
      Grow (32);
      Utf8Formatter.TryFormat (n, D.AsSpan (N), out int cb, new System.Buffers.StandardFormat ('X'));
      N += cb; return this;
   }

   static SearchValues<char> mSpl = SearchValues.Create ("\'\":[{()}]=");

   public UTFWriter Put (string s) {
      bool quote = s.Any (a => a is < (char)32 or > (char)128) || s.Any (mSpl.Contains);
      if (quote) {
         s = s.Replace ('"', '\''); 
         if (quote) s = $"\"{s}\"";
      }
      Grow (Encoding.UTF8.GetByteCount (s));
      int cb = Encoding.UTF8.GetBytes (s, D.AsSpan (N));
      N += cb; return this; 
   }

   void Grow (int required) {
      while (N + required >= D.Length)
         Array.Resize (ref D, D.Length * 2);
   }

   byte[] D = new byte[256];
   int N;
}
