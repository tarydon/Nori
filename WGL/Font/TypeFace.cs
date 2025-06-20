// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TypeFace.cs
// ║║║║╬║╔╣║ Wrapper around a FreeType typeface
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using Nori.FreeType;
namespace Nori;
using static Nori.FreeType.FreeType;
using Ptr = nint;

#region class TypeFace -----------------------------------------------------------------------------
/// <summary>TypeFace is a wrapper around a FreeType typeface</summary>
/// The typeface can be loaded from a TTF/OTF file, or from a memory block 
/// containing that data. A typeface is a font file + a given pixel size
public class TypeFace {
   // Constructors -------------------------------------------------------------
   /// <summary>Load a typeface from a TTF file</summary>
   public TypeFace (string fontFile, int pixelSize, double gamma = 1.5) {
      try {
         mGamma = gamma;
         Check (NewFace (Library, fontFile, 0, out mFace));
         InitFont (pixelSize);
      } catch (Exception e) {
         throw new IOException ($"Unable to load TypeFace from file '{fontFile}'", e);
      }
   }
   readonly HFace mFace;   // Freetype handle

   /// <summary>Load a typeface from a font file that is already loaded into memory</summary>
   public TypeFace (byte[] data, int pixelSize) {
      mFaceRawBlock = GCHandle.Alloc (data, GCHandleType.Pinned);
      Check (NewFace (Library, mFaceRawBlock.AddrOfPinnedObject (), data.Length, 0, out mFace));
      InitFont (pixelSize);
   }
   readonly GCHandle mFaceRawBlock;    // Unmanaged-memory copy of the byte-array

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the indices of all glyphs available in this font</summary>
   public IReadOnlyList<uint> AllGlyphs { get { _ = Map; return mAllGlyphs!; } }
   List<uint>? mAllGlyphs;

   /// <summary>The different encodings available for this font</summary>
   internal FTEncoding[] Encodings {
      get {
         if (mEncodings == null) {
            int count = mRec.CCharMaps;
            mEncodings = new FTEncoding[count];
            unsafe {
               IntPtr* array = (IntPtr*)mRec.Charmaps;
               for (int i = 0; i < count; i++) {
                  CharMap cmap = Marshal.PtrToStructure<CharMap> (*array);
                  mEncodings[i] = cmap.Encoding;
                  array++;
               }
            }
         }
         return mEncodings;
      }
   }
   FTEncoding[]? mEncodings;

   /// <summary>Returns an always-available default typeface (Roboto, 18px)</summary>
   public static TypeFace Default {
      get => mDefault ??= new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), 18);
      set => mDefault = value;
   }
   static TypeFace? mDefault;

   /// <summary>Set the Gamma value to use when rasterizing the font</summary>
   /// Monitor gamma values range from 1.8 to 2.2 typically, so we pick 2.0 as a close-enough
   /// default. The gamma correction is needed because the 'glyph bitmaps' generated by FreeType
   /// are 'coverage' bitmaps which indicate what fraction of each pixel are 'covered' by the font,
   /// and we need to translate those coverage values to appropriate gray-scale values for OpenGL 
   /// to use. 
   public double Gamma {
      get => mGamma;
      set {
         if (mGamma.EQ (value)) return;
         mGamma = value; mGammaMap = null;
         Bump ();
      }
   }
   double mGamma = 1.5;

   /// <summary>Handle for the entire Freetype library</summary>
   static HLibrary Library => sLazy2.Value;
   static readonly Lazy<HLibrary> sLazy2 = new (() => { Check (Init (out var lib)); return lib; });

   /// <summary>The line-to-line distance (in pixels)</summary>
   public int LineHeight => mLineHeight;
   int mLineHeight;

   /// <summary>Sets the pixel-size for subsequent use</summary>
   /// This regenerates all the glyphs with the new size
   public (int X, int Y) PixelSize {
      get => mPixelSize;
      set {
         mPixelSize = value;
         Check (SetPixelSizes (mFace, (uint)value.X, (uint)value.Y));
         mRec = Marshal.PtrToStructure<CFace> ((Ptr)mFace);
         var size = Marshal.PtrToStructure<SizeRec> (mRec.Size);
         mLineHeight = (int)(size.Height.Value + 0.5);
         Bump ();
      }
   }
   (int X, int Y) mPixelSize;
   CFace mRec;    // Marshalled struct from the C library

   /// <summary>Returns a large texture image that contains all the glyphs in the font</summary>
   /// The texture is always exactly 8192 pixels wide, and a certain number of pixels high (large
   /// enough to accomodate all the glyphs). It might be simpler to consider it as a single linear
   /// array of bytes, and each glphy then has a single integer TexOffset into this long buffer. 
   /// Suppose that glyph is 8 pixels wide and 12 pixels high. Then, starting at TexOffset, the
   /// first 8 bytes are the first row of the character (topmost row), the next 8 rows are the 
   /// next row. So the next consecutive 96 bytes encode this character. These bytes should be 
   /// treated as an (already gamma corrected) alpha value and can then be used for drawing the
   /// character. 
   /// See the Text.frag shader for code that takes the X,Y offsets into the character cell (which
   /// for this example would go from 0..7 in X and 0..11 in Y), and converts them into a linear 
   /// delta from 0..95 starting at that character's TexOffset into this long linear buffer. That
   /// shader has to also then convert this 'linear offset' into S,T coordinates into that texture,
   /// but that is simple since these textures are always exactly 8192 pixels wide 
   internal HTexture Texture {
      get {
         if (mTexture == 0) mTexture = BuildTexture ();
         return mTexture;
      }
   }
   HTexture mTexture;

   /// <summary>UID for this font (changes when the size changes!)</summary>
   public int UID => mUID;
   static int sNextUID;
   int mUID;

   // Methods ------------------------------------------------------------------
   /// <summary>Gets the glyph index for a particular char (0 if the char does not exist)</summary>
   public uint GetGlyphIndex (char ch) => Map.SafeGet (ch);

   /// <summary>Gets the kerning adjustment between two glyphs (specified by indices idx0, idx1)</summary>
   /// The kerning adjustment is rounded to the nearest integer (since we cannot handle fractional
   /// pixel positionings of glyphs)
   public int GetKerning (uint idx0, uint idx1) {
      GetCharKerning (mFace, idx0, idx1, 0, out var kerning);
      return (int)kerning.X.Value;
   }

   /// <summary>Get metrics data for a given character</summary>
   public ref Metrics GetMetrics (char ch) => ref GetMetrics (GetGlyphIndex (ch));

   /// <summary>Get metrics data for a given glyph index</summary>
   public ref Metrics GetMetrics (uint n) => ref Notes[n];

   /// <summary>This 'measures' the text and returns the bounding box of it</summary>
   /// This assumes we are drawing the text with the baseline-start at (0,0). As with
   /// all pixel coordinates, the top left of the screen is 0,0 and +Y goes downwards. 
   /// This returns the bounding rectangle of such a text. 
   /// <param name="text">The text to measure</param>
   /// <param name="exact">If set, the Y extents are tightly aligned to the extents of the
   /// given text. Otherwise, they are set to the general ascender / descender of this font,
   /// and thus more useful for alignment, planning and positioning</param>
   public RectS Measure (string text, bool exact = false) {
      uint idx0 = 0;
      int x = 0, y = 0;
      int xMin = 9999, yMin = 0, xMax = 0, yMax = 0;
      foreach (var ch in text) {
         uint idx1 = GetGlyphIndex (ch);
         var metric = GetMetrics (idx1);
         int kern = GetKerning (idx0, idx1);
         int xChar = x + metric.LeftBearing + kern, yChar = y + metric.TopBearing;
         xMin = Math.Min (xMin, xChar); yMin = Math.Min (yMin, yChar - metric.Rows);
         xMax = Math.Max (xMax, xChar + metric.Columns); yMax = Math.Max (yMax, yChar);
         x += metric.Advance + kern;
         idx0 = idx1;
      }
      if (!exact) { yMax = mAscender; yMin = -mDescender; }
      if (xMax == 0) xMin = 0;
      return new (xMin, yMin, xMax, yMax);
   }

   /// <summary>Measures each character of the text and stores the potential cursor positions in xpos</summary>
   /// This is useful when we are rendering a text-box, and want to position the
   /// caret at a particular character in the text.
   public void GetCharsPos (string text, List<short> xpos, int xstart = 0) {
      xpos.Clear ();
      uint idx0 = 0;
      int x = xstart; xpos.Add ((short)x);
      foreach (var ch in text) {
         uint idx1 = GetGlyphIndex (ch);
         var metric = GetMetrics (idx1);
         int kern = GetKerning (idx0, idx1);
         x += metric.Advance + kern; xpos.Add ((short)x);
         idx0 = idx1;
      }
   }

   /// <summary>Sets the 'M' size in pixels</summary>
   public void SetEMSizeInPixels (int pixels) {
      Check (SetPixelSizes (mFace, 1000, 1000));
      var glyph = GetGlyph (GetGlyphIndex ('M'));
      uint size = (uint)(pixels * 1000.0 / glyph.Rows + 0.45);
      PixelSize = ((int)size, (int)size);
      double lie = -(double)mRec.Descender / (mRec.Ascender - mRec.Descender);
      mDescender = (int)(PixelSize.Y * lie + 0.5);
      mAscender = PixelSize.Y - mDescender;
   }
   public int Ascender => mAscender;
   public int Descender => mDescender;
   int mAscender, mDescender;

   /// <summary>Select a particular encoding (affects character to glyph conversions)</summary>
   internal void SetEncoding (FTEncoding encoding)
      => Check (FreeType.FreeType.SetEncoding (mFace, encoding));

   // Implementation -----------------------------------------------------------
   // Called whenever the font size is changed
   void Bump () {
      mNotes = null; mRawTexData = null;
      if (mTexture != 0) { GL.DeleteTexture (mTexture); mTexture = HTexture.Zero; }
      mUID = ++sNextUID;
   }

   // Helper used to build the GL texture (from the mRawTexData byte-buffer we construct
   // when we iterate through all the glyphs). 
   unsafe HTexture BuildTexture () {
      _ = Notes;  // This will also build the mRawTexData buffer
      GL.ActiveTexture (ETexUnit.Tex0);
      HTexture texture = GL.GenTexture ();
      GL.BindTexture (ETexTarget.TexRectangle, texture);
      GL.PixelStore (EPixelStoreParam.UnpackAlignment, 1);
      byte[] texData = mRawTexData!;
      GL.TexImage2D (ETexTarget.TexRectangle, EPixelInternalFormat.Red, CXTex, texData.Length / CXTex, EPixelFormat.Red, EPixelType.UByte, texData);
      mRawTexData = null;
      return texture;
   }
   const int CXTex = 8192;

   // Called to initialize the font with a specified pixel size
   void InitFont (int pixelSize) {
      mRec = Marshal.PtrToStructure<CFace> ((Ptr)mFace);
      SetEMSizeInPixels (pixelSize);
      Bump ();
   }

   public override string ToString ()
      => $"{mRec.FamilyName.ToUTF8 ()} {mRec.StyleName.ToUTF8 ()}";

   // Returns the Glyph structure for a particular glyph index
   internal Glyph GetGlyph (uint glyphIdx) {
      Check (LoadGlyph (mFace, glyphIdx, 0));
      Check (RenderGlyph (mRec.Glyph, 0));
      mGlyph.Update (mRec.Glyph);
      return mGlyph;
   }
   Glyph mGlyph = new ();

   /// <summary>Dictionary that maps characters to glyph indices</summary>
   IReadOnlyDictionary<char, uint> Map {
      get {
         if (mMap != null) return mMap;
         mMap = [];
         HashSet<uint> allGlyphs = [];
         foreach (var e in Encodings) {
            SetEncoding (e);
            uint charCode = GetFirstChar (mFace, out uint gindex);
            while (gindex != 0) {
               allGlyphs.Add (gindex);
               mMap[(char)charCode] = gindex;
               charCode = GetNextChar (mFace, charCode, out gindex);
            }
         }
         mAllGlyphs = [.. allGlyphs];
         return mMap;
      }
   }
   Dictionary<char, uint>? mMap;

   // This returns an array that contains the Metrics for every glyph in the
   // font. When this is read the first time, this enumerates through all the glyphs in the
   // font, and extracts the metrics (by first loading that Glyph into the TypeFace's single
   // glyph-slot and then fetching the metrics). It also rasterizes the font, and fetches the
   // pixels of the glyph. All these glphs are loaded into the huge byte-array (mRawTexData),
   // and along with each glyph's notes, we also store the TexOffset into this array. 
   // This big array can be turned into an OpenGL texture by simplying reading the HTexture
   // property (which constructs the OpenGL texture, and then discards this byte-array, which
   // is no longer needed). 
   unsafe Metrics[] Notes {
      get {
         if (mNotes != null) return mNotes;
         var glyphIndices = AllGlyphs;
         uint max = glyphIndices.Max ();
         mNotes ??= new Metrics[max + 1];
         byte[] texData = new byte[131072];
         int texOffset = 0;
         for (int i = 0; i < glyphIndices.Count; i++) {
            var index = glyphIndices[i];
            if (mNotes[index].TexOffset > 0) continue;
            var g = GetGlyph (index);
            mNotes[index] = new Metrics (g, texOffset);
            int cb = g.Rows * g.Columns;
            if (cb > 0) {
               while (texOffset + cb >= texData.Length)
                  Array.Resize (ref texData, texData.Length * 2);
               if (g.Columns == g.Stride) {
                  fixed (void* pDst = &texData[texOffset])
                     Buffer.MemoryCopy (g.Buffer.ToPointer (), pDst, cb, cb);
               } else
                  throw new NotImplementedException ();
               texOffset += cb;
            }
         }
         byte[] gamma = GammaMap;
         for (int i = 0; i < texOffset; i++) texData[i] = gamma[texData[i]];
         texOffset = CXTex * ((texOffset + CXTex - 1) / CXTex);
         Array.Resize (ref texData, texOffset);
         mRawTexData = texData;
         return mNotes;
      }
   }
   byte[]? mRawTexData;

   // A pre-computed gamma map for the current Gamma
   byte[] GammaMap {
      get {
         if (mGammaMap == null) {
            mGammaMap = new byte[256];
            double f = 1 / Gamma;
            for (int i = 0; i < 256; i++) {
               double y = Math.Pow (i / 255.0, f);
               GammaMap[i] = (byte)(y * 255 + 0.5);
            }
         }
         return mGammaMap;
      }
   }
   byte[]? mGammaMap;

   // Nested types -------------------------------------------------------------

   public readonly struct Metrics {
      internal Metrics (Glyph g, int texOffset) {
         Advance = (short)g.Advance;
         (Rows, Columns) = ((short)g.Rows, (short)g.Columns);
         (LeftBearing, TopBearing) = ((short)g.LeftBearing, (short)g.TopBearing);
         TexOffset = texOffset;
      }

      /// <summary>Rows and Columns of the bitmap</summary>
      public readonly short Rows, Columns;
      /// <summary>Left-bearing and Top-bearing (used to position the character) from baseline</summary>
      public readonly short LeftBearing, TopBearing;
      /// <summary>Advance width after rendering this character</summary>
      public readonly short Advance;
      /// <summary>Offset into the texture bitmap</summary>
      public readonly int TexOffset;
   }
   Metrics[]? mNotes;
}
#endregion
