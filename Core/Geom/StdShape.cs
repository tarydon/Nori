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
      // F40 L F20 L F40 L F20 L .
      sRect ??= Make (@"^(F[^ ]+) ([L|R]) (F[^ ]+) \2 \1 \2 \3 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sRect.Match (code).Success) return false;

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
      // F30 G5,90 F10 G5,90 F30 G5,90 F10 G5,90 .
      sRoundRect ??= Make (@"^(F[^ ]+) ([G|D][^ ]+) (F[^ ]+) \2 \1 \2 \3 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sRoundRect.Match (code).Success) return false;

      var seg = poly[n];
      var rad = poly[n + 1].Radius;
      double angle = seg.Slope, wid = poly[n + 2].Length + 2 * rad;
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.RoundRect, cen, angle, [seg.Length + 2 * rad, wid, rad]);
      return true;
   }
   static Regex? sRoundRect;

   static bool IsQuadInFillet (Poly poly, ref ShapeDesc desc) {
      // F30 L D5,90 L F10 L D5,90 L F30 L D5,90 L F10 L D5,90 L .
      sQuadInFillet ??= Make (@"^(F[^ ]+) ([L|R]) ([D|G][^ ]+) \2 (F[^ ]+) \2 \3 \2 \1 \2 \3 \2 \4 \2 \3 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sQuadInFillet.Match (code).Success) return false;

      var (seg, aseg) = (poly[n], poly[n - 1]);
      var rad = aseg.Radius;
      double angle = seg.Slope, len = seg.Length + 2 * rad, wid = poly[n + 2].Length + 2 * rad;
      Point2 cen = aseg.Center.Polar (len / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.QuadInfillet, cen, angle, [len, wid, rad]);
      return true;
   }
   static Regex? sQuadInFillet;

   static bool IsChamferedRect (Poly poly, ref ShapeDesc desc) {
      // F30 L45 F7.071068 L45 F10 L45 F7.071068 L45 F30 L45 F7.071068 L45 F10 L45 F7.071068 L45 .
      sChamferRect ??= Make (@"^(F[^ ]+) ([L|R]45) (F[^ ]+) \2 (F[^ ]+) \2 \3 \2 \1 \2 \3 \2 \4 \2 \3 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sChamferRect.Match (code).Success) return false;

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
      // F20 G10,180 F20 G10,180 .
      sObround ??= Make (@"^(F[^ ]+) ([G|D][^,]+,180) \1 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sObround.Match (code).Success) return false;

      var seg = poly[n];
      var rad = poly[n + 1].Radius;
      double angle = seg.Slope, wid = 2 * rad;
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (rad, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.Obround, cen, angle, [seg.Length + wid, wid]);
      return true;
   }
   static Regex? sObround;

   static bool IsDoubleD (Poly poly, ref ShapeDesc desc) {
      // F34.641016 L60.000021 G20.000012,59.999959 L60.000021 F34.641016 L60.000021 G20.000012,59.999959 L60.000021 .
      sDoubleD ??= Make (@"^(F[^ ]+) ([L|R][^ ]+) ([G|D][^ ]+) \2 \1 \2 \3 \2 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sDoubleD.Match (code).Success) return false;

      var (seg, aseg) = (poly[n], poly[n + 1]);
      var (rad, angle) = (aseg.Radius, seg.Slope);
      Point2 cen = seg.A.Polar (seg.Length / 2, angle).Polar (rad, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.DoubleD, cen, angle, [2 * rad, aseg.A.DistTo (aseg.B)]);
      return true;
   }
   static Regex? sDoubleD;

   static bool IsParallelogram (Poly poly, ref ShapeDesc desc) {
      // F40 L70 F21.283555 L110 F40 L70 F21.283555 L110 .
      sParallelogram ??= Make (@"^(F[^ ]+) ([L|R])([^ ]+) (F[^ ]+) \2([^ ]+) \1 \2\3 \4 \2\5 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sParallelogram.Match (code).Success) return false;

      var (seg, seg2) = (poly[n], poly[n+2]);
      var (width, height) = (seg.Length, seg.A.DistToLine (seg2.A, seg2.B));
      var cen = seg.A.Midpoint (seg2.A);
      desc = new ShapeDesc (EShape.Parallelogram, cen, seg.Slope, [width, height, Math.Asin (height / poly[n + 1].Length)]);
      return true;
   }
   static Regex? sParallelogram;

   static bool IsSingleD (Poly poly, ref ShapeDesc desc) {
      // F34.641016 L59.999994 G20.000001,240.000011 L59.999994 .
      sSingleD ??= Make (@"^F[^ ]+ ([L|R][^ ]+) [G|D][^ ]+ \1 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sSingleD.Match (code).Success) return false;

      var (seg, aseg) = (poly[n], poly[n + 1]);
      var arcMid = aseg.GetPointAt (0.5);
      double angle = Lib.NormalizeAngle (aseg.Center.AngleTo (arcMid) + Lib.TwoPI);
      desc = new ShapeDesc (EShape.SingleD, aseg.Center, angle, [aseg.Radius * 2, seg.GetPointAt (0.5).DistTo (arcMid)]);
      return true;
   }
   static Regex? sSingleD;

   static bool IsTrapezoid (Poly poly, ref ShapeDesc desc) {
      // F40 L70 F21.283555 L70 F25.441191 L110 F21.283555 L110 .
      sTrapezoid ??= Make (@"^(F[^ ]+) ([L|R])([^ ]+) (F[^ ]+) \2\3 F[^ ]+ \2([^ ]+) \4 \2\5 .$");
      var (n, code) = poly.GetLogoCode (6);
      if (!sTrapezoid.Match (code).Success) return false;

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
}
#endregion
