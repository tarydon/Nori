// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Lib.cs ~ Implements the Lib module class that has a number of global functions
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Reflection;
namespace Nori;

#region class Lib ----------------------------------------------------------------------------------
public static class Lib {
   // Constants ----------------------------------------------------------------
   public const double Epsilon = 1e-6;
   public const double PI = Math.PI;
   public const double TwoPI = 2 * Math.PI;
   public const double HalfPI = Math.PI / 2;
   public const double QuarterPI = Math.PI / 4;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the cos-inverse of the given value</summary>
   /// This clamps values beyond the range -1 .. +1 to lie within that range
   public static double Acos (double f) => Math.Acos (f.Clamp (-1, 1));

   /// <summary>Loads text from an embedded resource</summary>
   public static string GetResText (string name) {
      var assembly = Assembly.GetExecutingAssembly ();
      using var stm = assembly.GetManifestResourceStream ($"Pix.Res.{name}")!;
      using var reader = new StreamReader (stm);
      return reader.ReadToEnd ().ReplaceLineEndings ("\n");
   }

   /// <summary>Normalizes an angle (in radians) to lie in the half open range (-PI .. PI]</summary>
   public static double NormalizeAngle (double fAng) {
      fAng %= TwoPI;
      if (fAng > PI) fAng -= TwoPI;
      if (fAng <= -PI) fAng += TwoPI;
      return fAng;
   }

   /// <summary>Solves a system of 2 linear equations with 2 unknowns</summary>
   /// Ax + By + C = 0
   /// Dx + Ey + F = 0
   public static bool SolveLinearPair (double A, double B, double C, double D, double E, double F, out double x, out double y) {
      double fHypot = A * E - D * B;
      if (fHypot.IsZero ()) { x = y = 0; return false; }
      x = (B * F - E * C) / fHypot; y = (D * C - A * F) / fHypot;
      return true;
   }
}
#endregion
