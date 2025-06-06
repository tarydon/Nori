// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Extensions.cs
// ║║║║╬║╔╣║ Various extension methods for common system types
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#region class Extensions ---------------------------------------------------------------------------
/// <summary>Extension functions on various standard types</summary>
public static class Extensions {
   /// <summary>Interpolates using a given lie f between two doubles a and b</summary>
   public static double Along (this double f, double a, double b)
      => a + (b - a) * f;
   /// <summary>Interpolates using a given lie f between two Point2 a and b</summary>
   public static Point2 Along (this double f, Point2 a, Point2 b)
      => new (f.Along (a.X, b.X), f.Along (a.Y, b.Y));
   /// <summary>Interpolates using a given lie f between two Point3 a and b</summary>
   public static Point3 Along (this double f, Point3 a, Point3 b)
      => new (f.Along (a.X, b.X), f.Along (a.Y, b.Y), f.Along (a.Z, b.Z));

   /// <summary>Gets the underlying T array for an immutablearray</summary>
   public static T[] AsArray<T> (this ImmutableArray<T> iarray)
      => ImmutableCollectionsMarshal.AsArray (iarray)!;

   /// <summary>Create an ImmutableArray view over an array (no copying)</summary>
   public static ImmutableArray<T> AsIArray<T> (this T[] array)
      => ImmutableCollectionsMarshal.AsImmutableArray (array);

   /// <summary>Gets a Span&lt;T&gt; view over the data in a list</summary>
   /// Note that you should not add or remove items from the list while the Span is being used.
   public static ReadOnlySpan<T> AsSpan<T> (this List<T> list) => CollectionsMarshal.AsSpan (list);

   /// <summary>Clamps the given double to lie within min..max (inclusive)</summary>
   public static double Clamp (this double a, double min, double max) => a < min ? min : (a > max ? max : a);
   /// <summary>Clamps the given double to the range 0..1</summary>
   public static double Clamp (this double a) => a < 0 ? 0 : (a > 1 ? 1 : a);
   /// <summary>Clamps the given float to lie within min..max (inclusive)</summary>
   public static float Clamp (this float a, float min, float max) => a < min ? min : (a > max ? max : a);
   /// <summary>Clamps the given float to the range 0..1</summary>
   public static float Clamp (this float a) => a < 0 ? 0 : (a > 1 ? 1 : a);
   /// <summary>Clamps a given integer to lie within min..max (inclusive)</summary>
   public static int Clamp (this int a, int min, int max) => a < min ? min : (a > max ? max : a);

   /// <summary>Get the description</summary>
   public static string Description (this Exception e) => $"{Lib.NiceName (e.GetType ())}: {e.Message}";

   /// <summary>Convert an angle from degrees to radians</summary>
   public static double D2R (this double f) => f * RadiansPerDegree;
   /// <summary>Convert an angle from degrees to radians</summary>
   public static double D2R (this int n) => n * RadiansPerDegree;
   const double RadiansPerDegree = PI / 180;

   /// <summary>Compare two doubles for equality to within 1e-6</summary>
   public static bool EQ (this double a, double b) => Abs (a - b) < 1e-6;
   /// <summary>Compare two doubles for equality with the given epsilon</summary>
   public static bool EQ (this double a, double b, double epsilon) => Abs (a - b) < epsilon;
   /// <summary>Compare two floats for equality to within 1e-5</summary>
   public static bool EQ (this float a, float b) => Abs (a - b) < 1e-5;
   /// <summary>Compare two halfs for equality to within 1e-4</summary>
   public static bool EQ (this Half a, Half b) => Abs ((float)a - (float)b) < 1e-3;

   /// <summary>Compares two string for equality, ignoring case</summary>
   public static bool EqIC (this string a, string b) => a.Equals (b, StringComparison.OrdinalIgnoreCase);

   /// <summary>Returns all elements of a sequence _except_ those that match the specified predicate</summary>
   public static IEnumerable<T> Except<T> (this IEnumerable<T> sequence, Predicate<T> excluder) {
      foreach (var elem in sequence)
         if (!excluder (elem)) yield return elem;
   }

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

   /// <summary>Checks if a member (type / method / property / field) has the given custom attribute attached</summary>
   public static bool HasAttribute<T> (this MemberInfo mi) where T : Attribute => mi.GetCustomAttribute<T> () != null;

   /// <summary>Returns true if a string is null, empty or whitespace</summary>
   public static bool IsBlank (this string? s) => string.IsNullOrWhiteSpace (s);

   /// <summary>Checks if a double is NaN</summary>
   public static bool IsNaN (this double a) => double.IsNaN (a);
   /// <summary>Checks if a float is NaN</summary>
   public static bool IsNaN (this float a) => float.IsNaN (a);

   /// <summary>Checks if a given double is zero to within 1e-6</summary>
   public static bool IsZero (this double a) => Abs (a) < 1e-6;
   /// <summary>Checks if a given float is zero to within 1e-5</summary>
   public static bool IsZero (this float a) => Abs (a) < 1e-5;

   /// <summary>
   /// Returns the index of the element that returns the highest value
   /// </summary>
   public static int MaxIndexBy<T> (this IEnumerable<T> seq, Func<T, double> func)
      => seq.MinIndexBy (a => -func (a));

   /// <summary>Computes the index of the 'minimum' value in a sequence (or -1 if the sequence is empty)</summary>
   public static int MinIndex<T> (this IReadOnlyList<T> seq) where T : IComparable {
      if (seq.Count == 0) return -1;
      int index = 0; T minimum = seq[0];
      for (int i = 1; i < seq.Count; i++)
         if (seq[i].CompareTo (minimum) < 0)
            (index, minimum) = (i, seq[i]);
      return index;
   }

   // Returns the index of the element that returns the lowest value after being passed
   // through the evaluator function func. If the sequence is empty, this returns -1
   public static int MinIndexBy<T> (this IEnumerable<T> seq, Func<T, double> func) {
      double min = double.MaxValue; int minIdx = -1, i = -1;
      foreach (var elem in seq) {
         double dist = func.Invoke (elem); i++;
         if (dist < min) (min, minIdx) = (dist, i);
      }
      return minIdx;
   }

   /// <summary>Returns a random bool</summary>
   public static bool NextBool (this Random r) => r.Next (10000) < 5000;

   /// <summary>Given a sequence, returns a 'numbered' version where each item is tagged with an ordinal (starting from 0)</summary>
   public static IEnumerable<(int No, T Data)> Numbered<T> (this IEnumerable<T> seq) {
      int c = 0;
      foreach (var elem in seq) yield return (c++, elem);
   }

   /// <summary>Convert an angle from radians to degrees</summary>
   public static double R2D (this double f) => f * DegreesPerRadian;
   const double DegreesPerRadian = 180 / PI;

   public static float R2D (this float f) => (float)(f * DegreesPerRadian);

   /// <summary>Returns a Half rounded off to 5 decimal places</summary>
   public static float R3 (this Half f) => (float)Math.Round ((float)f, 3);
   /// <summary>Returns a float rounded off to 5 decimal places</summary>
   public static float R5 (this float f) => (float)Math.Round (f, 5);
   /// <summary>Returns a double rounded off to 6 decimal places</summary>
   public static double R6 (this double f) => Math.Round (f, 6);

   /// <summary>Removes the last element from a List (and returns it)</summary>
   public static T RemoveLast<T> (this List<T> list) {
      T elem = list[^1]; list.RemoveAt (list.Count - 1);
      return elem;
   }

   /// <summary>'Rolls' a list, treating it as a circular list, starting with element N</summary>
   /// The element N is returned first, then N+1 and so on, until we finish with
   /// the element N-1. Thus, [1,2,3,4,5].Roll (2) will return [3,4,5,1,2].
   /// You can also pass in a negative index, so [1,2,3,4,5].Roll (-1) will return
   /// [5,1,2,3,4]
   public static IEnumerable<T> Roll<T> (this IReadOnlyList<T> list, int n) {
      for (int i = 0, count = list.Count; i < count; i++)
         yield return list[(i + n).Wrap (count)];
   }

   /// <summary>Rounds a double to the given number of digits</summary>
   public static double Round (this double a, int digits) => Math.Round (a, digits);
   /// <summary>Rounds a float to the given number of digits</summary>
   public static double Round (this float a, int digits) => Math.Round (a, digits);

   /// <summary>Rounds up the given integer to the next multiple of the given chunk size</summary>
   public static int RoundUp (this int n, int chunk) => chunk * ((n + chunk - 1) / chunk);

   /// <summary>Returns a value from a dictionary, or default value (of appropriate type) if the key does not exist</summary>
   public static U? SafeGet<T, U> (this IReadOnlyDictionary<T, U> dict, T key)
      => dict.GetValueOrDefault (key);
   /// <summary>Returns a value from a dictionary, or a user-supplied fallback if the key is not present</summary>
   public static U SafeGet<T, U> (this IReadOnlyDictionary<T, U> dict, T key, U fallback)
      => dict.GetValueOrDefault (key, fallback);

   /// <summary>Returns a value from an list, or default value (of appropriate type) if the index is out of range</summary>
   public static T? SafeGet<T> (this IReadOnlyList<T> list, int n)
      => n >= 0 && n < list.Count ? list[n] : default;

   /// <summary>Convert a double to a string, rounded to 6 decimal places (no trailing zeroes)</summary>
   /// This has special handling to avoid the annoying "-0"
   public static string S6 (this double f) {
      string s = Round (f, 6).ToString (CultureInfo.InvariantCulture);
      return s == "-0" ? "0" : s;
   }
   /// <summary>Convert a float a string, rounded to 5 decimal places (no trailing zeroes)</summary>
   /// This has special handling to prevent the annoying "-0"
   public static string S5 (this float f) {
      string s = Round (f, 5).ToString (CultureInfo.InvariantCulture);
      return s == "-0" ? "0" : s;
   }

   /// <summary>Converts an IEnumerable into a comma-separated list</summary>
   /// This takes each object out of the IEnumerable and prints it using it's ToString operator.
   /// It then returns all of them as a comma separated list. If any of the items has the separator
   /// appearing within it, it is quoted. If the quote character appears within any of the strings,
   /// the quote character is simply removed.
   public static string ToCSV<T> (this IEnumerable<T> collection, string separator = ",", string quote = "'") {
      bool iFirst = true;
      StringBuilder sb = new ();
      foreach (var obj in collection) {
         if (!iFirst) sb.Append (','); iFirst = false;
         string s = (obj?.ToString () ?? "").Replace ("\'", "");
         if (s.Contains ('\'')) s = $"'{s}'";
         sb.Append (s);
      }
      return sb.ToString ();
   }

   /// <summary>Convert a string to a double - if the conversion fails, this silently returns 0</summary>
   public static double ToDouble (this string s) {
      double.TryParse (s, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out double f);
      return f;
   }
   /// <summary>Convert a string to an integer - if the conversion fails, this silently returns 0</summary>
   public static int ToInt (this string s) {
      if (int.TryParse (s, out int n)) return n;
      if (s.IsBlank ()) return 0;
      s = s.Trim ();
      if (char.IsDigit (s[0])) return int.Parse (new string (s.TakeWhile (char.IsDigit).ToArray ()));
      return 0;
   }

   /// <summary>Convert a C style char * pointer to a C# string</summary>
   public static string ToUTF8 (this nint ptr)
      => Marshal.PtrToStringUTF8 (ptr) ?? string.Empty;

   /// <summary>Wrap an integer to a range within 0..max-1</summary>
   public static int Wrap (this int n, int max) => (n + max) % max;
}
#endregion

#region class EnumExtensions -----------------------------------------------------------------------
/// <summary>Extension methods on various Nori-defined enums</summary>
public static class EnumExtensions {
   /// <summary>How many bytes to encode each pixel, with a given DIBitmap format</summary>
   public static int BytesPerPixel (this DIBitmap.EFormat fmt) =>
      fmt switch {
         DIBitmap.EFormat.RGB8 => 3,
         DIBitmap.EFormat.Gray8 => 1,
         DIBitmap.EFormat.RGBA8 => 4,
         _ => throw new BadCaseException (fmt)
      };
}
#endregion
