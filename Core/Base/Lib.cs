// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Lib.cs
// ║║║║╬║╔╣║ Implements the Lib module class that has a number of global functions
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Threading;
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
   /// <summary>The constant square-root-of-2</summary>
   public const double Root2 = 1.4142135623730950488016887242097;

   // Properties ---------------------------------------------------------------
   /// <summary>The list of known assemblies</summary>
   public static IEnumerable<Assembly> Assemblies => mAssemblies;
   static HashSet<Assembly> mAssemblies = [];

   /// <summary>The root of Nori projects on developer machines</summary>
   public static string DevRoot {
      get {
         if (mDevRoot == null) {
            mDevRoot = Environment.GetEnvironmentVariable ("NORIROOT");
            if (mDevRoot.IsBlank ()) mDevRoot = "N:";
         }
         return mDevRoot;
      }
   }
   static string? mDevRoot;

   /// <summary>The list of 'well-known' namespaces</summary>
   /// When writing types out, or getting the friendly names of types (using
   /// Lib.GetNiceName, these namespace prefixes are removed. So we will get
   /// "Point2" rather than "Nori.Point2"
   public static IEnumerable<string> Namespaces => mNamespaces;
   static HashSet<string> mNamespaces = [];

   /// <summary>Are we in 'testing' mode?</summary>
   public static bool Testing { get; set; }

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the cos-inverse of the given value</summary>
   /// This clamps values beyond the range -1 .. +1 to lie within that range
   public static double Acos (double f) => Math.Acos (f.Clamp (-1, 1));

   /// <summary>Add an assembly to the list of 'known' assemblies</summary>
   /// These are the assemblies searched when we try to get a type by name
   public static void AddAssembly (Assembly assy) => mAssemblies.Add (assy);

   /// <summary>Adds a namespace to the list of 'known namespaces'</summary>
   /// We use this to when searching for a type by name. If only the core name of the type
   /// is specified, these namespaces are prepended to that name to try to form a match
   public static void AddNamespace (string nameSpace) => mNamespaces.Add ($"{nameSpace.TrimEnd ('.')}.");

   public static void AddMetadata (string[] metadata) => AuType.AddTactics (metadata);

   /// <summary>Checks a condition, and throws an exception in debug mode</summary>
   /// In release mode, this just returns the condition quietly
   public static bool Check (bool condition, string message) {
      if (!condition) throw new Exception (message);
      return condition;
   }

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
   public static string GetLocalFile (string file) {
      if (sCodeBase == null) {
         string? location = GetLocation (Assembly.GetEntryAssembly ())
            ?? GetLocation (Assembly.GetExecutingAssembly ())
            ?? GetLocation (Assembly.GetCallingAssembly ());
         sCodeBase = Path.GetDirectoryName (location) ?? "";
      }
      return Path.GetFullPath (Path.Combine (sCodeBase, file));

      // Helpers ..............................
      static string? GetLocation (Assembly? asm)
         => asm == null ? null : new Uri (asm.Location).LocalPath;
   }
   static string? sCodeBase;

   /// <summary>This should be called to initialize Nori.Core before use</summary>
   public static void Init () {
      if (!sInited) {
         sInited = true;
         var file = GetLocalFile ("Nori.wad");
         if (File.Exists (file)) Register (new ZipStmLocator ("nori:", file));
         else Register (new FileStmLocator ("nori:", $"{DevRoot}/Wad/"));
         AddAssembly (Assembly.GetExecutingAssembly ());
         AddNamespace ("Nori"); AddNamespace ("System"); AddNamespace ("System.Collections.Generic");
      }
   }
   static bool sInited;

   /// <summary>Checks if a reference is a 'null reference'</summary>
   public static bool IsNull<T> (ref readonly T source) => Unsafe.IsNullRef (in source);

   /// <summary>Returns the 'nice name' for a type (human readable name like 'int')</summary>
   public static string NiceName (Type type) {
      if (!sNiceNames.TryGetValue (type, out var s)) {
         if (type.IsGenericType) {
            s = type.GetGenericTypeDefinition ().FullName!;
            s = s[..s.IndexOf ('`')];
            s += $"<{type.GetGenericArguments ().Select (NiceName).ToCSV ()}>";
         } else
            s = type.FullName!;
         foreach (var ns in mNamespaces.OrderByDescending (a => a.Length))
            if (s.StartsWith (ns)) { s = s[ns.Length..]; break; }
         sNiceNames[type] = s;
      }
      return s;
   }
   static readonly Dictionary<Type, string> sNiceNames = new () {
      [typeof (int)] = "int", [typeof (float)] = "float", [typeof (double)] = "double",
      [typeof (void)] = "void", [typeof (short)] = "short", [typeof (uint)] = "uint",
      [typeof (ushort)] = "ushort", [typeof (byte)] = "byte", [typeof (bool)] = "bool",
      [typeof (long)] = "long", [typeof (ulong)] = "ulong", [typeof (sbyte)] = "sbyte",
      [typeof (string)] = "string", [typeof (char)] = "char", [typeof (object)] = "object"
   };

   /// <summary>Normalizes an angle (in radians) to lie in the half open range (-PI .. PI]</summary>
   public static double NormalizeAngle (double fAng) {
      fAng %= TwoPI;
      if (fAng > PI) fAng -= TwoPI;
      if (fAng <= -PI) fAng += TwoPI;
      return fAng;
   }

   /// <summary>Called to open a stream using the IStmLocator service</summary>
   /// For example, a stream can be opened from the wad using syntax like
   /// Sys.OpenRead ("nori:GL/point.frag");
   public static Stream OpenRead (string name) =>
               sLocators.Select (locator => locator.Open (name)).FirstOrDefault (stm => stm != null)
               ?? throw new Exception ($"Could not open {name}");

   /// <summary>Calls a function asynchronously on the current thread</summary>
   public static void Post (Action act) {
      var sc = SynchronizationContext.Current;
      if (sc != null) sc.Post (_ => act (), null);
      else act ();
   }

   /// <summary>Print a string with a given console color</summary>
   public static void Print (string text, ConsoleColor color) {
      Console.ForegroundColor = color;
      Console.Write (text);
      Console.ResetColor ();
   }

   /// <summary>Prints a string with a given console color, followed by a newline</summary>
   public static void Println (string text, ConsoleColor color)
      => Print ($"{text}\n", color);

   /// <summary>Reads all the bytes from a stream opened by the IStmLocator service</summary>
   public static byte[] ReadBytes (string file) {
      using var stm = OpenRead (file);
      byte[] data = new byte[stm.Length];
      stm.ReadExactly (data);
      return data;
   }

   /// <summary>Reads text from a stream opened by the IStmLocator service</summary>
   public static string ReadText (string file) {
      using var stm = OpenRead (file);
      using var reader = new StreamReader (stm);
      return reader.ReadToEnd ().ReplaceLineEndings ("\n");
   }

   /// <summary>Reads a set of lines from a stream opened by the IStmLocator service</summary>
   public static string[] ReadLines (string file)
      => ReadText (file).Split ('\n');

   /// <summary>Register a stream locator</summary>
   public static void Register (IStmLocator locator) => sLocators.Add (locator);
   static readonly List<IStmLocator> sLocators = [];

   /// <summary>Sets a double, and returns true if it has changed</summary>
   public static bool Set (ref double f0, double f1) { if (f0.EQ (f1)) return false; f0 = f1; return true; }
   /// <summary>Sets a float, and returns true if it has changed</summary>
   public static bool Set (ref float f0, float f1) { if (f0.EQ (f1)) return false; f0 = f1; return true; }
   /// <summary>Sets an int, and returns true if it has changed</summary>
   public static bool Set (ref int n0, int n1) { if (n0 == n1) return false; n0 = n1; return true; }
   /// <summary>Sets a bool, and returns true if it has changed</summary>
   public static bool Set (ref bool b0, bool b1) { if (b0 == b1) return false; b0 = b1; return true; }
   /// <summary>Sets a value, and returns true if it has changed (for any IEQuable)</summary>
   public static bool Set<T> (ref T t0, T t1) where T : struct, IEQuable<T> { if (t0.EQ (t1)) return false; t0 = t1; return true; }
   /// <summary>Sets a value and returns true if it has changed (for any reference type)</summary>
   public static bool SetR<T> (ref T t0, T t1) where T : class { if (t0 == t1) return false; t0 = t1; return true; }
   /// <summary>Sets a value and returns true if it has changed (for any enumeration type)</summary>
   public static bool SetE<T> (ref T t0, T t1) where T : Enum { if (t0.Equals (t1)) return false; t0 = t1; return true; }

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

   /// <summary>Outputs a string representation of the object to our tracer</summary>
   public static void Trace (object obj) => Tracer.Invoke ($"{obj}");

   /// <summary>Set this to point to your own trace handler</summary>
   /// By default, this just outputs to Debug.Write, but you could set this to
   /// something like Console.Write or TraceVN.Print
   public static Action<string> Tracer = s => Debug.Write (s);
}
#endregion
