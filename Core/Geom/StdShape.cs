// ────── ╔╗
// ╔═╦╦═╦╦╬╣ StdShape.cs
// ║║║║╬║╔╣║ Implements ShapeDesc and ShapeRecognizer classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class EShape -------------------------------------------------------------------------------
/// <summary>
/// Enumeration for all the 'standard shapes' that we can recognize / construct
/// </summary>
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
/// <summary>
/// Descriptor for a standard shape
/// </summary>
/// Given a Poly, the ShapeRecognizer can analyze it and build a ShapeDesc for that shape.
/// Given a ShapeDesc, the ShapeMaker can build a Poly that matches
public class ShapeDesc {
   // Constructor --------------------------------------------------------------
   internal ShapeDesc (EShape shape, Point2 cen, double angle, ImmutableArray<double> args)
      => (Shape, Center, Angle, Args) = (shape, cen, angle, args);

   public override string ToString () => $"{Shape} {Center.R6 ()} {Angle.R2D ().R6 ()}\u00b0 [{Args.Select (a => a.R6 ()).ToCSV ()}]";

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Type of shape this is (could be Shape.None to indicate we cannot recognize it)
   /// </summary>
   public readonly EShape Shape;
   /// <summary>
   /// 'Center point' of the shape
   /// </summary>
   public readonly Point2 Center;
   /// <summary>
   /// Rotation angle of the shape, relative to X axis
   /// </summary>
   public readonly double Angle;
   /// <summary>
   /// The 'parameters' for the shape (depends on type of shape)
   /// </summary>
   public ImmutableArray<double> Args;

   public bool IsNone => Shape == EShape.None;

   public static readonly ShapeDesc None = new (EShape.None, Point2.Zero, 0, []);
}
#endregion

#region class ShapeRecognizer ----------------------------------------------------------------------
/// <summary>
/// ShapeRecognizer recognizes many 'standard' shapes, and constructs ShapeDesc
/// </summary>
/// For example, these include shapes like Rect, FilletRect, Obround etc. The ShapeDesc
/// contains:
/// - the 'center' of the shape
/// - the 'rotation angle' of the shape
/// - the 'parameters' for the shape (for example, a FilletRect has Length, Width, Radius)
public static class ShapeRecognizer {
   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Gets the ShapeDesc for a Poly
   /// </summary>
   /// If the poly is not any recognizable shape (or open), this returns a ShapeDesc
   /// with Shape set to EShape.None (you can check that with the ShapeDesc.IsNone property)
   public static ShapeDesc Recognize (Poly poly) {
      ShapeDesc desc = ShapeDesc.None;
      if (poly.IsOpen) return desc;
      switch (poly.Count) {
         case 1:
            if (IsCircle (poly, ref desc)) return desc;
            break;
         case 4:
            if (IsRect (poly, ref desc)) return desc;
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
      sRect ??= Make (@"<insert suitable regular expression here>");
      var (n, code) = poly.GetLogoCode (6);
      Match m = sRect.Match (code); if (!m.Success) return false;

      // By the definition of GetLogoCode, seg is the longest segment, and that
      // becomes the 'length'. The next segment becomes the 'width'
      var seg = poly[n];
      double angle = seg.Slope, len = seg.Length, wid = poly[n + 1].Length;
      Point2 cen = seg.A.Polar (len / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.Rect, cen, angle, [len, wid]);
      return true;
   }
   static Regex? sRect;

   // Helpers ------------------------------------------------------------------
   // Makes a compiled mode Regex
   static Regex Make (string s) => new (s, RegexOptions.Compiled);
}
#endregion
