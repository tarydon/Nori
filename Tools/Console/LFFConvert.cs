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
   /// <summary>Builds an LFONT file from an LFF font definition file</summary>
   /// This method reads an LFF file containing glyph definitions (points, arcs, reuse info),
   /// processes each character, scales its geometry, and writes a structured LFONT output.
   /// <param name="lffFile">Path to the source LFF font file.</param>
   /// <param name="lFontFile">Path to the destination LFONT file to be created.</param>
   public static void BuildLFont (string lffFile, string lFontFile) {
      // Read all lines from the LFF file
      var lines = File.ReadAllLines (lffFile);

      // Initialize LFONT header with name and version (1)
      List<string> output = [$"LFONT,{Path.GetFileNameWithoutExtension (lffFile)},1"];

      Dictionary<string, FontChar> charCache = []; // Cache of parsed characters
      ReadOnlySpan<char> codeHex = "", reuseKey = "";
      FontChar? fc = null;
      double maxY = 0, minY = 0, letterSpacing = 0, wordSpacing = 0, lineSpacingFactor = 0;
      const string LetterSpacingTkn = "# LetterSpacing:", WordSpacingTkn = "# WordSpacing:", LineSpacingFactorTkn = "# LineSpacingFactor:";

      // Process each line in the LFF file ...
      foreach (var rawLine in lines) {
         var line = rawLine.Trim ();
         switch (line.FirstOrDefault ()) {
            case '#': // Comment line
               switch (line) {
                  case string s when s.StartsWith (LetterSpacingTkn):
                     letterSpacing = double.Parse (s[LetterSpacingTkn.Length..].Trim ());
                     break;
                  case string s when s.StartsWith (WordSpacingTkn):
                     wordSpacing = double.Parse (s[WordSpacingTkn.Length..].Trim ());
                     break;
                  case string s when s.StartsWith (LineSpacingFactorTkn):
                     lineSpacingFactor = double.Parse (s[LineSpacingFactorTkn.Length..].Trim ());
                     break;
               }
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
               if (fc != null && fc.Strokes.Count != 0) {
                  fc.ReuseKey = charCache.TryGetValue (reuseKey.ToString ().ToLower (), out var reused) ? reused : null;
                  for (var reuse = fc.ReuseKey; reuse != null; reuse = reuse.ReuseKey) {
                     if (reuse.Strokes.Count > 0) fc.Strokes.AddRange (reuse.Strokes);
                     if (reuse.PenMoves.Count > 0) fc.PenMoves.AddRange (reuse.PenMoves);
                  }
                  charCache[codeHex.ToString ()] = fc;
               }

               // Reset state for next glyph
               fc = null;
               maxY = double.MinValue; minY = double.MaxValue;
               reuseKey = "";
               break;

            default:
               // Glyph polyline definition
               if (fc is null) break;
               var sb = new StringBuilder ();
               var segs = line.Split (';');
               if (segs.Length < 2) continue;

               List<Point2> pts = [ExtractPoint (segs[0])];
               var prev = pts[0];
               sb.Append (" M" + $"{prev.X},{prev.Y}");
               for (int i = 1; i < segs.Length; i++) {
                  var s = segs[i];
                  if (s.Contains ('A')) {  // Parse arc segment and generate intermediate points
                     var (arcEnd, bulge) = ExtractArc (s);
                     sb.Append (" Q" + $"{arcEnd.X},{arcEnd.Y},{((8 * Math.Atan (bulge)) / Lib.PI).R6 ()}");
                     pts.Add (prev); pts.Add (arcEnd);
                     prev = arcEnd;
                  } else { // Parse a straight line segment
                     var pt = ExtractPoint (s);
                     sb.Append (GetCommand (prev, pt));
                     pts.Add (pt);
                     prev = pt;
                  }
               }
               fc.PenMoves.Add (sb.ToString ()); // Store the command string for this glyph
               var trace = Poly.Lines (pts);
               var box = trace.GetBound ();
               fc.Strokes.Add (trace);
               maxY = Math.Max (maxY, box.Y.Max);
               minY = Math.Min (minY, box.Y.Min);
               // Use 'M' (0x004D) height as ascender if tallest
               if (codeHex.ToString ().EqIC ("004d") && maxY > mAscender) mAscender = maxY;
               if (minY < mDescender) mDescender = minY;
               break;
         }
      }

      // Add a default space glyph (code 32)
      charCache.Add ("0020", new FontChar ("0020"));
      // Write font header (character count, ascender, descender, vAdvance)
      output.Add ($"{charCache.Count},{mAscender.R6 ()},{mDescender.R6 ()},{(mAscender - mDescender).R6 () * lineSpacingFactor:R}");

      // Build LFONT glyph output
      foreach (var val in charCache.Values) {
         // Use wordSpacing as fallback width if glyph width is zero
         double w = val.CharCode == 32 ? wordSpacing : val.Width,
         // Normalize horizontal advance based on ascender height
         hAdvance = ((w + letterSpacing) / mAscender).R6 ();
         output.Add ($"{val.CharCode},{hAdvance},{val.Strokes.Count},{val.Symbol}");
         // Output each stroke (polyline) as a series of commands
         foreach (var s in val.PenMoves) output.Add (s);
      }

      // Write final LFONT file
      File.WriteAllLines (lFontFile, output);
   }
   static double mAscender,   // Highest Y value of 'M' character (top of font)
                 mDescender;  // Lowest Y value in all characters (bottom of font)

   // Parses a 2D point from a string in the format "X,Y" with coordinate rounding.
   static Point2 ExtractPoint (string s) {
      var parts = s.Split (',');
      return new Point2 (double.Parse (parts[0]).R6 (), double.Parse (parts[1]).R6 ());
   }

   // Parses a 2D point and a bulge value from a string in the format "X,Y,A{bulge}".
   // Assumes the string always has three parts, and the third part starts with 'A' followed by the bulge value.
   static (Point2 Point, double Bulge) ExtractArc (string s) => (ExtractPoint (s), double.Parse (s.Split (',')[2][1..]));

   // Returns a compact drawing command string based on the relative position of two points
   static string GetCommand (Point2 a, Point2 b) =>
     (a.X.EQ (b.X), a.Y.EQ (b.Y)) switch {
        (true, _) => $" V{b.Y}",  // Vertical line: same X
        (_, true) => $" H{b.X}",  // Horizontal line: same Y
        _ => $" L{b.X},{b.Y}"     // General line
     };

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
