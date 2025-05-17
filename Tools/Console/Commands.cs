// ────── ╔╗                                                                                    CON
// ╔═╦╦═╦╦╬╣ Commands.cs
// ║║║║╬║╔╣║ Implements several commands (LineCount, SrcClean, ComputeCoverage, GetNextId ...)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace Nori.Con;

#region class ComputeCoverage ----------------------------------------------------------------------
/// <summary>Run the Nori.Test and compute the coverage</summary>
static class ComputeCoverage {
   public static void Run () {
      // First, run Nori.Test under 'dotnet-coverage' to generate a coverage.xml file in N:/Bin
      try {
         var pi = new ProcessStartInfo ("dotnet-coverage.exe", "collect Nori.Test.exe -f xml -o coverage.xml") { WorkingDirectory = $"{Lib.DevRoot}/Bin" };
         Process.Start (pi)!.WaitForExit ();
      } catch (Exception) {
         Program.Fatal ("Could not run the dotnet-coverage tool.\nUse 'dotnet tool install --global dotnet-coverage' to install.");
      }
      // Load the coverage file into N:/Bin/Coverage.xml
      Console.WriteLine ();
      var c = new Coverage ($"{Lib.DevRoot}/Bin/coverage.xml");
      TestRunner.SetNoriFiles (c);

      List<Datum> data = [];
      foreach (var file in c.Files) {
         var blocks = c.GetBlocksFor (file).ToList ();
         int total = blocks.Count, covered = blocks.Count (b => b.Covered);
         double f = Math.Round (100.0 * covered / total, 2);
         data.Add (new (file[3..], total, covered, f));
      }
      data = [.. data.OrderByDescending (a => a.Percent)];

      Console.WriteLine ("                             File  Blocks  Covered Uncovered       %");
      Console.WriteLine ("-----------------------------------------------------------------------");
      foreach (var datum in data)
         Console.WriteLine ($"{datum.File,33}{datum.Blocks,8}{datum.Covered,9}{datum.Uncovered,9}{datum.Percent,8:F1}");

      int cBlocks = c.Blocks.Count, cCovered = c.Blocks.Count (a => a.Covered), cUncovered = cBlocks - cCovered;
      double fPercent = Math.Round (100.0 * cCovered / cBlocks, 2);
      Console.WriteLine ("----------------------------------------------------------------------");
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine ($"{data.Count,33}{cBlocks,8}{cCovered,9}{cUncovered,9}{fPercent,9:F2} %");
      Console.ResetColor ();
   }

   readonly record struct Datum (string File, int Blocks, int Covered, double Percent) {
      public int Uncovered => Blocks - Covered;
   }
}
#endregion

#region class LineCount ----------------------------------------------------------------------------
/// <summary>Helper to count source code lines, and comment ratio</summary>
static class LineCount {
   public static void Run () {
      var (prevDir, nFiles, nLines, nComments) = ("", 0, 0, 0);
      Console.WriteLine ("  No                    Path File                   Lines Comments       %");
      Console.WriteLine ("---------------------------------------------------------------------------");
      foreach (var file in Directory.EnumerateFiles ("N:\\", "*.cs", SearchOption.AllDirectories)) {
         if (SrcClean.ExcludeFile (file)) continue;
         var code = File.ReadAllLines (file);
         int comments = code.Count (a => a.TrimStart ().StartsWith ("//"));

         nFiles++;
         var dir = Path.GetDirectoryName (file)!;
         Console.Write (nFiles.ToString ().PadLeft (4));
         if (dir != prevDir) {
            Console.Write (dir.PadLeft (24));
            prevDir = dir;
         } else Console.Write (" ".PadLeft (24));
         Console.Write (" " + Path.GetFileName (file).PadRight (20));

         Console.Write (code.Length.ToString ().PadLeft (8));
         Console.Write (comments.ToString ().PadLeft (9));

         double percent = Math.Round (100.0 * comments / code.Length, 1);
         Console.Write (percent.ToString ("F1").PadLeft (8));

         Console.WriteLine ();
         nLines += code.Length; nComments += comments;
      }
      Console.WriteLine ("---------------------------------------------------------------------------");
      string tPercent = (100.0 * nComments / nLines).ToString ("F1");
      var dt = DateTime.Now; dt = new DateTime (dt.Year, dt.Month, dt.Day);
      var dt0 = new DateTime (2024, 10, 13);
      int days = (int)((dt - dt0).TotalDays + 0.5);
      Console.WriteLine ($"{nFiles,4}                     Day {days,-4} {nLines,23}{nComments,9}{tPercent,8}%");
   }
}
#endregion

#region class NextId -------------------------------------------------------------------------------
/// <summary>Executes the NextID command (prints the next available test id)</summary>
static class GetNextId {
   public static void Run () {
      Assembly assy = Assembly.LoadFile (Lib.GetLocalFile ("Nori.Test.dll"));
      var tests = TestRunner.Gather ([assy]);
      var fixtures = tests.Select (a => a.Fixture).Distinct ().ToList ();
      int maxTest = tests.Max (a => a.Id), maxFixture = fixtures.Max (a => a.Id);
      Console.WriteLine ($"Next Fixture: {maxFixture + 1}, Next Test: {maxTest + 1}");

      List<int> ids = tests.Select (a => a.Id).Distinct ().ToList ();
      if (ids.Count != tests.Count) {
         Console.WriteLine ("\nDuplicate test Ids:");
         foreach (var id in ids) {
            var set = tests.Where (a => a.Id == id).ToList ();
            if (set.Count > 1) {
               foreach (var test in set) {
                  Console.Write ($"  {test.Id}   ");
                  Console.ForegroundColor = ConsoleColor.Yellow;
                  Console.Write ($"{test.Method.DeclaringType?.Name}.{test.Method.Name}()   ");
                  Console.ResetColor ();
                  Console.WriteLine ($"\"{test.Description}\"");
               }
            }
         }
      }
      ids = fixtures.Select (a => a.Id).Distinct ().ToList ();
      if (ids.Count != fixtures.Count) {
         Console.WriteLine ("\nDuplicate fixture Ids:");
         foreach (var id in ids) {
            var set = fixtures.Where (a => a.Id == id).ToList ();
            if (set.Count > 1) {
               foreach (var fix in set) {
                  Console.Write ($"  {fix.Id}   ");
                  Console.ForegroundColor = ConsoleColor.Yellow;
                  Console.Write ($"class {fix.Type.Name}   ");
                  Console.ResetColor ();
                  Console.WriteLine ($"\"{fix.Description}\"");
               }
            }
         }
      }
   }
}
#endregion

#region class SetOptimize --------------------------------------------------------------------------
/// <summary>Turns optimization on / off for all Nori projects</summary>
static class SetOptimize {
   public static void Run (bool optimize) {
      string off = "<Optimize>false</Optimize>", on = "<Optimize>true</Optimize>";
      foreach (var file in Directory.EnumerateFiles ($"{Lib.DevRoot}/", "*.csproj", SearchOption.AllDirectories)) {
         string text = File.ReadAllText (file), original = text;
         if (optimize) text = text.Replace (off, on);
         else text = text.Replace (on, off);
         if (text != original) {
            Console.WriteLine (file.Replace ('\\', '/'));
            File.WriteAllText (file, text);
         }
      }
   }
}
#endregion

#region class SetXmlDoc ----------------------------------------------------------------------------
/// <summary>Turns XML documentation on / off for all Nori projects</summary>
static class SetXmlDoc {
   public static void Run (bool generate) {
      string off = "<GenerateDocumentationFile>false</GenerateDocumentationFile>", on = "<GenerateDocumentationFile>true</GenerateDocumentationFile>";
      foreach (var file in Directory.EnumerateFiles ($"{Lib.DevRoot}/", "*.csproj", SearchOption.AllDirectories)) {
         string text = File.ReadAllText (file), original = text;
         if (generate) text = text.Replace (off, on);
         else text = text.Replace (on, off);
         if (text != original) {
            Console.WriteLine (file.Replace ('\\', '/'));
            File.WriteAllText (file, text);
         }
      }
   }
}
#endregion

#region class SrcClean -----------------------------------------------------------------------------
/// <summary>Helper to clean up source code</summary>
/// Replaces 3-line summary blocks with single line comments
static class SrcClean {
   public static void Run () {
      var (prevDir, nCleaned) = ("", 0);
      foreach (var file in Directory.EnumerateFiles ($"{Lib.DevRoot}/", "*.cs", SearchOption.AllDirectories)) {
         if (ExcludeFile (file)) continue;
         var dir = Path.GetDirectoryName (file)!;
         if (dir != prevDir) {
            if (Console.CursorLeft != 0) Console.WriteLine ();
            Lib.Print ($"{dir}", ConsoleColor.Blue); prevDir = dir;
         }
         string name = $"  {Path.GetFileNameWithoutExtension (file)}";
         if (Console.CursorLeft + name.Length >= Console.WindowWidth) Console.WriteLine ();
         bool cleaned = Clean (file);
         if (cleaned) nCleaned++;
         Lib.Print (name, cleaned ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
      }
      Console.WriteLine ();
      Console.WriteLine ($"{nCleaned} files cleaned.");
   }

   /// <summary>Exclude this file from cleaning, statistics gathering etc</summary>
   public static bool ExcludeFile (string file) {
      file = file.Replace ('\\', '/');
      if (file.EndsWith (".g.cs")) return true;
      if (file.Contains ("/obj/")) return true;
      if (file.Contains ("GeneratedFile")) return true;
      if (file.StartsWith ($"{Lib.DevRoot}/Scratch/")) return true;
      return false;
   }

   static bool Clean (string file) {
      // First, clean up 3-line summary descriptions into single lines
      var (lines, cleaned) = (File.ReadAllLines (file).ToList (), false);
      for (int i = lines.Count - 3; i >= 0; i--) {
         string a = lines[i].Trim (), b = lines[i + 1].Trim (), c = lines[i + 2].Trim ();
         if (a == "/// <summary>" && b.StartsWith ("///") && c == "/// </summary>") {
            lines[i] = lines[i] + b[3..].Trim () + "</summary>";
            lines.RemoveAt (i + 2); lines.RemoveAt (i + 1);
            i--; cleaned = true;
         }
      }

      // Next, add the Nori banner for files that don't have them
      if (lines.Count > 1 && !lines[0].Trim ().StartsWith ("//")) {
         string name = Path.GetFileName (file);
         lines.Insert (0,  "// ────── ╔╗");
         lines.Insert (1, $"// ╔═╦╦═╦╦╬╣ {name}");
         lines.Insert (2,  "// ║║║║╬║╔╣║ <<TODO>>");
         lines.Insert (3,  "// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────");
         cleaned = true;
      }
      if (cleaned) File.WriteAllLines (file, lines);
      return cleaned;
   }
}
#endregion

#region LFF2LFontConverter ------------------------------------------------------------------------
/// <summary>Converts LFF font files to the custom LFONT format</summary>
// This utility parses character definitions from an LFF file,
// scales and translates the glyph geometry, and outputs an LFONT-compliant
// file with character codes, names, and polyline path data.
public class LFF2LFontConverter {
   /// <summary>Builds an LFONT file from an LFF font definition file</summary>
   /// This method reads an LFF file containing glyph definitions (points, arcs, reuse info),
   /// processes each character, scales its geometry, and writes a structured LFONT output.
   /// <param name="lffFilePath">Path to the source LFF font file.</param>
   /// <param name="lfontFilePath">Path to the destination LFONT file to be created.</param>
   public static void BuildLFont (string lffFilePath, string lfontFilePath) {
      // Read all lines from the LFF file
      var lines = File.ReadAllLines (lffFilePath);

      // Initialize LFONT header with name and version (1)
      var output = new List<string> {
        $"LFONT,{Path.GetFileNameWithoutExtension(lffFilePath)},1",
    };

      var charCache = new Dictionary<string, FontChar> (); // Cache of parsed characters
      ReadOnlySpan<char> codeHex = "", currentChar = "", reuseKey = "";
      var glyphLines = new List<string> (); // Temporary glyph drawing output
      FontChar? fc = null;
      double maxX = 0, minX = 0, maxY = 0, minY = 0,
             letterSpacing = 0, wordSpacing = 0, lineSpacingFactor = 1;

      // Process each line in the LFF file
      foreach (var rawLine in lines) {
         var line = rawLine.Trim ();
         switch (line.FirstOrDefault ()) {
            case '#': // Comment line
               switch (line) {
                  case var s when s.StartsWith ("# LetterSpacing:"):
                     letterSpacing = double.Parse (s["# LetterSpacing:".Length..].Trim ());
                     break;

                  case var s when s.StartsWith ("# WordSpacing:"):
                     wordSpacing = double.Parse (s["# WordSpacing:".Length..].Trim ());
                     break;

                  case var s when s.StartsWith ("# LineSpacingFactor:"):
                     lineSpacingFactor = double.Parse (s["# LineSpacingFactor:".Length..].Trim ());
                     break;
               }
               break;

            case '[':
               // Start of new glyph
               codeHex = line.AsSpan ()[1..5];   // Hex code (e.g., 0041 for 'A')
               currentChar = line[6..].Trim ();  // Symbol (e.g., 'A')
               break;

            case 'C':
               // Reuse key declaration
               reuseKey = line.AsSpan ()[1..];
               break;

            case '\0':
               // End of glyph block
               if (fc != null &&
                   int.TryParse (codeHex, NumberStyles.HexNumber, null, out int charCode)) {
                  fc.CharCode = charCode;
                  fc.Symbol = currentChar.ToString ();
                  fc.ReuseKey = charCache.TryGetValue (reuseKey.ToString ().ToLower (), out var reused) ? reused : null;
                  fc.Width = Math.Abs (maxX - minX) + letterSpacing; // Calculate advance width
                  charCache[codeHex.ToString ()] = fc;
               }
               // Reset state for next glyph
               fc = null;
               maxX = minX = maxY = minY = 0;
               glyphLines.Clear ();
               reuseKey = "";
               break;

            default:
               // Glyph polyline definition
               fc ??= new FontChar ();
               fc.Points.Add (line);

               // Parse and compute bounds
               var pts = line.Split (';').Select (ParsePoint).ToArray ();
               maxX = Math.Max (maxX, pts.Max (p => p.X));
               minX = Math.Min (minX, pts.Min (p => p.X));
               maxY = Math.Max (maxY, pts.Max (p => p.Y));
               minY = Math.Min (minY, pts.Min (p => p.Y));

               // Update global font metrics
               if (maxY > mAscender) mAscender = maxY;
               if (minY < mDescender) mDescender = minY;
               break;
         }
      }

      // Add a default space glyph (code 32)
      charCache["0020"] = new FontChar { Symbol = " ", CharCode = 32, Width = wordSpacing, Points = [] };
      // Write font header (character count, ascender, descender, vAdvance)
      output.Add ($"{charCache.Count},{mAscender:R},{mDescender:R},{(mAscender - mDescender) * lineSpacingFactor:R}");
      // Build LFONT glyph output
      foreach (var val in charCache.Values) {
         double hAdvance = (val.Width / mAscender).R6 ();
         // Character metadata: code, advance width, polyline count, symbol
         output.Add ($"{val.CharCode},{hAdvance},{val.Count},{val.Symbol}");
         // Add reused polylines if any
         if (val.ReuseKey?.Points is { Count: > 0 } reusedPts)
            output.AddRange (reusedPts);

         foreach (var line in val.Points) {
            var parts = line.Split (';');
            if (parts.Length < 2) continue;
            var p0 = ParsePoint (parts[0]);
            var seg = new StringBuilder ().Append (" M").Append (FormatPt (ScalePt (p0, hAdvance, lineSpacingFactor)));
            var prev = p0;

            // Process each point or arc in the polyline
            for (int j = 1; j < parts.Length; j++) {
               var pt = parts[j];
               if (pt.Contains ('A')) {
                  var (arcEnd, bulge) = ParseArc (pt);
                  if (bulge == 0) {
                     // Treat zero-bulge arc as line
                     seg.Append (" L").Append (FormatPt (ScalePt (arcEnd, hAdvance, lineSpacingFactor)));
                     prev = arcEnd;
                  } else {
                     foreach (var arcPoint in GetArcPoints (prev, arcEnd, bulge))
                        seg.Append (" L").Append (FormatPt (ScalePt (arcPoint, hAdvance, lineSpacingFactor)));
                     prev = arcEnd;
                  }
               } else {
                  var currentPt = ParsePoint (pt);
                  seg.Append (GetCommand (ScalePt (prev, hAdvance, lineSpacingFactor), ScalePt (currentPt, hAdvance, lineSpacingFactor)));
                  prev = currentPt;
               }
            }
            glyphLines.Add (seg.ToString ());
            output.Add (seg.ToString ());
         }
         val.Points = [.. glyphLines];
         glyphLines.Clear ();
      }

      // Write final LFONT file
      File.WriteAllLines (lfontFilePath, output);
   }
   static double mAscender,   // Highest Y value in all characters (top of font)
                 mDescender;  // Lowest Y value in all characters (bottom of font)

   // Parses a point from a string formatted as "X,Y".
   static Point2 ParsePoint (string s) {
      var parts = s.Split (',');
      return new Point2 (double.Parse (parts[0]), double.Parse (parts[1]));
   }

   // Scales a point by separate X and Y scale factors
   static Point2 ScalePt (Point2 pt, double scaleX, double scaleY) => new (pt.X * scaleX, pt.Y * scaleY);

   // Parses a point with an optional bulge value from a string.
   static (Point2 Point, double Bulge) ParseArc (string s) {
      var parts = s.Split (',');
      // If third part exists and starts with 'A', parse bulge from it; else bulge = 0
      return (parts.Length == 3 && parts[2].StartsWith ('A'))
          ? (ParsePoint (s), double.Parse (parts[2][1..]))
          : (ParsePoint (s), 0);
   }

   // Scales and converts a <see cref="Point2"/> to a comma-separated string
   static string FormatPt (Point2 pt) => $"{pt.X},{pt.Y}";

   // Returns a compact drawing command string based on the relative position of two points
   static string GetCommand (Point2 a, Point2 b) =>
     (a.X.EQ (b.X), a.Y.EQ (b.Y)) switch {
        (true, _) => $" V{b.Y}",  // Vertical line: same X
        (_, true) => $" H{b.X}",  // Horizontal line: same Y
        _ => $" L{b.X},{b.Y}"     // General line
     };

   // Calculates the midpoint of the arc (not the circle center) defined by a start and end point with a given bulge
   // Midpoint of the arc’s curve, offset perpendicular to the chord
   static Point2 GetArcCenter (Point2 start, Point2 end, double bulge) {
      Vector2 chord = end - start;
      double cl = chord.Length;
      if (cl < Lib.Epsilon) return start; // If the chord length is nearly zero
      var mid = start.Midpoint (end);
      double nx = chord.Y / cl, ny = -chord.X / cl, // Unit normal vectors perpendicular to the chord 
             sagitta = bulge * cl / 2.0;  // Height from the chord to the arc at midpoint
      return new Point2 (mid.X + sagitta * nx, mid.Y + sagitta * ny);
   }

   // Generates a list of points approximating an arc from start to end using the specified bulge
   // returns a list of representing points along the arc.
   static List<Point2> GetArcPoints (Point2 start, Point2 end, double bulge, int segments = 3) {
      Point2 arcCenter = GetArcCenter (start, end, bulge), // Midpoint of the arc (not the circle center)
             center = Get3PCircle (start, arcCenter, end); // Center of the circle defined by the 3 points

      // If the points are collinear or nearly so, return the (start, mid, end)
      if (center.IsNil) return [start, arcCenter, end];
      Vector2 vStart = center - start, vEnd = center - end;
      // Calculate the angle swept by the arc and the angle to the start point
      double centralAng = vStart.AngleTo (vEnd), startAng = center.AngleTo (start);
      List<Point2> pts = [];
      // Interpolate points along the arc using the specified number of segments
      for (int i = 0; i < segments; i++) {
         double t = (double)i / segments;
         double angle = startAng + (bulge > 0 ? +1 : -1) * t * centralAng;
         // Generate a point at the given angle and radius, and round it
         pts.Add (center.Polar (center.DistTo (start), angle).R6 ());
      }
      pts.Add (end);
      return pts;
   }

   //Center of the circle passing through three non-collinear points
   // If the points are collinear, this returns Point2.Nil
   static Point2 Get3PCircle (Point2 a, Point2 b, Point2 c) {
      // Get the midpoints of the sides, and the perpendicular bisector
      // vectors of the two sides
      Point2 mid1 = a.Midpoint (b), mid2 = b.Midpoint (c);
      Vector2 perp1 = (b - a).Perpendicular (), perp2 = (c - b).Perpendicular ();

      // The center is the intersection of these two perpendicular bisectors
      return Nori.Geo.LineXLine (mid1, mid1 + perp1, mid2, mid2 + perp2);
   }

   /// <summary>Represents a single character definition in a font, including geometry, code, and optional reuse data</summary>
   class FontChar {
      /// <summary>Symbol of the character (e.g., "A", "-", " ") that this glyph represents</summary>
      public string Symbol { get; set; } = "";

      /// <summary>Character code for this glyph</summary>
      public int CharCode { get; set; }

      /// <summary>An optional reference to another FontChar whose points should be reused</summary>
      public FontChar? ReuseKey { get; set; }

      /// <summary>List of drawing instructions (typically polylines or arcs) defining the shape of this glyph</summary>
      public List<string> Points { get; set; } = [];

      /// <summary>Total number of polylines used to draw this character, including those from a reused character if present</summary>
      public int Count => Points.Count + (ReuseKey?.Count ?? 0);

      /// <summary>Width of the glyph</summary>
      public double Width { get; set; }
   }
}
#endregion
