// ────── ╔╗
// ╔═╦╦═╦╦╬╣ StdShape.cs
// ║║║║╬║╔╣║ Implements ShapeDesc and ShapeRecognizer classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class EShape -------------------------------------------------------------------------------
/// <summary>Enumeration for all the 'standard shapes' that we can recognize / construct</summary>
public enum EShape {
   None, Circle, Rect, ChamferSquare, ChamferRect, RoundRect, Obround, SingleD, DoubleD,
   HalfObround, Diamond, Diajoint, Trapezoid, RoundTrapezoid, RightTriangle, Parallelogram,
   Hexagon, Octagon, ArcSlot, SquareArcslot, HShape, FlatKeyhole, DoubleFlatKeyhole,
   FlatKeyslot, Keyslot, DoubleKeyslot, QuadInfillet, RectInfillet, TwoRoundDiamond,
   IsoTriangle, RoundIsoTriangle, OneRoundIsoTriangle, OneRoundRightTriangle,
   QuadFlatKeyhole,
}
#endregion

#region class ShapeDesc ----------------------------------------------------------------------------
/// <summary>Descriptor for a standard shape</summary>
/// Given a Poly, the ShapeRecognizer can analyze it and build a ShapeDesc for that shape.
/// Given a ShapeDesc, the ShapeMaker can build a Poly that matches
public class ShapeDesc {
   // Constructor --------------------------------------------------------------
   internal ShapeDesc (EShape shape, Point2 cen, double angle, ImmutableArray<double> args)
      => (Shape, Center, Angle, Args) = (shape, cen, angle, args);

   public override string ToString () => $"{Shape} {Center.R6 ()} {Angle.R2D ().R6 ()}\u00b0 [{Args.Select (a => a.R6 ()).ToCSV ()}]";

   // Properties ---------------------------------------------------------------
   /// <summary>Type of shape this is (could be Shape.None to indicate we cannot recognize it)</summary>
   public readonly EShape Shape;
   /// <summary>'Center point' of the shape</summary>
   public readonly Point2 Center;
   /// <summary>Rotation angle of the shape, relative to X axis</summary>
   public readonly double Angle;
   /// <summary>The 'parameters' for the shape (depends on type of shape)</summary>
   public ImmutableArray<double> Args;

   public bool IsNone => Shape == EShape.None;

   public static readonly ShapeDesc None = new (EShape.None, Point2.Zero, 0, []);
}
#endregion

#region class ShapeRecognizer ----------------------------------------------------------------------
/// <summary>ShapeRecognizer recognizes many 'standard' shapes, and constructs ShapeDesc</summary>
/// For example, these include shapes like Rect, FilletRect, Obround etc. The ShapeDesc
/// contains:
/// - the 'center' of the shape
/// - the 'rotation angle' of the shape
/// - the 'parameters' for the shape (for example, a FilletRect has Length, Width, Radius)
public static class ShapeRecognizer {
   // Methods ------------------------------------------------------------------
   /// <summary>Gets the ShapeDesc for a Poly</summary>
   /// If the poly is not any recognizable shape (or open), this returns a ShapeDesc
   /// with Shape set to EShape.None (you can check that with the ShapeDesc.IsNone property)
   public static ShapeDesc Recognize (Poly poly) {
      ShapeDesc desc = ShapeDesc.None;
      if (poly.IsOpen) return desc;
      switch (poly.Count) {
         case 1:
            if (IsCircle (poly, ref desc)) return desc;
            break;
         case 2:
            if (IsSingleD (poly, ref desc)) return desc;
            break;
         case 4:
            if (IsRect (poly, ref desc)) return desc;
            if (IsObround (poly, ref desc)) return desc;
            if (IsDoubleD (poly, ref desc)) return desc;
            if (IsParallelogram (poly, ref desc)) return desc;
            if (IsTrapezoid (poly, ref desc)) return desc;
            break;
         case 8:
            if (IsRoundRect (poly, ref desc)) return desc;
            if (IsQuadInFillet (poly, ref desc)) return desc;
            if (IsChamferedRect (poly, ref desc)) return desc;
            break;
      }
      return ShapeDesc.None;
   }

   // Recognizers --------------------------------------------------------------
   // Check if the given poly is a circle
   static bool IsCircle (Poly poly, ref ShapeDesc desc) {
      if (!poly.IsCircle) return false;
      var seg = poly[0];
      desc = new ShapeDesc (EShape.Circle, seg.Center, 0, [seg.Radius]);
      return true;
   }

   // Check if the given Poly is a rectangle
   static bool IsRect (Poly poly, ref ShapeDesc desc) {
      sRect ??= Make (@"^(F[^ ]+ L F[^ ]+ L ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sRect.Match (code))) return false;

      // By the definition of GetLogoCode, seg is the longest segment, and that
      // becomes the 'length'. The next segment becomes the 'width'
      var seg = poly[n];
      double angle = seg.Slope, len = seg.Length, wid = poly[n + 1].Length;
      Point2 cen = seg.A.Polar (len / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.Rect, cen, angle, [len, wid]);
      return true;
   }
   static Regex? sRect;

   // Check if the given Poly is a rounded/filleted rectangle
   static bool IsRoundRect (Poly poly, ref ShapeDesc desc) {
      sRoundRect ??= Make (@"^(F[^ ]+ R[^ ]+ G[^ ]+ R[^ ]+ F[^ ]+ R[^ ]+ G[^ ]+ R[^ ]+ ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sRoundRect.Match (code))) return false;

      var seg = poly[n];
      var rad = poly[n + 1].Radius;
      double angle = seg.Slope, wid = poly[n + 2].Length + 2 * rad;
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.RoundRect, cen, angle, [seg.Length + 2 * rad, wid, rad]);
      return true;
   }
   static Regex? sRoundRect;

   static bool IsQuadInFillet (Poly poly, ref ShapeDesc desc) {
      sQuadInFillet ??= Make (@"^(F[^ ]+ L[^ ]+ D[^ ]+ L[^ ]+ F[^ ]+ L[^ ]+ D[^ ]+ L[^ ]+ ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sQuadInFillet.Match (code))) return false;

      var (seg, aseg) = (poly[n], poly[n - 1]);
      var rad = aseg.Radius;
      double angle = seg.Slope, len = seg.Length + 2 * rad, wid = poly[n + 2].Length + 2 * rad;
      Point2 cen = aseg.Center.Polar (len / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.QuadInfillet, cen, angle, [len, wid, rad]);
      return true;
   }
   static Regex? sQuadInFillet;

   static bool IsChamferedRect (Poly poly, ref ShapeDesc desc) {
      sChamferRect ??= Make (@"^(F[^ ]+ L45 F[^ ]+ L45 F[^ ]+ L45 F[^ ]+ L45 ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sChamferRect.Match (code))) return false;

      var seg = poly[n];
      double chamfer = poly[n + 1].Length;
      double c2 = chamfer * Lib.Root2;
      var (width, height) = (seg.Length + c2, poly[n + 2].Length + c2);
      Point2 cen = seg.A.Midpoint (poly[n+4].A);
      desc = new ShapeDesc (width.EQ (height) ? EShape.ChamferSquare : EShape.ChamferRect, cen, seg.Slope, [width, height, chamfer]);
      return true;
   }
   static Regex? sChamferRect;

   static bool IsObround (Poly poly, ref ShapeDesc desc) {
      sObround ??= Make (@"^(F[^ ]+ G[^ ]+ ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sObround.Match (code))) return false;

      var seg = poly[n];
      var rad = poly[n + 1].Radius;
      double angle = seg.Slope, wid = 2 * rad;
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (rad, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.Obround, cen, angle, [seg.Length + wid, wid]);
      return true;
   }
   static Regex? sObround;

   static bool IsDoubleD (Poly poly, ref ShapeDesc desc) {
      sDoubleD ??= Make (@"^(F[^ ]+ L[^ ]+ G[^ ]+ L[^ ]+ ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sDoubleD.Match (code))) return false;

      var (seg, aseg) = (poly[n], poly[n + 1]);
      var (rad, angle) = (aseg.Radius, seg.Slope);
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (rad, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.DoubleD, cen, angle, [2 * rad, aseg.A.DistTo (aseg.B)]);
      return true;
   }
   static Regex? sDoubleD;

   static bool IsParallelogram (Poly poly, ref ShapeDesc desc) {
      sParallelogram ??= Make (@"^(F[^ ]+ L[^ ]+ F[^ ]+ L[^ ]+ ){2}\.$");
      var (n, code) = poly.GetLogoCode (6);
      if (!RepeatedPattern (sParallelogram.Match (code))) return false;

      var (seg, seg2) = (poly[n], poly[n+2]);
      var (width, height) = (seg.Length, seg.A.DistToLine (seg2.A, seg2.B));
      var cen = seg.A.Midpoint (seg2.A);
      desc = new ShapeDesc (EShape.Parallelogram, cen, seg.Slope, [width, height, Math.Asin (height / poly[n + 1].Length)]);
      return true;
   }
   static Regex? sParallelogram;

   static bool IsSingleD (Poly poly, ref ShapeDesc desc) {
      // Captures: F34.641016 L(59.999994) G20.000001,240.000011 L(59.999994) .
      sSingleD ??= Make (@"^F[^ ]+ L([^ ]+) G[^ ]+ L([^ ]+) .$");
      var (n, code) = poly.GetLogoCode (6);
      var m = sSingleD.Match (code);
      if (!m.Success || m.Groups[1].Value != m.Groups[2].Value) return false;

      var (seg, aseg) = (poly[n], poly[n + 1]);
      var arcMid = aseg.GetPointAt (0.5);
      double angle = Lib.NormalizeAngle (aseg.Center.AngleTo (arcMid) + Lib.TwoPI);
      desc = new ShapeDesc (EShape.SingleD, aseg.Center, angle, [aseg.Radius * 2, seg.GetPointAt (0.5).DistTo (arcMid)]);
      return true;
   }
   static Regex? sSingleD;

   static bool IsTrapezoid (Poly poly, ref ShapeDesc desc) {
      // Captures: F40 L(70) F(21.283555) L(70) F25.441191 L(110) F(21.283555) L(110) .
      // Ensure: Capture(0) == Capture(2); Capture(1) == Capture(4); Capture(3) == Capture(5)
      sTrapezoid ??= Make (@"^F[^ ]+ L([^ ]+) F([^ ]+) L([^ ]+) F[^ ]+ L([^ ]+) F([^ ]+) L([^ ]+) .$");
      var (n, code) = poly.GetLogoCode (6);
      var m = sTrapezoid.Match (code);
      if (!m.Success || m.Groups[1].Value != m.Groups[3].Value
         || m.Groups[2].Value != m.Groups[5].Value || m.Groups[4].Value != m.Groups[6].Value) return false;

      var seg = poly[n];
      var (pt, pt2) = (seg.GetPointAt (0.5), poly[n + 2].GetPointAt (0.5));
      double l = seg.Length, h = pt.DistTo (pt2);
      var ex = (l - poly[n + 2].Length) / 2;
      desc = new ShapeDesc (EShape.Trapezoid, pt.Midpoint (pt2), seg.Slope, [l, h, Math.Atan2 (ex, h)]);
      return true;
   }
   static Regex? sTrapezoid;

   // Helpers ------------------------------------------------------------------
   // Makes a compiled mode Regex
   static Regex Make (string s) => new (s, RegexOptions.Compiled);

   // Tells if repeated matched patterns are actually identical.
   static bool RepeatedPattern (Match m) {
      if (!m.Success || m.Groups.Count != 2 || m.Groups[1].Captures.Count != 2) return false;
      var captures = m.Groups[1].Captures;
      return captures[0].Value == captures[1].Value;
   }
}
#endregion
