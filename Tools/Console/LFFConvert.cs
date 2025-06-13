// ────── ╔╗                                                                                    CON
// ╔═╦╦═╦╦╬╣ LFFConvert.cs
// ║║║║╬║╔╣║ Converts LFF font to LFONT format, preserving lines and arcs for vector text rendering.
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
namespace Nori.Con;

#region class LFF2LFontConverter -------------------------------------------------------------------
/// <summary>Converts LFF font files to the custom LFONT format</summary>
// This utility parses character definitions from an LFF file
// and outputs an LFONT-compliant file containing character codes,
// names, and raw polyline and arc path data.
public class LFF2LFontConverter {
   public LFF2LFontConverter (string lff, string lfont) {
      mOutputFile = lfont;
      mFontName = Path.GetFileNameWithoutExtension (lff);
      mLffLines = File.ReadAllLines (lff);
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Builds an LFONT file from an LFF font definition file</summary>
   /// This method reads an LFF file containing glyph definitions (points, arcs, reuse info),
   /// processes each character, scales its geometry, and writes a structured LFONT output.
   /// <param name="lffFile">Path to the source LFF font file.</param>
   /// <param name="lFontFile">Path to the destination LFONT file to be created.</param>
   public void BuildLFont () {
      ReadOnlySpan<char> codeHex = "", reuseKey = "";
      FontChar? fc = null;

      // Process each line in the LFF file ...
      foreach (var rawLine in mLffLines) {
         var line = rawLine.Trim ();
         switch (line.FirstOrDefault ()) {
            case '#': // Comment line
               SetSpacingParams (line);
               TrySetFontName(line);
               break;

            case '[':
               // Start of new glyph
               codeHex = line.AsSpan ()[1..5];  // Hex code (e.g., 0041 for 'A')
               fc = new FontChar (codeHex.ToString ());
               break;

            case 'C':
               // Extract reuse key from the line, skipping the leading 'C' character.
               // Example: If the line is "C0041", the reuse key is "0041"
               reuseKey = line.AsSpan ()[1..];
               break;

            case '\0':
               // End of glyph block
               SetGlyphParams (codeHex.ToString (), reuseKey.ToString (), fc);
               // Reset state for next glyph
               fc = null;
               reuseKey = "";
               break;

            default:
               // Glyph polyline definition
               if (fc is null) break;
               ParseGlyphStroke (line, fc);
               break;
         }
      }

      // Add a default space glyph (code 32)
      mCharCache.Add ("0020", new FontChar ("0020"));
      ShipLFontFile (mOutputFile);
   }

   // Implementation -----------------------------------------------------------
   // Parses font spacing metadata from comments
   void SetSpacingParams (string line) {
      if (line.StartsWith ("# LetterSpacing:"))
         mLetterSpacing = double.Parse (line[16..].Trim ());      // "# LetterSpacing:".Length == 16
      else if (line.StartsWith ("# WordSpacing:"))
         mWordSpacing = double.Parse (line[14..].Trim ());        // "# WordSpacing:".Length == 14
      else if (line.StartsWith ("# LineSpacingFactor:"))
         mLineSpacingFactor = double.Parse (line[21..].Trim ());  // "# LineSpacingFactor:".Length == 21
   }

   // Checks if the line contains a valid # Name: header and sets the font name.
   void TrySetFontName (string line) {
      if (line.StartsWith ("# Name:")) {
         var name = line[7..].Trim (); // "# Name:".Length == 7
         if (!name.IsBlank ())
            mFontName = name;
      }
   }

   // Parses a glyph stroke definition and adds the corresponding drawing instructions to a FontChar.
   // The input is a semicolon-separated string of segments:(e.g., "0,0;10,0;A10,10,0.414")
   // A segment like "x,y" represents a line to that point.
   // A segment like "AendX,endY,bulge" represents an arc to (endX, endY) with the given bulge value.
   // This method builds a vector path string (e.g., "Mx,y Lx,y Qx,y,q") and parses it into a Poly object
   // that is stored in the FontChar for rendering and bounds calculation.
   void ParseGlyphStroke (string line, FontChar fc) {
      StringBuilder sb = new ();
      var segs = line.Split (';');
      var prev = ExtractPoint (segs[0]); // Starting point of the glyph stroke
      sb.Append (" M" + $"{prev.X},{prev.Y}");
      for (int i = 1; i < segs.Length; i++) {
         var s = segs[i];
         // Arc segment: starts with 'A'
         // Convert arc segment to quadratic arc command (Qx,y,q : where q is the number of quarter-turns in the arc)
         if (s.Contains ('A')) {
            var (arcEnd, bulge) = ExtractArc (s);
            sb.Append (" Q" + $"{arcEnd.X},{arcEnd.Y},{((8 * Math.Atan (bulge)) / Lib.PI).R6 ()}");
            prev = arcEnd;
         } else {
            // Regular line segment
            var pt = ExtractPoint (s);
            sb.Append (GetCommand (prev, pt));
            prev = pt;
         }
      }

      // Finalize the path string and add it to FontChar
      var path = sb.ToString ();
      fc.PenMoves.Add (path);
      var trace = Poly.Parse (path);
      fc.Strokes.Add (trace);

      // Update global ascender/descender metrics based on this character
      var box = trace.GetBound ();
      if (fc.CharCode == 77 && box.Y.Max > mAscender)  // 'M' used as reference for ascender
         mAscender = box.Y.Max;
      mDescender = Math.Min (mDescender, box.Y.Min);
   }

   // Finalizes a FontChar glyph by optionally merging strokes and pen moves from a reused glyph,
   // then caches the resulting FontChar by its hexadecimal code key.
   void SetGlyphParams (string codeHex, string reuseKey, FontChar? fc) {
      if (fc != null && fc.Strokes.Count != 0) {
         if (mCharCache.TryGetValue (reuseKey.ToLower (), out var reused)) {
            fc.ReuseKey = reused;
            for (var r = reused; r != null; r = r.ReuseKey) {
               fc.Strokes.AddRange (r.Strokes);
               fc.PenMoves.AddRange (r.PenMoves);
            }
         }
         mCharCache[codeHex] = fc;
      }
   }

   // Exports the cached font characters and metrics into an LFONT format file.
   // The method writes the LFONT header, font metrics, and glyph definitions
   // including character codes, horizontal advances, stroke counts, symbols,
   // and pen move commands for each glyph.
   void ShipLFontFile (string lFontFile) {
      var sb = new StringBuilder ();

      // Header: LFONT name and version
      sb.AppendLine ($"LFONT,{mFontName},1");

      // Font metrics: character count, ascender, descender, vertical advance
      sb.AppendLine ($"{mCharCache.Count},{mAscender.R6 ()},{mDescender.R6 ()},{(mAscender - mDescender).R6 () * mLineSpacingFactor:R}");

      foreach (var val in mCharCache.Values) {
         // Use wordSpacing as fallback width if glyph width is zero
         double w = val.CharCode == 32 ? mWordSpacing : val.Width,
         // Normalize horizontal advance based on ascender height
         hAdvance = ((w + mLetterSpacing) / mAscender).R6 ();
         sb.AppendLine ($"{val.CharCode},{hAdvance},{val.Strokes.Count},{val.Symbol}");
         foreach (var stroke in val.PenMoves) sb.AppendLine (stroke);
      }
      File.WriteAllText (lFontFile, sb.ToString ());
   }

   // Parses a 2D point from a string in the format "X,Y" with coordinate rounding.
   static Point2 ExtractPoint (string s) {
      var parts = s.Split (',');
      return new Point2 (double.Parse (parts[0]).R6 (), double.Parse (parts[1]).R6 ());
   }

   // Parses a 2D point and a bulge value from a string in the format "X,Y,A{bulge}".
   // Assumes the string always has three parts, and the third part starts with 'A' followed by the bulge value.
   static (Point2 Point, double Bulge) ExtractArc (string s) => (ExtractPoint (s), double.Parse (s.Split (',')[2][1..]));

   // Returns a compact drawing command string based on the relative position of two points
   static string GetCommand (Point2 a, Point2 b) {
      if (a.X.EQ (b.X)) return $" V{b.Y}";  // Vertical line: same X
      if (a.Y.EQ (b.Y)) return $" H{b.X}";  // Horizontal line: same Y
      return $" L{b.X},{b.Y}";              // General line
   }

   // Private fields ------------------------------------------------
   Dictionary<string, FontChar> mCharCache = []; // Cache of parsed characters
   double mAscender = double.MinValue,   // Highest Y value of 'M' character (top of font)
          mDescender = double.MaxValue,  // Lowest Y value in all characters (bottom of font)
          mLetterSpacing = 0, mWordSpacing = 0, mLineSpacingFactor = 1;
   string mFontName = "",
          mOutputFile; // Output LFONT file paths
   string[] mLffLines; // All raw lines read from the LFF font file

   // Nested types -------------------------------------------------------------
   // Represents a single character definition in a font, including geometry, code, and optional reuse data
   class FontChar {
      /// <summary>Constructs a FontChar from a hex code string (e.g., "0041" for 'A')</summary>
      public FontChar (string hexCode) {
         if (!int.TryParse (hexCode, System.Globalization.NumberStyles.HexNumber, null, out int code))
            throw new ArgumentException ($"Invalid hex character code: {hexCode}", nameof (hexCode));
         CharCode = code;
         Symbol = char.ConvertFromUtf32 (CharCode);
      }

      // Character representation (e.g., "A", "-", " "). Falls back to CharCode if unset.
      public readonly string Symbol;

      // Unicode character code (e.g., 65 for 'A')
      public readonly int CharCode;

      // An optional reference to another FontChar whose points should be reused
      public FontChar? ReuseKey { get; set; }

      // Strokes (lines/arcs) defining this character
      public List<Poly> Strokes { get; set; } = [];

      // Width of the glyph
      public double Width => Strokes.Count == 0 ? 0 : Strokes.Max (a => a.GetBound ().X.Max);

      // List of vector drawing commands used to render the character
      public List<string> PenMoves { get; set; } = [];
   }
}
#endregion
