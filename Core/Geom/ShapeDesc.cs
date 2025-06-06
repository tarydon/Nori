using System.Text.RegularExpressions;
namespace Nori;

public enum EShape {
   None, Circle, Rect, FilletedRect, ChamferedRect, Obround, SingleD, DoubleD,
}

public class ShapeDesc {
   public ShapeDesc (EShape shape, Point2 cen, double angle, ImmutableArray<double> args)
      => (Shape, Center, Angle, Args) = (shape, cen, angle, args);

   public override string ToString () => $"{Shape} {Center} {Angle.R2D ()}\u00b0 [{Args.ToCSV ()}]";

   public readonly EShape Shape;
   public readonly Point2 Center;
   public readonly double Angle;
   public ImmutableArray<double> Args;
   public bool IsNone => Shape == EShape.None;

   public static readonly ShapeDesc None = new (EShape.None, Point2.Zero, 0, []);
}

public static class ShapeRecognizer {
   public static ShapeDesc Get (Poly poly) {
      ShapeDesc desc = ShapeDesc.None;
      if (poly.IsOpen) return desc;
      switch (poly.Count) {
         case 1:
            if (IsCircle (poly, ref desc)) return desc;
            break;

         // case 2:
         //    if (IsSingleD (poly, ref desc)) return desc;
         //    break;

         case 4:
            if (IsRect (poly, ref desc)) return desc;
            break;
         //   if (IsObround (poly, ref desc)) return desc;
         //   break;
      }
      return ShapeDesc.None;
   }

   static bool IsCircle (Poly poly, ref ShapeDesc desc) {
      if (!poly.IsCircle) return false;
      var seg = poly[0];
      desc = new ShapeDesc (EShape.Circle, seg.Center, 0, [seg.Radius]);
      return true;
   }

   static bool IsRect (Poly poly, ref ShapeDesc desc) {
      sRect ??= Make (@"F(\S*) L F(\S*) L F\1 L F\2 L .");
      var (n, code) = poly.GetLogoCode (6);
      Match m = sRect.Match (code); if (!m.Success) return false;

      var seg = poly[n];
      double angle = seg.Slope, len = GetD (m, 1), wid = GetD (m, 2);
      Point2 cen = seg.A.Polar (len / 2, angle).Polar (wid / 2, angle + Lib.HalfPI);
      desc = new ShapeDesc (EShape.Rect, cen, angle, [len, wid]);
      return true;
   }
   static Regex? sRect;

   static Regex Make (string s) => new (s, RegexOptions.Compiled);

   static double GetD (Match m, int n) => double.Parse (m.Groups[n].ValueSpan);
}
