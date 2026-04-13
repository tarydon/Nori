// ────── ╔╗
// ╔═╦╦═╦╦╬╣ AltDXFCore.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Alt;
using static EDXF;
using static ELineType;

// enum EDXF -------------------------------------------------------------------
enum EDXF {
   NIL, SKIPPEDENT,

   _FIRSTENT,

   // All objects in this range are ignored
   _FIRSTIGNORE,
   LTYPE, ENDSEC, TABLE, ENDTAB, SEQEND, 

   // These are the entities we still have to implement!
   DIMSTYLE, LEADER, ATTDEF, HATCH, MATERIAL, MLEADERSTYLE, MLINESTYLE, DIMASSOC, XLINE,
   _LASTIGNORE,

   // These objects are all loaded using a 'simple load' - this means we can read in all
   // the key value pairs (since none repeat) before building the object
   _FIRSTSIMPLE,
   LAYER, STYLE, BLOCK, ENDBLK, SOLID, TRACE, CIRCLE, POINT, INSERT, ARC, TEXT, MTEXT, ATTRIB,
   _LASTSIMPLE,

   // These are handled using custom import routines (typically because they can contain
   // one or more repeated group codes)
   LINE, LWPOLYLINE, DIMENSION, SPLINE, POLYLINE, VERTEX, ELLIPSE,

   // These are the entities we are going to try and read (this also includes things like
   // LAYER, STYLE etc that don't reside in the ENTITIES section, but in other sections such
   // as the TABLES section)
   _LASTENT,

   // These are the other objects (not entities) that we are going to not skip over
   _FIRSTAUX,
   SECTION, 
   _LASTAUX,

   // Header values we are going to read
   _ACADVER, _DWGCODEPAGE, _MEASUREMENT, _CLAYER, _INSUNITS,

   // Miscellaneous
   HEADER, TABLES, BLOCKS, ENTITIES, CLASSES, LAYERS, BYBLOCK, BYLAYER,
   EOF
}

// class DXFCore ---------------------------------------------------------------
public class DXFCore {
   internal static Color4[] ACADColors
      => sACADColors ??= [..Lib.ReadLines("nori:DXF/color.txt")
                          .Select(a => new Color4(uint.Parse(a, NumberStyles.HexNumber) | 0xff000000))];
   static Color4[]? sACADColors;

   internal static SymTable<EDXF> Dict {
      get {
         if (sDict == null) {
            sDict = new ();
            for (var ed = _FIRSTENT; ed <= EOF; ed++) {
               var s = ed.ToString ();
               if (s.StartsWith ("_3")) s = s[1..];
               else if (s[0] == '_') s = s.Replace ('_', '$');
               sDict.Add (s, ed);
            }
         }
         return sDict;
      }
   }
   static SymTable<EDXF>? sDict;

   internal static readonly HashSet<string> SkipBlocks = new (StringComparer.OrdinalIgnoreCase) {
      "", "*MODEL_SPACE", "*PAPER_SPACE", "*PAPER_SPACE0"
   };

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

   internal static Color4 GetColor (ReadOnlySpan<byte> txt) {
      if (Dict.GetValueOrDefault (txt) is BYLAYER or BYBLOCK) return Color4.Nil;
      if (txt.Length == 0) return Color4.Nil;
      return ACADColors[txt.ToInt () & 255];
   }

   internal static ELineType GetLType (string s) 
      => sLinteypes.GetValueOrDefault (s, Continuous);
   static Dictionary<string, ELineType> sLinteypes = new (StringComparer.OrdinalIgnoreCase) {
      ["DOT"] = Dot, ["DOTTED"] = Dot, ["DASH"] = Dash, ["DASHED"] = Dash, ["DASHDOT"] = DashDot, 
      ["DIVIDE"] = DashDotDot, ["DASHDOTDOT"] = DashDotDot, ["DASH2DOT"] = DashDotDot, 
      ["CENTER"] = Center, ["BORDER"] = Border, ["DASH2"] = Dash2, ["HIDDEN"] = Hidden,
      ["PHANTOM"] = Phantom, ["CONTINUOUS"] = Continuous,
   };

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

   static internal void UnknownEnt (string s) {
      sIgnore ??= [.. Lib.ReadLines ("nori:DXF/ent-ignore.txt")];
      if (sIgnore.Contains (s)) return;
      Lib.Trace ($"Unknown entity {s} in DXFReader");
      sIgnore.Add (s);
      Environment.Exit (-1);
   }
   static HashSet<string>? sIgnore;
}
