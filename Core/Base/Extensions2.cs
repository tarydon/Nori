// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Extensions2.cs
// ║║║║╬║╔╣║ Extension methods on built-in types (defined using extension blocks)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Extensions2 --------------------------------------------------------------------------
/// <summary>Extension methods, properties on various standard types</summary>
public static class Extensions2 {
   // Extensions on double -----------------------------------------------------
   // Extension methods on double
   extension(double f) {
      /// <summary>Returns true if a double is nan - easier to use than double.IsNaN(f)</summary>
      public bool IsNan => double.IsNaN (f);

      /// <summary>Rounds a double to the nearest integer</summary>
      public int RInt () => (int)(Math.Round (f) + 0.5);

      /// <summary>Transforms a distance by the given transform</summary>
      /// Distance is updated only if the matrix has a scaling component
      public static double operator * (double a, Matrix3 xfm)
         => xfm.HasScaling ? a * xfm.ScaleFactor : a;
   }

   extension(ref double f) {
      /// <summary>Computes a double using the provided function (if it's NaN), caching it</summary>
      public double Cached (Func<double> compute) {
         if (double.IsNaN (f)) f = compute ();
         return f;
      }
   }

   // Extensions on ImmutableArray<Contour> ------------------------------------
   extension(ImmutableArray<Contour3> contours) {
      public static ImmutableArray<Contour3> operator * (ImmutableArray<Contour3> cons, Matrix3 xfm)
         => [.. cons.Select (a => a * xfm)];
   }

   // Extensions on string -----------------------------------------------------
   extension(string s) {
      /// <summary>Compares two strings, ignoring case</summary>
      public bool EqIC (string b) => s.Equals (b, StringComparison.OrdinalIgnoreCase);

      /// <summary>Checks if a string starts with the given substring (ignoring case)</summary>
      public bool StartsWithIC (string b) => s.StartsWith (b, StringComparison.OrdinalIgnoreCase);

      /// <summary>Convert a string to a double - if the conversion fails, this silently returns 0</summary>
      public double ToDouble () => ToDouble (s, 0.0);

      /// <summary>Convert a string to a double, returning the given fallback value if conversion fails</summary>
      public double ToDouble (double fallback) {
         if (double.TryParse (s, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out var f)) return f;
         return fallback;
      }

      /// <summary>Unquotes a string by removing the enveloping "" or '', if any</summary>
      /// This converts <tt>"Hello"</tt> to <tt>Hello</tt> or <tt>'Dolly'</tt> to <tt>Dolly</tt>. If the start
      /// and end quote characters do not match, or if the string is not _quoted_ at all, 
      /// it returns the original string unchanged. 
      public string Unquote () {
         if (s.Length >= 2) {
            char ch1 = s[0], ch2 = s[^1];
            if (ch1 == ch2 && (ch1 is '"' or '\'')) return s[1..^1];
         }
         return s;
      }
   }
}
#endregion
