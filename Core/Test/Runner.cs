// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Runner.cs ~ Implements the TestRunner class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Console;
using static ConsoleColor;

#region ITestCallback ------------------------------------------------------------------------------
/// <summary>Interface to be implemented by test runners</summary>
public interface ITestCallback {
   /// <summary>Called when the tests begin</summary>
   void Begin (int cFixtures, int cTests);
   /// <summary>Called when we enter a new fixture</summary>
   void StartFixture (Fixture fixture);
   /// <summary>Called before we start executing a test</summary>
   /// This is always followed by one of TestPassed, TestSkipped, TestCrashed or TestFailed
   void StartTest (Test test);
   /// <summary>Called when the test passes (the previous call would be a StartTest)</summary>
   void TestPassed (Test test);
   /// <summary>Called when a test is skipped (the previous call would be a StartTest)</summary>
   /// Note that even when a test is skipped, we will have the StartTest..TestSkipped sequence
   void TestSkipped (Test test);
   /// <summary>Called when a test crashes (the exception thrown by the test is passed as parameter)</summary>
   void TestCrashed (Test test, Exception ex);
   /// <summary>Called when a test fails (the TestException thrown by the test is passed as parameter)</summary>
   void TestFailed (Test test, TestException ex);
   /// <summary>Called after any of the TestXXX methods (every StartTest is followed eventually by an EndTest)</summary>
   /// <param name="test">The test that just Passed/Skipped/Crashed/Failed</param>
   /// <param name="cDone">How many tests are done in total</param>
   /// <param name="cTotal">How many tests are there in total</param>
   /// <param name="elapsed">Total elapsed time, since testing began</param>
   void EndTest (Test test, int cDone, int cTotal, TimeSpan elapsed);
   /// <summary>Called after all the tests are done</summary>
   /// <param name="cTotal">Total number of tests</param>
   /// <param name="cFailed">How many of them failed?</param>
   /// <param name="cCrashed">How many of them crashed?</param>
   /// <param name="cSkipped">How many of them were skipped?</param>
   /// <param name="elapsed">Total time to run all the tests</param>
   void End (int cTotal, int cFailed, int cCrashed, int cSkipped, TimeSpan elapsed);
}
#endregion

#region ConsoleTestCallback ------------------------------------------------------------------------
/// <summary>Implementation of ITestCallback that echoes to the console</summary>
public partial class ConsoleTestCallback : ITestCallback {
   // ITestCallback implementation ---------------------------------------------
   public void Begin (int cFixtures, int cTests) => WriteLine ($"{cFixtures} fixtures, {cTests} tests");
   public void StartTest (Test test) => Write ($" {test.Id}. {test.Description} ");
   public void TestPassed (Test test) => WriteRight (Green, "pass");
   public void TestSkipped (Test test) => WriteRight (Cyan, "SKIP");

   public void StartFixture (Fixture fixture) {
      ForegroundColor = White; BackgroundColor = DarkBlue;
      WriteLine ($"{fixture.Id}. {fixture.Description} [{fixture.Module}]".PadRight (WindowWidth - 1));
      ResetColor ();
   }

   public void TestFailed (Test test, TestException ex) {
      WriteRight (Yellow, "FAIL");
      ForegroundColor = Yellow; WriteLine (ex.Message); ResetColor ();
   }

   public void TestCrashed (Test test, Exception ex) {
      WriteRight (Red, "CRASH");
      ForegroundColor = Yellow; WriteLine (ex.Message); ResetColor ();
   }

   public void EndTest (Test test, int cDone, int cTotal, TimeSpan elapsed)
      => Title = $"Nori.Test {cDone}/{cTotal}. {Math.Round (elapsed.TotalSeconds)} seconds";

   public void End (int cTotal, int cFailed, int cCrashed, int cSkipped, TimeSpan elapsed) {
      ForegroundColor = White; BackgroundColor = DarkBlue;
      string s;
      if (cFailed + cCrashed + cSkipped == 0) s = $"All {cTotal} tests passed";
      else s = $"{cTotal} tests, {cFailed} failed, {cCrashed} crashed, {cSkipped} skipped";
      s += $", {Math.Round (elapsed.TotalSeconds, 1)} seconds";
      Write (s.PadRight (WindowWidth - 1));
      ResetColor ();
      WriteLine ();
   }

   // Helpers ------------------------------------------------------------------
   static void WriteRight (ConsoleColor color, string text) {
      Write (new string ('.', WindowWidth - CursorLeft - text.Length - 1));
      ForegroundColor = color; Write (text);
      ResetColor ();
      WriteLine ();
   }

   public readonly static ConsoleTestCallback It = new ();
}
#endregion

#region class TestRunner ---------------------------------------------------------------------------
/// <summary>This implements the generalized TestRunner interface</summary>
/// The TestRunner gathers and runs the tests. It uses a callback to report progress when the
/// tests are running
public static class TestRunner {
   // Methods ------------------------------------------------------------------
   /// <summary>Gather all the tests from the given set of assemblies</summary>
   public static List<Test> Gather (Assembly[] assemblies)
      => Gather (assemblies, t => ETest.Run);

   /// <summary>Gathers a filtered set of tests from the given set of assemblies</summary>
   /// The given filter function should return one of Run / Skip / Hide for each test that is
   /// passed in. Tests that are marked Hide are not even displayed / echoed, while tests that
   /// are tagged as Skip are output to the runner callback, but with the test status as 'Skipped'
   static List<Test> Gather (Assembly[] assemblies, Func<Test, ETest> filter) {
      // First, gather all the tests, filtering them through the test filter as needed
      List<Test> tests = [];
      foreach (var type in assemblies.SelectMany (a => a.GetTypes ())) {
         var attr = type.GetCustomAttribute<FixtureAttribute> ();
         if (attr != null && !attr.Skip) {
            foreach (var t in new Fixture (type, attr).Tests) {
               switch (filter (t)) {
                  case ETest.Hide: continue;
                  case ETest.Skip: t.Skip = true; break;
               }
               tests.Add (t);
            }
         }
      }
      tests = [.. tests.OrderBy (a => a.Fixture.Id)];
      return tests;
   }

   /// <summary>Gathers tests from the given assemblies and runs them (this is the simple-use-case function)</summary>
   /// <param name="assemblies">The set of assemblies to search for Test fixtures</param>
   /// <param name="filter">The filter function that narrows down the set of tests to run</param>
   /// <param name="echo">The callback that should be used to report progress while the tests are running</param>
   public static void GatherAndRun (Assembly[] assemblies, Func<Test, ETest> filter, ITestCallback echo)
      => Run (Gather (assemblies, filter), echo);

   /// <summary>This is used to run a set of tests that we have gathered</summary>
   /// <param name="tests">The set of tests to run</param>
   /// <param name="echo">The callback that should be used to report progress</param>
   public static void Run (IReadOnlyList<Test> tests, ITestCallback echo) {
      // Now, run the the tests
      Lib.Testing = true;
      DateTime start = DateTime.Now;
      Fixture? fxLast = null;
      object? fxObject = null;
      int cTests = tests.Count, cFixtures = tests.Select (a => a.Fixture).Distinct ().Count ();
      int cFailed = 0, cSkipped = 0, cCrashed = 0, cDone = 0;
      echo.Begin (cFixtures, cTests);
      foreach (var test in tests) {
         var fixture = test.Fixture;
         if (fixture != fxLast) {
            fxLast = fixture;
            if (fxObject is IDisposable disp1) disp1.Dispose ();
            echo.StartFixture (fixture);
            fxObject = fixture.Constructor.Invoke (null);
         }
         echo.StartTest (test);
         if (test.Skip) {
            echo.TestSkipped (test); cSkipped++;
         } else {
            Exception? except = null;
            try {
               test.Method.Invoke (fxObject, null);
            } catch (Exception ex) {
               if (ex is TargetInvocationException te) except = te.InnerException ?? te;
               else except = ex;
            }
            switch (except) {
               case TestException te: echo.TestFailed (test, te); cFailed++; break;
               case Exception ex: echo.TestCrashed (test, ex); cCrashed++; break;
               default: echo.TestPassed (test); break;
            }
         }
         echo.EndTest (test, ++cDone, cTests, DateTime.Now - start);
      }
      if (fxObject is IDisposable disp) disp.Dispose ();
      echo.End (cTests, cFailed, cCrashed, cSkipped, DateTime.Now - start);
   }

   /// <summary>Given a Coverage object with all files, this marks only the files of interest to Nori testing</summary>
   /// When we run all the tests with the coverage analyzer (dotnet-coverage), it gathers coverage
   /// information for all the code. However, some of that code is autogenerated (files with .g.cs)
   /// and needs to be excluded. Further, the actual test source files (in N:/Test) should also be
   /// excluded. This routine picks up only the files we are interested in, and uses the 
   /// Coverage.SetFilesOfInterest to focus on only these files
   public static void SetNoriFiles (Coverage c) {
      var files = c.Files.Where (Passes).ToList ();
      c.SetFilesOfInterest (files);

      static bool Passes (string file) {
         if (file.EndsWith (".g.cs")) return false;
         file = file.Replace ('\\', '/');
         if (file.StartsWith ($"{Lib.DevRoot}/Test/")) return false;
         return true;
      }
   }

   // Nested types -------------------------------------------------------------
   /// <summary>Values returned by the test-filter</summary>
   public enum ETest { Run, Skip, Hide };
}
#endregion
