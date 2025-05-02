// ────── ╔╗                                                                                    CON
// ╔═╦╦═╦╦╬╣ Console.cs
// ║║║║╬║╔╣║ Entry point into the Nori.Console program
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Con;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static System.Reflection.BindingFlags;

#region class Program ------------------------------------------------------------------------------
static class Program {
   /// <summary>Entry point into the Nori.Con program</summary>
   [STAThread]
   static void Main (string[] args) {
      if (args.Length == 0) Help ();
      else {
         var mi = typeof (Program).GetMethods (Static | NonPublic | Public)
            .FirstOrDefault (mi => mi.HasAttribute<ConsoleCommandAttribute> () && mi.Name.EqIC (args[0]));
         if (mi == null) Help ();
         else mi.Invoke (null, null);
      }
   }

   /// <summary>Displays usage help</summary>
   [ConsoleCommand]
   static void Help () {
      Console.WriteLine ($$"""
         Nori.Con: Nori console utility for developers.
         Build {{Build}}.

         CLEAN        - Do basic cleanup on all the Nori source files
         COUNT        - Do a line-count on Nori source files
         COVERAGE     - Compute coverage % for Nori.Test
         HELP         - Display this help message
         NEXTID       - Gets the next available test Id
         OPTIMIZE 0/1 - Turns optimization on / off for all Nori projects
         XMLDOC 0/1   - Turns XML documentation on / off for all Nori projects
         """);
      Environment.Exit (0);
   }

   [ConsoleCommand] static void Clean () => SrcClean.Run ();
   [ConsoleCommand] static void Coverage () => ComputeCoverage.Run ();
   [ConsoleCommand] static void Count () => LineCount.Run ();
   [ConsoleCommand] static void NextId () => GetNextId.Run ();

   [ConsoleCommand]
   static void Optimize () {
      string[] args = Environment.GetCommandLineArgs ();
      if (args.Length != 3) Help ();
      if (!int.TryParse (args[2], out int n)) Help ();
      if (n is < 0 or > 1) Help ();
      SetOptimize.Run (n == 1);
   }

   [DoesNotReturn]
   public static void Fatal (string s) {
      Console.WriteLine ();
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine (s);
      Console.ResetColor ();
      Environment.Exit (-1);
   }

   [ConsoleCommand]
   static void XmlDoc () {
      string[] args = Environment.GetCommandLineArgs ();
      if (args.Length != 3) Help ();
      if (!int.TryParse (args[2], out int n)) Help ();
      if (n is < 0 or > 1) Help ();
      SetXmlDoc.Run (n == 1);
   }

   // Placeholder for putting in some test code and running it
   [ConsoleCommand]
   static void TestHook () {
      Random r = new (3);
      int count = 400000000;
      Bound2 b = new (0.25, 0.25, 0.75, 0.75);
      var xfm = Matrix2.Rotation (new Point2 (0.5, 0.5), 45.D2R ());
      {
         List<Point2> pts = [];
         Stopwatch sw = Stopwatch.StartNew ();
         for (int i = 0; i < count; i++)
            pts.Add (new (r.NextDouble (), r.NextDouble ()));

         int fraction = pts.Select (a => a * xfm).Count (b.Contains);
         sw.Stop ();
         Console.WriteLine ($"Ratio: {(double)fraction / count}");
         Console.WriteLine ($"Point2: {sw.Elapsed.TotalMilliseconds}");
      }
      {
         List<Vec2F> pts2 = [];
         var sw = Stopwatch.StartNew ();
         for (int i = 0; i < count; i++)
            pts2.Add (new ((float)r.NextDouble (), (float)r.NextDouble ()));

         var fraction = pts2.Select (a => a * xfm).Count (b.Contains);
         sw.Stop ();
         Console.WriteLine ($"Ratio: {(double)fraction / count}");
         Console.WriteLine ($"Vec2F: {sw.Elapsed.TotalMilliseconds}");
      }

      Half h = (Half)1000.82;
      Console.WriteLine ((double)h);
   }

   static int Build = 2;
}
#endregion

#region [ConsoleCommand] attribute -----------------------------------------------------------------
/// <summary>[ConsoleCommand] attribute is used to decorate methods that should be exposed as commands</summary>
[AttributeUsage (AttributeTargets.Method)]
class ConsoleCommandAttribute : Attribute { }
#endregion
