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
      string lname = name.ToLower ();
      // Try to get font from the cache.
      var font = sFonts.SafeGet (lname);
      if (font != null) return font;
      string[]? lines = null;
      // Load font from wad and return default if the font file is missing.
      // As font resources are one of the internal data files, rigorous error handling
      // has been ommitted during the load in favor of simpler code.
      try { lines = Lib.ReadLines ($"wad:DXF/{lname}.lfont"); } catch { }
      if (lines == null || lines.Length == 0) return Get ("simplex");
      int n = 0; string[] w = Next (3); name = w[1];
      Dictionary<int, Glyph> glyphs = [];
      w = Next ();
      var (nchars, asc, desc, vadv) = (w[0].ToInt (), w[1].ToDouble (), w[2].ToDouble (), w[3].ToDouble ());
      List<Poly> polys = []; PolyBuilder builder = new ();
      for (int i = 0; i < nchars; i++) {
         polys.Clear (); w = Next ();
         var (code, adv, npoly, ch) = (w[0].ToInt (), w[1].ToDouble (), w[2].ToInt (), w[3][0]);
         for (int j = 0; j < npoly; j++) polys.Add (builder.Build (Line ()));
         glyphs[code] = new (code, adv, ch, [.. polys]);
      }
      font = new LineFont (name, nchars, asc, desc, vadv, glyphs.ToFrozenDictionary ());
      return sFonts[font.Name.ToLower ()] = font;

      string Line () => lines[n++]; // Fetch next line
      string[] Next (int n = 4) => Line ().Split (',', n); // Fetch tokens from next line
   }
   // The LFONT database.
   static readonly Dictionary<string, LineFont> sFonts = [];

   /// <summary>This renders the given text to a List of Poly. </summary>
   /// Each character in the text is taken, and the set of Poly objects representing that character
   /// is cloned, scaled, rotated and translated based on pos, height and angle. It is then added to 
   /// the output, and the 'current position' is advanced by the advance width of that character.
   /// Thus, the input point pos becomes the lower-left corner where we start rendering the text.
   /// <param name="text">The text to render.</param>
   /// <param name="pos">The start position.</param>
   /// <param name="oblique">Oblique angle of the font in radians.</param>
   /// <param name="xscale">XScale factor of the font.</param>
   /// <param name="height">Height of the font.</param>
   /// <param name="angle">Rotation angle in radians.</param>
   /// <param name="output">The output list where the rendered output is collected.</param>
   public void Render (string text, Point2 pos, double oblique, double xscale, double height, double angle, List<Poly> output) {
      // Initialize the factor by which glyphs have to be scaled.
      // It will be a constant for this render call.
      var scale = height / Ascender;
      // Compose the scaling and rotation matrices.
      Matrix2 mat0 = Matrix2.Scaling (xscale * scale, scale), mat1 = Matrix2.Rotation (pos, angle);
      // Initialize the (x, y) positions of the 'next' char along with the line-gap, dy.
      double x = pos.X, y = pos.Y, dy = scale * VAdvance;
      // Compose the oblique matrix
      if (!oblique.IsZero ()) mat0 *= new Matrix2 (1, 0, Math.Tan (oblique), 1, 0, 0);
      // Render the text characters now.
      foreach (char ch in text) {
         if (ch == '\n') {
            // We found a linebreak. Reset x and advance y.
            x = pos.X; y -= dy;
            continue;
         }
         if (!Glyphs.TryGetValue (ch, out var g)) continue;
         if (g.Polys.Length > 0) {
            // Transform and output the glyph shape.
            var mat = mat0 * Matrix2.Translation (x, y);
            if (!angle.IsZero ()) mat *= mat1;
            output.AddRange (g.Polys.Select (x => x * mat));
         }
         // Advance the x-position by HAdvance.
         x += g.HAdvance * height;
      }
   }

   public override string ToString () => $"{Name}.lfont";
   
   // The immutable, readonly font glyphs. 
   readonly FrozenDictionary<int, Glyph> Glyphs;

   // A glyph contains the shape data needed to render an individual character.
   // An example: 107,0.81,3,k
   class Glyph (int code, double adv, char ch, ImmutableArray<Poly> polys) {
      // The unicode character code, in this case 107 (meaning lower-case k) 
      public readonly int CharCode = code;
      // The width this character uses, in terms of Ascender units. In this case,
      // if the ascender is 100, it means the character uses 0.81*100 = 81 units
      // by that scale
      public readonly double HAdvance = adv;
      // The number of Poly objects used to define this character
      public readonly int NPoly = polys.Length;
      // The actual character itself (in this case 'k') - this is not used by the
      // LFONT parser, but is more to assist easy reading of LFONT files
      public readonly char Char = ch;
      // The shape geometry
      public readonly ImmutableArray<Poly> Polys = polys;
      public override string ToString () => $"{Char}:{CharCode}";
   }
}
#endregion
