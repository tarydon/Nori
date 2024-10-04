// ────── ╔╗ Nori.Con
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Commands.cs ~ Implements several commands (LineCount, SrcClean)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Reflection;
namespace Nori.Con;

#region class ComputeCoverage ----------------------------------------------------------------------
/// <summary>Run the Nori.Test and compute the coverage</summary>
static class ComputeCoverage {
   public static void Run () {
      // First, run Nori.Test under 'dotnet-coverage' to generate a coverage.xml file in N:/Bin
      var pi = new ProcessStartInfo ("dotnet-coverage.exe", "collect Nori.Test.exe -f xml -o coverage.xml") { WorkingDirectory = @"N:/Bin" };
      Process.Start (pi)!.WaitForExit ();
      // Load the coverage file into N:/Bin/Coverage.xml
      Console.WriteLine ();
      var c = new Coverage ("N:/Bin/coverage.xml");
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
      Console.WriteLine ($"{nFiles,4}{nLines,53}{nComments,9}{tPercent,8}%");

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
      foreach (var file in Directory.EnumerateFiles ("N:/", "*.csproj", SearchOption.AllDirectories)) {
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

#region class SrcClean -----------------------------------------------------------------------------
/// <summary>Helper to clean up source code</summary>
/// Replaces 3-line summary blocks with single line comments
static class SrcClean {
   public static void Run () {
      var (prevDir, nCleaned) = ("", 0);
      foreach (var file in Directory.EnumerateFiles ("N:/", "*.cs", SearchOption.AllDirectories)) {
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
      if (file.StartsWith ("N:/Scratch/")) return true;
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
         lines.Insert (0, "// ────── ╔╗ Nori™");
         lines.Insert (1, "// ╔═╦╦═╦╦╬╣ Copyright © 2025 Arvind");
         lines.Insert (2, $"// ║║║║╬║╔╣║ {name} ~ <<" + "TODO>>");
         lines.Insert (3, "// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────");
         cleaned = true;
      }
      if (cleaned) File.WriteAllLines (file, lines);
      return cleaned;
   }
}
#endregion
