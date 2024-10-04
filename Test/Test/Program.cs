// ────── ╔╗ Nori.Test
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Globals.cs ~ Global includes for the Nori.Test project
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

#region class Program ------------------------------------------------------------------------------
/// <summary>Entry point of the Nori.Test application</summary>
class Program {
   // Entry point into Nori.Test.exe
   public static void Main (string[] args)
      => new Program (args).Run ();

   // Implementation -----------------------------------------------------------
   // The constructor gathers all the tests, and also parses the command line arguments
   Program (string[] args) {
      foreach (var arg in args) {
         if (int.TryParse (arg, out int n)) {
            if (n >= 0) mTestID.Add (n);
            else mFixtureID.Add (-n);
         }
      }
   }
   readonly List<int> mTestID = [];       // If non-empty, run only these tests
   readonly List<int> mFixtureID = [];    // If non-empty, run only these fixtures

   // This runs the tests in this assembly
   void Run () {
      var assembly = typeof (Program).Assembly;
      TestRunner.GatherAndRun ([assembly], Filter, ConsoleTestCallback.It);
   }

   // This is the filter used to run specific tests or fixtures
   TestRunner.ETest Filter (Test t) {
      if (mTestID.Count > 0 || mFixtureID.Count > 0)
         return mTestID.Contains (t.Id) || mFixtureID.Contains (t.Fixture.Id) ? TestRunner.ETest.Run : TestRunner.ETest.Hide;
      return TestRunner.ETest.Run;
   }
}
#endregion
