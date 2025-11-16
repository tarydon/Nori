// ────── ╔╗
// ╔═╦╦═╦╦╬╣ FreeType.cs
// ║║║║╬║╔╣║ Defines the FreeType class, with P-Invokes into the freetype.dll library
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.FreeType;
using static System.Runtime.InteropServices.CallingConvention;
using Ptr = nint;

#region class FreeType -----------------------------------------------------------------------------
/// <summary>Class that encapsulates the FreeType library</summary>
static class FreeType {
   // Constants ----------------------------------------------------------------
   const string DLL = "freetype.dll";

   public enum Error {
      Ok = 0x00,
      CannotOpenResource = 0x01,          // Cannot open resource
      UnknownFileFormat = 0x02,           // Unknown file format
      InvalidFileFormat = 0x03,           // Broken file
      InvalidVersion = 0x04,              // Incorrect FreeType version
      LowerModuleVersion = 0x05,          // Module version too low
      InvalidArgument = 0x06,             // Invalid argument
      UnimplementedFeature = 0x07,        // Unimplemented feature
      InvalidTable = 0x08,                // Broken table
      InvalidOffset = 0x09,               // Broken offset within table
      ArrayTooLarge = 0x0A,               // Array size exceeded
      InvalidGlyphIndex = 0x10,           // Invalid glyph index
      InvalidCharacterCode = 0x11,        // Invalid character code
      InvalidGlyphFormat = 0x12,          // Unsupported glyph format
      CannotRenderGlyph = 0x13,           // Cannot render this glyph format
      InvalidOutline = 0x14,              // Invalid font outline
      InvalidComposite = 0x15,            // Invalid composite glyph
      TooManyHints = 0x16,                // Too many hints
      InvalidPixelSize = 0x17,            // Invalid pixel size
      InvalidHandle = 0x20,               // Invalid object handle
      InvalidLibraryHandle = 0x21,        // Invalid library handle
      InvalidDriverHandle = 0x22,         // Invalid driver handle
      InvalidFaceHandle = 0x23,           // Invalid face handle
      InvalidSizeHandle = 0x24,           // Invalid size handle
      InvalidSlotHandle = 0x25,           // Invalid glyph slot handle
      InvalidCharMapHandle = 0x26,        // Invalid charmap handle
      InvalidCacheHandle = 0x27,          // Invalid cache manager handle
      InvalidStreamHandle = 0x28,         // Invalid stream handle
      TooManyDrivers = 0x30,              // Too many modules
      TooManyExtensions = 0x31,           // Too many extensions
      OutOfMemory = 0x40,                 // Out of memory
      UnlistedObject = 0x41,              // Unlisted object
      CannotOpenStream = 0x51,            // Cannot open stream
      InvalidStreamSeek = 0x52,           // Invalid stream seek
      InvalidStreamSkip = 0x53,           // Invalid stream skip
      InvalidStreamRead = 0x54,           // Invalid stream read
      InvalidStreamOperation = 0x55,      // Invalid stream operation
      InvalidFrameOperation = 0x56,       // Invalid frame operation
      NestedFrameAccess = 0x57,           // Nested frame access
      InvalidFrameRead = 0x58,            // Invalid frame read
      RasterUninitialized = 0x60,         // Raster uninitialized
      RasterCorrupted = 0x61,             // Raster corrupted
      RasterOverflow = 0x62,              // Raster overflow
      RasterNegativeHeight = 0x63,        // Negative height while rastering
      TooManyCaches = 0x70,               // Too many registered caches
      InvalidOpCode = 0x80,               // Invalid opcode
      TooFewArguments = 0x81,             // Too few arguments
      StackOverflow = 0x82,               // Stack overflow
      CodeOverflow = 0x83,                // Code overflow
      BadArgument = 0x84,                 // Bad argument
      DivideByZero = 0x85,                // Division by zero
      InvalidReference = 0x86,            // Invalid reference
      DebugOpCode = 0x87,                 // Found debug opcode
      EndfInExecStream = 0x88,            // Found ENDF opcode in execution stream
      NestedDefs = 0x89,                  // Nested DEFS
      InvalidCodeRange = 0x8A,            // Invalid code range
      ExecutionTooLong = 0x8B,            // Execution context too long
      TooManyFunctionDefs = 0x8C,         // Too many function definitions
      TooManyInstructionDefs = 0x8D,      // Too many instruction definitions
      TableMissing = 0x8E,                // SFNT font table missing
      HorizHeaderMissing = 0x8F,          // Horizontal header (hhea) table missing
      LocationsMissing = 0x90,            // Locations (loca) table missing
      NameTableMissing = 0x91,            // Name table missing
      CMapTableMissing = 0x92,            // Character map (cmap) table missing
      HmtxTableMissing = 0x93,            // Horizontal metrics (hmtx) table missing
      PostTableMissing = 0x94,            // PostScript (post) table missing
      InvalidHorizMetrics = 0x95,         // Invalid horizontal metrics
      InvalidCharMapFormat = 0x96,        // Invalid character map (cmap) format
      InvalidPPem = 0x97,                 // Invalid ppem value
      InvalidVertMetrics = 0x98,          // Invalid vertical metrics
      CouldNotFindContext = 0x99,         // Could not find context
      InvalidPostTableFormat = 0x9A,      // Invalid PostScript (post) table format      
      InvalidPostTable = 0x9B,            // Invalid PostScript (post) table
      SyntaxError = 0xA0,                 // Opcode syntax error
      StackUnderflow = 0xA1,              // Argument stack underflow
      Ignore = 0xA2,                      // Ignore this error
      NoUnicodeGlyphName = 0xA3,          // No Unicode glyph name found
      MissingStartfontField = 0xB0,       // `STARTFONT' field missing
      MissingFontField = 0xB1,            // `FONT' field missing
      MissingSizeField = 0xB2,            // `SIZE' field missing
      MissingFontboudingboxField = 0xB3,  // `FONTBOUNDINGBOX' field missing
      MissingCharsField = 0xB4,           // `CHARS' field missing
      MissingStartcharField = 0xB5,       // `STARTCHAR' field missing
      MissingEncodingField = 0xB6,        // `ENCODING' field missing
      MissingBbxField = 0xB7,             // `BBX' field missing
      BbxTooBig = 0xB8,                   // `BBX' too big
      CorruptedFontHeader = 0xB9,         // Font header corrupted or missing fields     
      CorruptedFontGlyphs = 0xBA          // Font glyphs corrupted or missing fields
   }

   // Methods ------------------------------------------------------------------
   [DllImport (DLL, EntryPoint = "FT_Get_Char_Index", CallingConvention = Cdecl)]
   internal static extern uint GetCharIndex (HFace face, uint charcode);

   [DllImport (DLL, EntryPoint = "FT_Get_First_Char", CallingConvention = Cdecl)]
   internal static extern uint GetFirstChar (HFace face, out uint agindex);

   [DllImport (DLL, EntryPoint = "FT_Get_Kerning", CallingConvention = Cdecl)]
   internal static extern Error GetCharKerning (HFace face, uint left_glyph, uint right_glyph, uint kern_mode, out Vector26_6 akerning);

   [DllImport (DLL, EntryPoint = "FT_Get_Next_Char", CallingConvention = Cdecl)]
   internal static extern uint GetNextChar (HFace face, uint char_code, out uint agindex);

   [DllImport (DLL, EntryPoint = "FT_Init_FreeType", CallingConvention = Cdecl)]
   internal static extern Error Init (out HLibrary library);

   [DllImport (DLL, EntryPoint = "FT_Load_Glyph", CallingConvention = Cdecl)]
   internal static extern Error LoadGlyph (HFace face, uint glyph_index, int load_flags);

   [DllImport (DLL, EntryPoint = "FT_New_Face", CallingConvention = Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
   internal static extern Error NewFace (HLibrary library, string filepathname, int face_index, out HFace aface);

   [DllImport (DLL, EntryPoint = "FT_New_Memory_Face", CallingConvention = Cdecl)]
   internal static extern Error NewFace (HLibrary library, Ptr file_base, int file_size, int face_index, out HFace aface);

   [DllImport (DLL, EntryPoint = "FT_Render_Glyph", CallingConvention = Cdecl)]
   internal static extern Error RenderGlyph (IntPtr slot, int render_mode);

   [DllImport (DLL, EntryPoint = "FT_Set_Pixel_Sizes", CallingConvention = Cdecl)]
   internal static extern Error SetPixelSizes (HFace face, uint width, uint height);

   [DllImport (DLL, EntryPoint = "FT_Select_Charmap", CallingConvention = Cdecl)]
   internal static extern Error SetEncoding (HFace face, FTEncoding encoding);

   public static void Check (Error error) {
      if (error != Error.Ok) throw new Exception ($"FreeType error: {error}");
   }
}
#endregion

#region C structs used by FreeType -----------------------------------------------------------------
// Represents a bounding-box
[StructLayout (LayoutKind.Sequential)]
readonly struct BBox {
   public readonly int XMin, YMin;
   public readonly int XMax, YMax;
}

// Describes a bitmap (output from a rasterization call)
[StructLayout (LayoutKind.Sequential)]
struct Bitmap {
   public int Rows, Columns;           // Number of rows, columns
   public int Stride;                  // To go from one bitmap row to the next one
   public Ptr Buffer;                  // Pointer to bitmap data         
   public short CGrays;                // Number of gray-levels
   byte PixelMode, PaletteMode;
   Ptr palette;
}

// Represents a char-map (maps character codes in some encoding to glyph indices)
[StructLayout (LayoutKind.Sequential)]
readonly struct CharMap {
   readonly Ptr Face;
   public readonly FTEncoding Encoding;
   readonly ushort PlatformId;
   readonly ushort EncodingId;
}

// Encapsulates an FT_FaceRec record
[StructLayout (LayoutKind.Sequential)]
struct CFace {
   int CFaces, FaceIndex;              // Used only for multiple faces in the same font file
   int FaceFlags, StyleFlags;
   public int CGlyphs;                 // Number of glyphs in the face      
   public Ptr FamilyName;              // Font family name
   public Ptr StyleName;               // Font style name
   int CFixedSizes;                    // Number of fixed sizes
   Ptr AvailableSizes;                 // Array of FT_Bitmap_Size for each of the CFixedSizes
   public int CCharMaps;               // Number of char-maps
   public Ptr Charmaps;                // Pointer to those charmaps
   Generic generic;
   public BBox BBox;                   // Font bounding box 
   public ushort UnitsPerEM;           // Font units per EM square for this font
   public short Ascender;              // Typographic ascender, in font units
   public short Descender;             // Typographic descender, in font units (negative value)
   public short Height;                // Vertical distance between two consecutive baselines
   short MaxAdvanceWidth, MaxAdvanceHeight;
   public short UnderlinePosition;     // Position of the center of the underlining stem
   public short UnderlineThickness;    // Thickness of the underlining 
   public Ptr Glyph;                   // Pointer to the current glyph slot
   public Ptr Size;                    // Current active size for this font
   public Ptr Charmap;                 // Current active charmap for this font
   Ptr Driver, Memory, Stream;
   Ptr SizesList;
   Generic AutoHint;
   Ptr Extensions, Public;
   public static int SizeInBytes => Marshal.SizeOf (typeof (CFace));
}

// Used internally as a pointer to a generic client-specified 'finalizer'
[StructLayout (LayoutKind.Sequential)]
struct Generic { Ptr Data, Finalizer; }

// Metrics for a glyph
[StructLayout (LayoutKind.Sequential)]
struct GlyphMetrics {
   public Fix26_6 Width, Height;                    // Width, height of the glyph
   public Fix26_6 HBearingX, HBearingY, HAdvance;   // Bearing and Advance for horizontal layout
   public Fix26_6 VBearingX, VBearingY, VAdvance;   // Bearing and advance for vertical layout
}

// Basic data structure that captures a complete Glyph
[StructLayout (LayoutKind.Sequential)]
struct CGlyphSlot {
   Ptr Library, Face, Next;
   public uint GlyphIndex;       // Glyph index passed in during load
   Generic generic;
   public GlyphMetrics Metrics; // Metrics of last loaded glyph
   int RawHAdvance, RawVAdvance;
   Vector26_6 Advance;
   uint Format;
   public Bitmap Bitmap;
   public int BmpLeft;           // Bitmap's left bearing, expressed in integer pixels
   public int BmpTop;            // Bitmap's top bearing, expressed in integer pixels
   Outline Outline;
   int CSubGlyphs;               // Number of sub-glyphs         
   Ptr SubGlyphs;                // Pointer to those sub-glyphs
   Ptr ControlData;
   int ControlLen;
   int LSBDelta;                 // Difference between hinted and unhinted LeftSideBearing when auto-hinting is used
   int RSBDelta;                 // Same for RightSideBearing
   Ptr Other, Internal;
}

// Represents a 'fixed-point' number (26 bits for integer part, 6 bits for fraction)
[StructLayout (LayoutKind.Sequential)]
readonly struct Fix26_6 {
   public Fix26_6 (double f) => Raw = (int)(f * 64 + 0.5);
   public readonly double Value => Raw / 64.0;
   public override string ToString () => $"[{Value}]";
   readonly int Raw;
}

// Represents a set of polylines making up a glyph
[StructLayout (LayoutKind.Sequential)]
struct Outline {
   short NContours, NPoints;
   Ptr Points, Tags, Contours;
   int Flags;
}

// A size record
[StructLayout (LayoutKind.Sequential)]
struct SizeRec {
   Ptr Face;
   Generic Generic;
   ushort XPPem, YPPem;
   Fix26_6 XScale, YScale;
   public Fix26_6 Ascender, Descender, Height, Advance;
}

// A 2D vector whose X,Y components are both Fix26_6
[StructLayout (LayoutKind.Sequential)]
readonly struct Vector26_6 {
   public readonly Fix26_6 X, Y;
}
#endregion

#region class Glyph --------------------------------------------------------------------------------
/// <summary>Represents a single glyph from a freetype font</summary>
class Glyph {
   internal void Update (Ptr ptrGlyphSlot) {
      CGlyphSlot data = Marshal.PtrToStructure<CGlyphSlot> (ptrGlyphSlot);
      Bitmap bmp = data.Bitmap;
      GlyphMetrics metrics = data.Metrics;
      Buffer = bmp.Buffer; Rows = bmp.Rows; Columns = bmp.Columns; Stride = bmp.Stride;
      LeftBearing = data.BmpLeft; TopBearing = data.BmpTop;
      Advance = (int)metrics.HAdvance.Value;
   }

   public Ptr Buffer;         // Pointer to the bitmap data
   public int Rows;           // Number of rows in the bitmap
   public int Columns;        // Number of columns in the bitmap
   public int Stride;         // Delta (in bytes) to go to the next row of the bitmap
   public int LeftBearing;    // Left bearing, in pixels
   public int TopBearing;     // Top bearing, in pixels
   public int Advance;
}
#endregion

#region Enumerations -------------------------------------------------------------------------------
// Handle to the entire freetype library 
enum HLibrary : ulong { Zero };
// Handle for a typeface loaded through the library
enum HFace : ulong { Zero };

// The various encodings possible for a CharMap
enum FTEncoding : uint {
   None = 0,
   MicrosoftSymbol = ('s' << 24 | 'y' << 16 | 'm' << 8 | 'b'),
   Unicode = ('u' << 24 | 'n' << 16 | 'i' << 8 | 'c'),
   Sjis = ('s' << 24 | 'j' << 16 | 'i' << 8 | 's'),
   GB2312 = ('g' << 24 | 'b' << 16 | ' ' << 8 | ' '),
   Big5 = ('b' << 24 | 'i' << 16 | 'g' << 8 | '5'),
   Wansung = ('w' << 24 | 'a' << 16 | 'n' << 8 | 's'),
   Johab = ('j' << 24 | 'o' << 16 | 'h' << 8 | 'a'),
   AdobeStandard = ('A' << 24 | 'D' << 16 | 'O' << 8 | 'B'),
   AdobeExpert = ('A' << 24 | 'D' << 16 | 'B' << 8 | 'E'),
   AdobeCustom = ('A' << 24 | 'D' << 16 | 'B' << 8 | 'C'),
   AdobeLatin1 = ('l' << 24 | 'a' << 16 | 't' << 8 | '1'),
   AppleRoman = ('a' << 24 | 'r' << 16 | 'm' << 8 | 'n'),
}
#endregion
