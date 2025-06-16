using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Nori;

public partial class DXFReader {
   /// <summary>Convert white entities to black on import</summary>
   public static bool WhiteToBlack = true;
   public static bool DarkenColors = true;

   /// <summary>The Standard 256 ACAD Colors</summary>
   public static Color4[] ACADColors {
      get {
         if (sACADColors == null) {
            sACADColors = [..Lib.ReadLines ("nori:DXF/color.txt")
                            .Select (a => new Color4 (uint.Parse (a, NumberStyles.HexNumber) | 0xff000000))];
            Debug.Assert (sACADColors.Length == 256);
         }
         return sACADColors;
      }
   }
   static Color4[]? sACADColors;

   /// <summary>Convert an AutoCAD color to a Color4</summary>
   public static Color4 GetColor (int index) {
      if (index == 256) return Color4.Nil;
      var color = ACADColors[index.Clamp (0, 255)];
      if (WhiteToBlack && color.EQ (Color4.White)) color = Color4.Black;
      if (DarkenColors) color = color.Darkened ();
      return color;
   }
   public Color4 GetColor () => GetColor (ColorNo);

   /// <summary>Converts a DXF linetype string to the corresponding ELineType enum value </summary>
   ELineType GetLType (string lt) => lt.ToUpper () switch {
      "DOT" or "DOTTED" => ELineType.Dot,
      "DASH" or "DASHED" => ELineType.Dash,
      "DASHDOT" => ELineType.DashDot,
      "DIVIDE" or "DASHDOTDOT" or "DASH2DOT" => ELineType.DashDotDot,
      "CENTER" => ELineType.Center,
      "BORDER" => ELineType.Border,
      "HIDDEN" => ELineType.Hidden,
      "DASH2" => ELineType.Dash2,
      "PHANTOM" => ELineType.Phantom,
      _ => ELineType.Continuous,
   };

   // Add drawing --------------------------------------------------------------
   void Add (Poly poly) => Add (new E2Poly (Layer, poly) { Color = GetColor () });
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);
   void Add (Ent2 ent) {
      if (Invisible) return;
      if (mBlockEnts != null) { ent.InBlock = true; mBlockEnts.Add (ent); }
      else mDwg.Add (ent);
   }

   // Make an ellipse and adds it
   void AddEllipse (Point2 cen, Vector2 major, double ratio, (double, double) aRange) {
      Point2 east = cen + major;
      var (aStart, aEnd) = aRange;
      while (aEnd < aStart) aEnd += Lib.TwoPI;
      double R = major.Length, r = R * ratio, aSpan = aEnd - aStart;
      // Figure out the number of steps for discretization (5 degrees per step)
      int c = (int)Math.Max (4.0, (aSpan / (Math.PI / 36)).Round (0));
      double aStep = aSpan / c, rot = cen.AngleTo (east);
      bool closed = aSpan.EQ (2 * Math.PI);
      if (closed) c--;

      for (int i = 0; i <= c; i++) {
         var (sin, cos) = Math.SinCos (aStart + i * aStep);
         var node = new Point2 (R * cos, r * sin * ZDir).Rotated (rot);
         mPolyBuilder.Line (node.Moved (cen.X, cen.Y));
      }
      if (closed) mPolyBuilder.Close ();
      Add (mPolyBuilder.Build ());
   }

   // Make a Polyline
   void AddPolyline () {
      if (mVertex.Count > 0) {
         // If there are curve-fit vertices, remove the others
         if (mVertex.Any (a => (a.Flags & 8) != 0))
            mVertex = mVertex.Where (a => (a.Flags & 8) != 0).ToList ();
         for (int i = 0; i < mVertex.Count; i++) {
            if (mVertex[i].Bulge > 1e6 || mVertex[i].Bulge.IsZero ()) mPolyBuilder.Line (mVertex[i].Pt);
            else mPolyBuilder.Arc (mVertex[i].Pt, mVertex[i].Bulge);
         }
         if (mIsClosed) mPolyBuilder.Close ();
         Add (mPolyBuilder.Build ());
         mVertex.Clear ();
      }
   }

   // Extracts text from the encoded MTEXT string and returns the corresponding E2Text entities.
   IEnumerable<E2Text> AddMText (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, ETextAlign align) {
      var matches = sRxMText.Matches (text);
      if (matches.Count > 0) {
         mSB.Clear ();
         int last = 0; var tspan = text.AsSpan ();
         foreach (Match M in matches) {
            // Extract the raw-text from the given string.
            if (M.Index > last) Append2 (tspan[last..M.Index]);
            if (M.Groups.TryGetValue ("fract", out var fract) && fract.ValueSpan.Length > 0) {
               // No special fraction rendering is supported. Just concatenate using the division '/' symbol.
               Append (fract.Value.Replace ("^", "/").Replace ("#", "/"));
            }
            if (M.Groups.TryGetValue ("hex4", out var hex4) && hex4.ValueSpan.Length > 0) {
               // Replace the 4 hex digits with the corresponding unicode character
               int uni = int.Parse (hex4.Value, NumberStyles.HexNumber);
               Append (((char)uni).ToString ());
            }
            last = M.Index + M.Length;
         }
         if (last < text.Length) Append2 (tspan[last..]);
         text = mSB.ToString ();

         void Append (string text) => mSB.Append (text);
         void Append2 (ReadOnlySpan<char> text) => mSB.Append (text);
      }
      // Now cleanup the raw-string by removing code-blocks ({...}) and split
      // them into multiple lines by the line-break (\P).
      string[] lines = text.Replace ("{", "").Replace ("}", "").Split ("\\P");
      // Output a text entity for each line.
      double dyLine = 0;
      var mat = Matrix2.Rotation (pos, angle);
      for (int i = 0; i < lines.Length; i++) {
         var pt = pos;
         if (!dyLine.IsZero ()) {
            pt = new (pt.X, pt.Y - dyLine);
            if (!angle.IsZero ()) pt *= mat;
         }
         var ent = new E2Text (layer, style, Clean (lines[i], mSB), pt, height, angle, style.Oblique, style.XScale, align) { Color = GetColor () };
         dyLine += ent.DYLine;
         yield return ent;
      }
   }
   // The RegEx used to parse various escape-sequences in MText (See Doc\MText-Codes.pdf for a reference).
   // A MTEXT text entity can specify inline text styles and formatting. The Regex below identifies
   // the format strings and extracts the raw-text out of them. Multiple patterns are supported, and
   // each is on a separate line. These patterns are combined using the OR operator. The paragraph
   // markers (\P) and the style code-blocks ({...}) are left unidentified and are
   // processed after the text extraction.
   static Regex sRxMText = new (
     @"(\\[Ff][^|;]+((\|([bicp])\d+)+)?;)|" +   // Font name & style (e.g., \fTimes New Roman|b1|i0;)
     @"(\\[AHWCT](\d*?(\.\d+)?x?));|" +         // Height, Width, Alignment, Color codes like: \H3x; \H12.500; \W0.8x;
     @"(\\[LlOoKk])|" +                         // Underline, Overstrike, Strikethrough: \L \l \O \K
     @"\\U\+(?<hex4>[0-9A-Fa-f]{4})|" +         // Match 4 hex digits prefixed with \U+
     @"(\\S(?<fract>[^;]+[#/\^][^;]+);)",       // Stacking fractions like: \S+0.8^+0.1; \S+0.8#+0.1;
     RegexOptions.Compiled);

   // Parses the encoded characters in the text to the corresponding special characters
   internal static string Clean (string text, StringBuilder? sb = null) {
      if (!text.Contains ("%%")) return text;
      (sb ??= new ()).Clear ();
      int len = text.Length, i = 0;
      while (i < len) {
         char ch = text[i++];
         if (ch == '%' && len > i + 1 && text[i] == '%') {
            switch (text[i + 1]) {
               case 'd' or 'D': ch = (char)0xB0; i += 2; break;
               case 'p' or 'P': ch = (char)0xB1; i += 2; break;
               case 'c' or 'C': ch = (char)0x2205; i += 2; break;
            }
            ;
         }
         sb.Append (ch);
      }
      return sb.ToString ();
   }

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      var line1 = mReader.ReadLine (); if (line1 == null) return false;
      var line2 = mReader.ReadLine (); if (line2 == null) return false;
      G = int.Parse (line1); V = line2;
      return V != "EOF";
   }

   // Nested types -------------------------------------------------------------
   // A structure read in from a 'VERTEX' entity
   readonly struct Vertex {
      public Vertex (Point2 pt, int flags, double bulge) => (Pt, Flags, Bulge) = (pt, flags, bulge);
      public readonly Point2 Pt;
      public readonly int Flags;
      public readonly double Bulge;
   }

   // Private data -------------------------------------------------------------
   int G; string V = "";         // Current Group-Code and Group-Value (updated by Next())
   readonly Dwg mDwg = new ();   // The drawing we are building;
   readonly string mFile;        // The file we're reading from
   StreamReader mReader;         // Reader we're loading from
   static readonly HashSet<string> sSkipBlocks = [
      "*Model_Space", "*Paper_Space", "*Paper_Space0", "*MODEL_SPACE", "*PAPER_SPACE", "*PAPER_SPACE0"
   ];
}
