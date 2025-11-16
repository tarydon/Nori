// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ LFont.cs
// ║║║║╬║╔╣║ Contains the LineFont class needed to render vector fonts in a drawing.
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Frozen;
namespace Nori;

#region class LineFont -----------------------------------------------------------------------------
/// <summary>Represents fonts that define each character glyph as a set of lines and arcs.</summary>
public class LineFont {
   // A private constructor used to instantiate the LineFont object.
   private LineFont (string name, int nchars, double asc, double desc, double adv, FrozenDictionary<int, Glyph> glyphs) =>
      (Name, NChars, Ascender, Descender, VAdvance, Glyphs) = (name, nchars, asc, desc, adv, glyphs);

   /// <summary>The name of the font</summary>
   public readonly string Name;
   /// <summary>The number of character glyphs found in this font file</summary>
   public readonly int NChars;
   /// <summary>The height of the 'M' character (typically called the em-box) in the font, above 
   /// the baseline.In effect, you can consider this as the height of the font.</summary>
   public readonly double Ascender;
   /// <summary>Lowest position (relative to the baseline) of the bounding box bottoms of all the characters.</summary>
   /// For example, characters like g, y, descend below the baseline, and this value 
   /// is the maximum value of such descent (this is a negative value)
   public readonly double Descender;
   /// <summary>The recommended vertical spacing between consecutive lines of text</summary>
   public readonly double VAdvance;

   /// <summary>Obtains a font by name. Returns the default 'simplex' font if given font is missing.</summary>
   /// The font name is case insensitive. Font data is loaded on-demand from the static wad 
   /// resources. Each font, once loaded, are cached and never loaded again in that run. 
   /// <remarks>If the specified name does not exist, it returns the default **simplex** for now. Thus, this 
   /// routine always returns at least a fallback font.</remarks>
   /// <param name="name">The font name.</param>
   public static LineFont Get (string name) {
      if (name.IsBlank ()) name = "simplex";
      string lname = name.ToLower ();
      // Try to get font from the cache.
      var font = mFonts.SafeGet (lname);
      if (font != null) return font;
      string[]? lines = null;
      // Load font from wad and return default if the font file is missing.
      // As font resources are one of the internal data files, rigorous error handling
      // has been ommitted during the load in favor of simpler code.
      try { lines = Lib.ReadLines ($"nori:DXF/{lname}.lfont"); } catch { }
      if (lines == null || lines.Length == 0) {
         var lfont = Get ("simplex");
         mFonts[lname] = lfont;
         return lfont;
      }
      int n = 0; string[] w = Next (3); name = w[1];
      Dictionary<int, Glyph> glyphs = [];
      w = Next ();
      var (nchars, asc, desc, vadv) = (w[0].ToInt (), w[1].ToDouble (), w[2].ToDouble (), w[3].ToDouble ());
      List<Poly> polys = []; PolyBuilder builder = new ();
      for (int i = 0; i < nchars; i++) {
         polys.Clear (); w = Next ();
         var (code, adv, npoly, _) = (w[0].ToInt (), w[1].ToDouble (), w[2].ToInt (), w[3][0]);
         for (int j = 0; j < npoly; j++) polys.Add (builder.Build (Line ()));
         glyphs[code] = new (adv * asc, [.. polys]);
      }
      font = new LineFont (name, nchars, asc, desc, vadv, glyphs.ToFrozenDictionary ());
      return mFonts[font.Name.ToLower ()] = font;

      string Line () => lines[n++]; // Fetch next line
      string[] Next (int ntok = 4) => Line ().Split (',', ntok); // Fetch tokens from next line
   }
   // The LFONT database.
   static readonly Dictionary<string, LineFont> mFonts = [];

   /// <summary>Render text using this font into a a set of polylines</summary>
   /// <param name="text">The text to render (multi-line text supported, separate lines with \n characters)</param>
   /// <param name="pos">Reference point of the text</param>
   /// <param name="align">Specifies which corner of the text bounding box the 'pos' is aligned to</param>
   /// <param name="oblique">The text obliquing angle (in radians), use 0 for non-italic text</param>
   /// <param name="xstretch">How much is the text 'stretched' in X direction (1 = normal, less than 1 for condensed etc)</param>
   /// <param name="height">Text height (height of a capital M, typically)</param>
   /// <param name="angle">Rotation angle of the baseline, in radians</param>
   /// <param name="output">The List(Poly) the text is output into</param>
   public void Render (string text, Point2 pos, ETextAlign align, double oblique, double xstretch, double height, double angle, List<Poly> output) {
      string[] lines = [.. text.Split ('\n')];                 // Split the text into lines, 
      List<double> widths = [.. lines.Select (GetWidth)];      // and get their widths (assuming height=1)

      // Compute the scale factors in X and Y (from font data to final output), 
      // and then the unit vectors to move 'across' a line, and 'down' between lines
      int cLines = lines.Length;
      double scaleY = height / Ascender, scaleX = xstretch * scaleY;
      // Compute the 'across' vector that moves along the text baseline, and the
      // 'down' vector that moves us to the next line (these depend on the rotation angle,
      // text height, and the xstretch factor). 
      var rotation = Matrix2.Rotation (angle);
      Vector2 across = new Vector2 (scaleX, 0) * rotation, down = new Vector2 (0, -scaleY) * rotation;
      double y0 = (((int)align - 1) / 3) switch {
         0 => -Ascender,                              // Top alignment
         2 => -Descender + VAdvance * (cLines - 1),   // Bottom alignment
         1 => (VAdvance * (cLines - 1) - Ascender) / 2,  // Center
         _ => VAdvance * (cLines - 1) // Baseline alignment
      };

      // posChar is first set to the start point (baseline) of the first character 
      // on the first line
      Point2 posChar = pos + new Vector2 (0, y0 * scaleY) * rotation;
      // xfm is a transform that handles the rotation, scaling, x-stretch. In addition to this
      // we need a translation by posChar to position individual characters, but that changes with
      // each charcter so we compute that translation as posChar gets updated
      var xfm = Matrix2.Scaling (scaleX, scaleY) * Matrix2.Rotation (angle);
      if (!oblique.IsZero ()) xfm = new Matrix2 (1, 0, Math.Tan (oblique), 1, 0, 0) * xfm;

      for (int i = 0; i < lines.Length; i++) {
         // At the start of each line, 'park' the position at the start of the line
         var (line, posPark) = (lines[i], posChar);
         // xshift is based on the horizontal alignment, since posPark was computed assuming a 
         // left alignment, we have to adjust it if we are using centered or right alignment
         double xshift = (((int)align - 1) % 3) switch { 1 => -widths[i] / 2, 2 => -widths[i], _ => 0 };
         posChar += across * xshift;
         foreach (var ch in line) {
            // As we output each character, adjust posChar by the horizontal advance of this character
            if (Glyphs.TryGetValue (ch, out var g)) {
               output.AddRange (g.Polys.Select (a => a * xfm * Matrix2.Translation (posChar.X, posChar.Y)));
               posChar += across * g.HAdvance;
            }
         }
         // Line is over, go back to the start of the line (posPark) and then go down to the next line
         posChar = posPark + down * VAdvance;
      }

      // Helpers ...........................................
      // Gets the width of a line of text, when rendered at size = 1, xscale = 1
      double GetWidth (string line) {
         double x = 0;
         for (int i = 0; i < line.Length; i++) {
            if (!Glyphs.TryGetValue (line[i], out var g)) continue;
            // For most of the characters, we use HAdvance (which includes the padding space
            // to use after this character), but for the last character we use Width (which is 
            // just up to the right edge of the bounding box of this character)
            if (i < line.Length - 1) x += g.HAdvance;
            else x += g.Width;
         }
         return x; 
      }
   }
   public override string ToString () => $"{Name}.lfont";
   
   // The immutable, readonly font glyphs. 
   readonly FrozenDictionary<int, Glyph> Glyphs;

   // Nested types -------------------------------------------------------------
   // A glyph contains the shape data needed to render an individual character.
   // An example: 107,0.81,3,k
   class Glyph (double adv, ImmutableArray<Poly> polys) {
      // The width this character uses, in terms of Ascender units. In this case,
      // if the ascender is 100, it means the character uses 0.81*100 = 81 units
      // by that scale
      public readonly double HAdvance = adv;
      // The shape geometry
      public readonly ImmutableArray<Poly> Polys = polys;
      // The width of the character (not including the whitespace on the right)
      public readonly double Width = polys.IsEmpty ? 0 : polys.Max (a => a.GetBound ().X.Max);
   }
}
#endregion
