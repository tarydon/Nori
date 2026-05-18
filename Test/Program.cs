// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Entry point into the Nori.Test application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

#region class Program ------------------------------------------------------------------------------
/// <summary>Entry point of the Nori.Test application</summary>
class Program {
   // Entry point into Nori.Test.exe
   [STAThread]
   public static void Main (string[] args)
      => new Program (args).Run ();

   // Implementation -----------------------------------------------------------
   // The constructor gathers all the tests, and also parses the command line arguments
   Program (string[] args) {
      Lib.Init ();
      Lux2.Init (); 
      Lux.CreatePanel (true);
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
      var cit = ConsoleTestCallback.It;
      var args = Environment.GetCommandLineArgs ().Select (a => a.ToUpper ()).ToList ();
      TestRunner.StopOnFail = args.Contains ("-STOP");
      TestRunner.RunDiff = args.Contains ("-DIFF");
      TestRunner.OnlyFailed = args.Contains ("-FAILED");
      string fail = NT.File ("failed.txt");
      if (File.Exists (fail)) {
         // If the 'failed.txt' exists, load it in
         foreach (var n in File.ReadAllLines (fail).Select (a => a.ToInt ()))
            TestRunner.FailList.Add (n);
      }
      TestRunner.GatherAndRun ([assembly], Filter, cit);
      File.WriteAllLines (fail, TestRunner.FailList.Select (a => a.ToString ()));
   }

   // This is the filter used to run specific tests or fixtures
   TestRunner.ETest Filter (Test t) {
      if (TestRunner.OnlyFailed && !TestRunner.FailList.Contains (t.Id))
         return TestRunner.ETest.Hide;
      if (mTestID.Count > 0 || mFixtureID.Count > 0)
         return mTestID.Contains (t.Id) || mFixtureID.Contains (t.Fixture.Id) ? TestRunner.ETest.Run : TestRunner.ETest.Hide;
      return TestRunner.ETest.Run;
   }
}
#endregion
