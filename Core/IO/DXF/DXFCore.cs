// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFCore.cs
// ║║║║╬║╔╣║ Helper enumerations, classes for DXF reading/writing
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static EDXF;
using static ELineType;

#region enum EDXF ----------------------------------------------------------------------------------
// This enumeration holds various 'symbols' for DXF import - these include entity names, some
// header variables we are going to read. These are all loaded into a SymTable so we can obtain
// the corresponding value using a Span(byte) directly as the key (see SymTable class for details)
enum EDXF {
   NIL, 
   // All objects in this range are ignored
   __FIRSTIGNORE, LTYPE, ENDSEC, TABLE, ENDTAB, SEQEND, ATTDEF, __LASTIGNORE,
   // These are the entities we still have to implement!
   __FIRSTTODO, LEADER, HATCH, MATERIAL, MLEADERSTYLE, MLINESTYLE, DIMASSOC, XLINE, __LASTTODO,
   // These objects are all loaded using a 'simple load' - this means we can read in all
   // the key value pairs (since none repeat) before building the object
   __FIRSTSIMPLE, LAYER, STYLE, BLOCK, ENDBLK, SOLID, TRACE, CIRCLE, POINT, INSERT, 
   ARC, TEXT, MTEXT, ATTRIB, DIMSTYLE, __LASTSIMPLE,
   // These are handled using custom import routines (typically because they can contain
   // one or more repeated group codes)
   LINE, LWPOLYLINE, DIMENSION, SPLINE, POLYLINE, VERTEX, ELLIPSE,  
   // Header values we are going to read (the _ will get converted to $ when we make the
   // symbol table)
   _ACADVER, _DWGCODEPAGE, _MEASUREMENT, _CLAYER, _DIMASZ, _DIMEXE, _DIMEXO, _DIMTXT, _DIMCEN, 
   _DIMGAP, _DIMTIH, _DIMTOH, _DIMTOFL, _DIMTAD, _DIMSCALE, _DIMDEC, _DIMADEC, _INSUNITS, _DIMSTYLE,
   // Miscellaneous
   SECTION, HEADER, TABLES, BLOCKS, ENTITIES, CLASSES, LAYERS, BYBLOCK, BYLAYER,
   EOF
}
#endregion

#region class DXFCore ------------------------------------------------------------------------------
/// <summary>Static class containing various helper related to DXF import/export</summary>
public static partial class DXFCore {
   // Properties ---------------------------------------------------------------
   // Map with 256 AutoCAD colors (numbers 0 .. 255)
   internal static Color4[] ACADColors
      => sACADColors ??= [..Lib.ReadLines("nori:DXF/color.txt")
                          .Select(a => new Color4(uint.Parse(a, NumberStyles.HexNumber) | 0xff000000))];
   static Color4[]? sACADColors;

   // The SymTable that maps spans of bytes (characters) to EDXF values
   internal static SymTable<EDXF> Dict {
      get {
         if (sDict == null) {
            sDict = new ();
            for (var ed = NIL; ed <= EOF; ed++) {
               var s = ed.ToString ();
               if (s[0] == '_') {
                  if (s[1] == '_') continue;
                  s = s.Replace ('_', '$');
               }
               sDict.Add (s, ed);
            }
         }
         return sDict;
      }
   }
   static SymTable<EDXF>? sDict;

   // Set of 'special' blocks that we will skip (we don't import these blocks, or
   // INSERT objects that reference these blocks)
   internal static readonly HashSet<string> SkipBlocks = new (StringComparer.OrdinalIgnoreCase) {
      "", "*MODEL_SPACE", "*PAPER_SPACE", "*PAPER_SPACE0"
   };

   // Methods ------------------------------------------------------------------
   // Parses the encoded characters in the text to the corresponding special characters
   internal static string CleanText (string text, StringBuilder? sb = null) {
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
         }
         sb.Append (ch);
      }
      return sb.ToString ();
   }

   // Encodes a string into DXF form
   internal static string EncodeDXF (string s) {
      return s.Replace ("\u00b0", "%%d").Replace ("\u2205", "%%c");
   }

   // Convert a color name from DXF into a color value
   // Mostly, these color names are integers that map to colors, but this also handles
   // the special values BYLAYER and BYBLOCK (and returns Color4.Nil in those cases)
   internal static Color4 GetColor (ReadOnlySpan<byte> txt) {
      if (Dict.GetValueOrDefault (txt) is BYLAYER or BYBLOCK) return Color4.Nil;
      if (txt.Length == 0) return Color4.Nil;
      return ACADColors[txt.ToInt ().Clamp (0, 255)]; // TODO: Handle -ve colors to indicate visibility
   }

   // Converts an integer AutoCAD color value into a Color4
   internal static Color4 GetColor (int nColor) {
      if (nColor == 256) return Color4.Nil;
      return ACADColors[nColor.Clamp (0, 255)]; // TODO: Handle -ve colors to indicate visibility
   }

   // Converts a linetype name into an ELineType enumeration
   internal static ELineType GetLType (string s) 
      => sLinteypes.GetValueOrDefault (s, Continuous);
   static Dictionary<string, ELineType> sLinteypes = new (StringComparer.OrdinalIgnoreCase) {
      ["DOT"] = Dot, ["DOTTED"] = Dot, ["DASH"] = Dash, ["DASHED"] = Dash, ["DASHDOT"] = DashDot, 
      ["DIVIDE"] = DashDotDot, ["DASHDOTDOT"] = DashDotDot, ["DASH2DOT"] = DashDotDot, 
      ["CENTER"] = Center, ["BORDER"] = Border, ["DASH2"] = Dash2, ["HIDDEN"] = Hidden,
      ["PHANTOM"] = Phantom, ["CONTINUOUS"] = Continuous,
   };

   // Given a codepage, returns the corresponding Encoding
   internal static Encoding GetEncoding (string codepage) {
      if (!sEncodingsRegistered) {
         sEncodingsRegistered = true;
         Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
      }
      return Encoding.GetEncoding (GetCodePage (codepage)) ?? Encoding.UTF8;

      // Helper ............................................
      static int GetCodePage (string name) {
         if (name == "dos932") return 932;
         if (int.TryParse (name.Split ('_').Last (), out int n)) return n;
         return 1252;
      }
   }
   static bool sEncodingsRegistered;

   // Extracts text from the encoded MTEXT string and returns the corresponding E2Text entities.
   internal static IEnumerable<E2Text> MakeMText (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, ETextAlign align, StringBuilder sb) {
      var matches = sRxMText.Matches (text);
      if (matches.Count > 0) {
         sb.Clear ();
         int last = 0; var tspan = text.AsSpan ();
         foreach (Match M in matches) {
            // Extract the raw-text1 from the given string.
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
         text = sb.ToString ();

         // Helpers ........................................
         void Append (string text1) => sb.Append (text1);
         void Append2 (ReadOnlySpan<char> text2) => sb.Append (text2);
      }

      // Now cleanup the raw-string by removing code-blocks ({...}) and split
      // them into multiple lines by the line-break (\P).
      string[] lines = text.Replace ("{", "").Replace ("}", "").Split ("\\P");
      // Output a text entity for each line.
      double dyLine = 0;
      var mat = Matrix2.Rotation (pos, angle);
      foreach (var line in lines) {
         var pt = pos;
         if (!dyLine.IsZero ()) {
            pt = new (pt.X, pt.Y - dyLine);
            if (!angle.IsZero ()) pt *= mat;
         }
         var ent = new E2Text (layer, style, CleanText (line, sb), pt, height, angle, style.Oblique, style.XScale, align);
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
   static readonly Regex sRxMText = new (
     @"(\\[Ff][^|;]+((\|([bicp])\d+)+)?;)|" +   // Font name & style (e.g., \fTimes New Roman|b1|i0;)
     @"(\\[AHWCT](\d*?(\.\d+)?x?));|" +         // Height, Width, Alignment, Color codes like: \H3x; \H12.500; \W0.8x;
     @"(\\[LlOoKk])|" +                         // Underline, Overstrike, Strikethrough: \L \l \O \K
     @"\\U\+(?<hex4>[0-9A-Fa-f]{4})|" +         // Match 4 hex digits prefixed with \U+
     @"(\\S(?<fract>[^;]+[#/\^][^;]+);)",       // Stacking fractions like: \S+0.8^+0.1; \S+0.8#+0.1;
     RegexOptions.Compiled);

   // Convert the special text in the DXF to a bend line, unless is match with the sBend format
   static internal void ProcessBendText (Dwg2 dwg) {
      var ents = dwg.Ents;
      List<Ent2> bend = [], rmv = [];
      foreach (var e2t in ents.OfType<E2Text> ()) {
         var match = sBend.Match (e2t.Text);
         if (!match.Success) continue;
         var e2p = ents.OfType<E2Poly> ().Where (a => a.Poly.IsLine).MinBy (a => a.Poly.GetDistance (e2t.Pt).Dist);
         if (e2p == null) continue;
         rmv.AddRange (e2t, e2p);
         double angle = match.Groups[1].Value.ToDouble ().D2R ().Clamp (-Lib.PI, Lib.PI);
         double radius = match.Groups[2].Value.ToDouble ();
         double kfactor = match.Groups[3].Value.ToDouble ();
         if (kfactor > 0.501) kfactor /= 2;
         bend.Add (new E2Bendline (dwg, e2p.Poly.Pts, angle, radius, kfactor));
      }
      foreach (var a in rmv) ents.Remove (a);
      foreach (var b in bend) ents.Add (b);
   }
   static readonly Regex sBend = new (@"A([-+]?[0-9]*\.?[0-9]+)\s*R([0-9]*\.?[0-9]+)\s*K([0-9]*\.?[0-9]+)",
      RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

   // Helper used to report unknown entities in DXF (only in debug builds)
   static internal partial void UnknownEnt (string s);

   #if DEBUG
   static internal partial void UnknownEnt (string s) {
      sIgnore ??= [.. Lib.ReadLines ("nori:DXF/ent-ignore.txt")];
      if (sIgnore.Contains (s)) return;
      Lib.Trace ($"Unknown entity {s} in DXFReader");
      sIgnore.Add (s);
   }
   static HashSet<string>? sIgnore;
   #else
   static internal partial void UnknownEnt (string s) { }
   #endif
}
#endregion
