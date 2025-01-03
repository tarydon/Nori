// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Lib.cs
// ║║║║╬║╔╣║ Implements the Lib module class that has a number of global functions
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Lib ----------------------------------------------------------------------------------
public static class Lib {
   // Constants ----------------------------------------------------------------
   /// <summary>Epsilon = 1e-6</summary>
   public const double Epsilon = 1e-6;
   /// <summary>PI = 180 degrees, in radians</summary>
   public const double PI = Math.PI;
   /// <summary>TwoPI = 360 degrees, in radians</summary>
   public const double TwoPI = 2 * Math.PI;
   /// <summary>HalfPI = 90 degrees, in radians</summary>
   public const double HalfPI = Math.PI / 2;
   /// <summary>QuarterPI = 45 degrees, in radians</summary>
   public const double QuarterPI = Math.PI / 4;

   // Properties ---------------------------------------------------------------
   /// <summary>The root of Nori projects on developer machines</summary>
   public const string DevRoot = "N:";

   /// <summary>Are we in 'testing' mode?</summary>
   public static bool Testing { get; set; }

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the cos-inverse of the given value</summary>
   /// This clamps values beyond the range -1 .. +1 to lie within that range
   public static double Acos (double f) => Math.Acos (f.Clamp (-1, 1));

   /// <summary>Adds a namespace to the list of 'known namespaces'</summary>
   /// We use this to when searching for a type by name. If only the core name of the type
   /// is specified, these namespaces are prepended to that name to try to form a match
   public static void AddNamespace (string nameSpace) => mNamespaces.Add ($"{nameSpace}.");
   internal static List<string> mNamespaces = ["Nori"];

   /// <summary>Returns the number of steps required to rasterize and arc with a given tolerance</summary>
   /// <param name="radius">The radius of the arc</param>
   /// <param name="angSpan">The angular span of the arc (can be +ve or -ve)</param>
   /// <param name="tolerance">The error tolerance (chordal deviation)</param>
   public static int GetArcSteps (double radius, double angSpan, double tolerance) {
      tolerance = tolerance.Clamp (radius * 0.0001, radius * 0.9999);
      double angStep = 2 * Acos ((radius - tolerance) / radius);
      return Max ((int)Ceiling (Abs (angSpan) / angStep), 1);
   }

   /// <summary>Returns the full-path-filename of a 'local' file (relative to startup EXE)</summary>
   static public string GetLocalFile (string file) {
      if (sCodeBase == null) {
         string? location = GetLocation (Assembly.GetEntryAssembly ())
            ?? GetLocation (Assembly.GetExecutingAssembly ())
            ?? GetLocation (Assembly.GetCallingAssembly ());
         sCodeBase = Path.GetDirectoryName (location) ?? "";
      }
      return Path.GetFullPath (Path.Combine (sCodeBase, file));

      // Helpers ..............................
      static string? GetLocation (Assembly? asm) {
         if (asm == null) return null;
         return new Uri (asm.Location).LocalPath;
      }
   }
   static string? sCodeBase;

   /// <summary>Checks if a reference is a 'null reference'</summary>
   public static bool IsNull<T> (ref readonly T source) => Unsafe.IsNullRef (in source);

   /// <summary>Returns the 'nice name' for a type (human readable name like 'int')</summary>
   public static string NiceName (Type type) {
      if (!sNiceNames.TryGetValue (type, out var s)) {
         s = type.FullName!;
         foreach (var ns in mNamespaces)
            if (s.StartsWith (ns)) { s = s[(ns.Length + 1)..]; break; }
         sNiceNames[type] = s;
      }
      return s;
   }
   static Dictionary<Type, string> sNiceNames = new () {
      [typeof (int)] = "int", [typeof (float)] = "float", [typeof (double)] = "double",
      [typeof (void)] = "void", [typeof (short)] = "short", [typeof (uint)] = "uint",
      [typeof (ushort)] = "ushort", [typeof (byte)] = "byte", [typeof (bool)] = "bool",
      [typeof (long)] = "long", [typeof (ulong)] = "ulong", [typeof (sbyte)] = "sbyte",
      [typeof (string)] = "string", [typeof (char)] = "char"
   };

   /// <summary>Normalizes an angle (in radians) to lie in the half open range (-PI .. PI]</summary>
   public static double NormalizeAngle (double fAng) {
      fAng %= TwoPI;
      if (fAng > PI) fAng -= TwoPI;
      if (fAng <= -PI) fAng += TwoPI;
      return fAng;
   }

   /// <summary>Print a string with a given console color</summary>
   static public void Print (string text, ConsoleColor color) {
      Console.ForegroundColor = color;
      Console.Write (text);
      Console.ResetColor ();
   }

   /// <summary>Prints a string with a given console color, followed by a newline</summary>
   static public void Println (string text, ConsoleColor color)
      => Print ($"{text}\n", color);

   /// <summary>Solves a system of 2 linear equations with 2 unknowns</summary>
   /// Ax + By + C = 0
   /// Dx + Ey + F = 0
   public static bool SolveLinearPair (double A, double B, double C, double D, double E, double F, out double x, out double y) {
      double fHypot = A * E - D * B;
      if (fHypot.IsZero ()) { x = y = 0; return false; }
      x = (B * F - E * C) / fHypot; y = (D * C - A * F) / fHypot;
      return true;
   }

   /// <summary>Orders two comparable so a is always less than or equal to b</summary>
   public static void Sort<T> (ref T a, ref T b) where T : IComparable<T> {
      if (a.CompareTo (b) > 0) (a, b) = (b, a);
   }
}
#endregion
