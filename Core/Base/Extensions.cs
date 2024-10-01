// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Extensions.cs ~ Various extension methods for common system types
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Extensions ---------------------------------------------------------------------------
public static class Extensions {
   /// <summary>Clamps the given double to lie within min..max (inclusive)</summary>
   public static double Clamp (this double a, double min, double max) => a < min ? min : (a > max ? max : a);
   /// <summary>Clamps the given double to the range 0..1</summary>
   public static double Clamp (this double a) => a < 0 ? 0 : (a > 1 ? 1 : a);

   /// <summary>Clamps the given float to lie within min..max (inclusive)</summary>
   public static float Clamp (this float a, float min, float max) => a < min ? min : (a > max ? max : a);

   /// <summary>Convert an angle from degrees to radians</summary>
   public static double D2R (this double f) => f * RadiansPerDegree;
   /// <summary>Convert an angle from degrees to radians</summary>
   public static double D2R (this int n) => n * RadiansPerDegree;

   /// <summary>Compare two doubles for equality to within 1e-6</summary>
   public static bool EQ (this double a, double b) => Abs (a - b) < Lib.Epsilon;
   /// <summary>Compares two floats for equality to within 1e-5</summary>
   public static bool EQ (this float a, float b) => Abs (a - b) < 1e-5;

   /// <summary>Performs an action on each element of a sequence</summary>
   public static void ForEach<T> (this IEnumerable<T> seq, Action<T> action) {
      foreach (var elem in seq) action (elem);
   }

   /// <summary>Gets a value from a dictionary, or adds a new one (synthesized by the maker function)</summary>
   public static U Get<T, U> (this IDictionary<T, U> dict, T key, Func<T, U> maker) {
      if (!dict.TryGetValue (key, out var value)) 
         dict[key] = value = maker (key); 
      return value;
   }

   /// <summary>Check if a double is equal to 0, to within Epsilon</summary>
   public static bool IsZero (this double a) => Abs (a) < Lib.Epsilon;

   /// <summary>Returns a Half rounded off to 5 decimal places</summary>
   public static float R3 (this Half f) => (float)Math.Round ((float)f, 3);
   /// <summary>Returns a float rounded off to 5 decimal places</summary>
   public static float R5 (this float f) => (float)Math.Round (f, 5);
   /// <summary>Returns a double rounded off to 6 decimal places</summary>
   public static double R6 (this double f) => Math.Round (f, 6);

   /// <summary>Convert an angle from radians to degrees</summary>
   public static double R2D (this double f) => f * DegreesPerRadian;

   const double DegreesPerRadian = 180 / PI;
   const double RadiansPerDegree = PI / 180;
}
#endregion
