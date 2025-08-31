// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Seg.cs
// ║║║║╬║╔╣║ Implements the Seg struct (one segment of a Poly)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
using static System.Math;
namespace Nori;

#if !OLDSEG
#region struct Seg --------------------------------------------------------------------------------
/// <summary>Represents a single Seg of a Poly (could be a line, arc or circle)</summary>
public readonly struct Seg {
   /// <summary>Construct a Seg, given a Poly and a seg index</summary>
   public Seg (Poly poly, int n) {
      if (n < 0 || n >= poly.Count) throw new IndexOutOfRangeException ($"Poly.Count = {poly.Count}, N = {n}");
      Poly = poly; N = n;
   }

   public override string ToString ()
      => IsArc
         ? $"ARC {A} .. {B}, {Center} {IsCCW}"
         : $"LINE {A} .. {B}";

   // Properties ---------------------------------------------------------------
   /// <summary>Start point of the segment</summary>
   public Point2 A => Poly.Pts[N];

   /// <summary>Angular span of this segment (+ve for CCW, -ve for CW)</summary>
   /// If this is a linear segment, this returns 0
   public double AngSpan {
      get {
         if (IsArc2 (out var cen, out var flags)) {
            var (sa, ea) = GetStartAndEndAngles (cen, flags);
            return ea - sa;
         }
         return 0;
      }
   }

   /// <summary>End point of the segment</summary>
   public Point2 B => Poly.Pts[(N + 1) % Poly.Pts.Length];

   /// <summary>The Bound of the segment</summary>
   public Bound2 Bound {
      get {
         Point2 a = A, b = B;
         Bound2 bound = new (a.X, a.Y, b.X, b.Y);
         if (IsArc2 (out var cen, out var flags)) {
            double r = cen.DistTo (a);
            if ((flags & Poly.EFlags.Circle) != 0)
               return new (cen.X - r, cen.Y - r, cen.X + r, cen.Y + r);
            bool ccw = (flags & Poly.EFlags.CCW) > 0;

            // Compute the start ang end angles in terms of quarter turns,
            // starting from east
            double sa = cen.AngleTo (a) / Lib.HalfPI;
            double ea = cen.AngleTo (b) / Lib.HalfPI;
            if (!ccw) (sa, ea) = (ea, sa);
            if (sa < 0) sa += 4;
            while (ea < sa) ea += 4;

            // The quadrant points lying within the circle can be enumerated from
            // ceiling(sa) .. floor(ea).
            int isa = (int)Ceiling (sa), iea = (int)Floor (ea);
            for (int i = isa; i <= iea; i++)
               bound += cen.CardinalMoved (r, (EDir)(i % 4));
         }
         return bound;
      }
   }

   /// <summary>Center point of a curved segment</summary>
   /// For a linear segment, this returns Point2.Nil
   public Point2 Center => IsArc2 (out var cen, out _) ? cen : Point2.Nil;

   /// <summary>Returns the Flags value for this segment</summary>
   public Poly.EFlags Flags => N < Poly.Extra.Length ? Poly.Extra[N].Flags : 0;

   /// <summary>Is this an arc? (if not, it's a line)</summary>
   public bool IsArc => (Flags & Poly.EFlags.Arc) != 0;
   /// <summary>Is this a CCW arc (if not, this is a CW arc OR a line)</summary>
   public bool IsCCW => (Flags & Poly.EFlags.CCW) != 0;
   /// <summary>Is this a 'full circle' segment</summary>
   public bool IsCircle => (Flags & Poly.EFlags.Circle) != 0;
   /// <summary>Is this the last segment in the Poly</summary>
   public bool IsLast => N == Poly.Count - 1;
   /// <summary>Is this a line? (if not, it's an arc)</summary>
   public bool IsLine => !IsArc;

   /// <summary>Returns true if the given point is to the 'left' of this segment</summary>
   public bool IsPointOnLeft (Point2 pt) {
      if (IsArc2 (out var cen, out var flags)) {
         double dist = cen.DistTo (pt), radius = cen.DistTo (A);
         return (dist > radius) ^ IsCCW;
      } else
         return pt.LeftOf (A, B);
   }

   /// <summary>Returns the length of this segment</summary>
   public double Length {
      get {
         if (IsArc2 (out var cen, out var flags)) {
            double r = cen.DistTo (A);
            var (sa, ea) = GetStartAndEndAngles (cen, flags);
            return r * Abs (sa - ea);
         }
         return A.DistTo (B);
      }
   }

   /// <summary>The midpoint of this segment</summary>
   public Point2 Midpoint => GetPointAt (0.5);

   /// <summary>Index of this seg within the Poly</summary>
   public readonly int N;

   /// <summary>The next segment on this same Poly</summary>
   /// If this is the last seg in the Poly, this returns:
   /// - null if this is an open Poly
   /// - the first seg if this is a closed Poly
   public Seg? Next {
      get {
         int n = (N + 1) % Poly.Count;
         if (n == 0 && Poly.IsOpen) return null;
         return new (Poly, n);
      }
   }
   /// <summary>The Poly this seg belongs to</summary>
   public readonly Poly Poly;

   /// <summary>The previous segment on this same Poly</summary>
   /// If this is the first seg in this Poly, this rturns:
   /// - null if this is a closed Poly
   /// - the last seg if this is a closed Poly
   public Seg? Prev {
      get {
         int n = (N - 1).Wrap (Poly.Count);
         if (N == 0 && Poly.IsOpen) return null;
         return new (Poly, n);
      }
   }

   /// <summary>Returns the slope of this segment (for an arc, the slope at the midpoint)</summary>
   public double Slope => GetSlopeAt (0.5);

   /// <summary>The radius of the segment (if it is an arc)</summary>
   /// If this is a line, this returns 0
   public double Radius => IsArc2 (out var cen, out _) ? cen.DistTo (A) : 0;

   // Methods ------------------------------------------------------------------
   /// <summary>Check if the Seg 'contains' the given point</summary>
   /// For correct results, the point in question must lie on the infinite
   /// line (if the Seg is linear) or on the circle (if it is curved)
   public bool Contains (Point2 p) => GetLie (p) is >= 0 and <= 1;

   /// <summary>This discretizes the segment into the given list of points with a given error threshold</summary>
   /// This does NOT add the start point into the list - if this is a line, it adds just
   /// one endpoint. If this is an arc, it adds as many points as needed to keep the error between
   /// the theoretical arc and the 'chords' to within the given error threshold
   public void Discretize (List<Point2> pts, double threshold) {
      if (IsArc2 (out var cen, out var flags)) {
         var (sa, ea) = GetStartAndEndAngles (cen, flags);
         var radius = cen.DistTo (A);
         int cSteps = Lib.GetArcSteps (radius, Abs (ea - sa), threshold);
         double angstep = (ea - sa) / cSteps;
         for (int j = 1; j <= cSteps; j++)
            pts.Add (cen.Polar (radius, sa += angstep));
      } else
         pts.Add (B);
   }

   /// <summary>Computes the bounding rectangle of the Seg, under a given transform</summary>
   public Bound2 GetBound (Matrix2 xfm) {
      Point2 a = A * xfm, b = B * xfm;
      Bound2 bound = new (a.X, a.Y, b.X, b.Y);
      if (IsArc2 (out var cen, out var flags)) {
         // Some repeated code here again because of the time-critical nature
         // of the Bound code - we don't want to keep that routine as tight as possible
         cen *= xfm;
         double r = cen.DistTo (a);
         bool ccw = (flags & Poly.EFlags.CCW) > 0;
         double dAngle = (Vector2.XAxis * xfm).Heading;
         double sa = cen.AngleTo (a) + dAngle, ea = cen.AngleTo (b) + dAngle;
         for (int i = 0; i <= 4; i++) {
            double ang = i * Lib.HalfPI;
            bool include;
            if (ccw) {
               // For a CCW segment, we have -180 < sa <= 180, and we have
               // ea > sa. So first adjust ang so that it is more than sa.
               if (ang < sa) ang += Lib.TwoPI;
               include = ang < ea;
            } else {
               // For a CW segment, ea < sa, so we adjust ang so that it is less
               // than ea and then check if it lies within range
               if (ang > sa) ang -= Lib.TwoPI;
               include = ang > ea;
            }
            if (include)
               bound += cen.CardinalMoved (r, (EDir)i);
         }
      }
      return bound;
   }

   /// <summary>Returns the closest point to the given point on the given segment</summary>
   public Point2 GetClosestPoint (Point2 pt) {
      if (IsArc2 (out var cen, out var flags)) {
         Point2 pt2 = cen.Polar (cen.DistTo (A), cen.AngleTo (pt));
         return GetPointAt (GetLie (pt2).Clamp ());
      } else
         return pt.SnappedToLineSeg (A, B);
   }

   /// <summary>Returns the closest distance of the given point pt to this seg</summary>
   /// If the distance is more than the cutoff distance, this does not return
   /// the exact distance, but a conservative distance (greater than actual distance)
   public double GetDist (Point2 p, double cutoff) {
      if (IsArc2 (out var cen, out var flags)) {
         var radius = cen.DistTo (A);
         double dist = Abs (cen.DistTo (p) - radius);
         if (dist >= cutoff) return dist;
         double lie = GetLie (p).Clamp ();
         return p.DistTo (GetPointAt (lie));
      } else {
         double dist = p.DistToLine (A, B);
         if (dist >= cutoff) return dist;
         return p.DistToLineSeg (A, B);
      }
   }

   /// <summary>Gets the 'lie' of a given point on the segment (0 = start, 1 = end)</summary>
   /// The lie may be less than zero or more than 1. For a point that lies outside
   /// the extent of an arc, there is an ambiguity - there may be a point that could
   /// be represented as having either a positive lie (> 1) or a negative lie (< 0).
   /// We pick the lie based on whether the given point is closer to the start or the
   /// end point.
   public double GetLie (Point2 pt) {
      if (IsArc2 (out var cen, out var flags)) {
         var (sa, ea) = GetStartAndEndAngles (cen, flags);
         double ang = cen.AngleTo (pt);
         if ((flags & Poly.EFlags.CCW) != 0) {
            double oppMid = (sa + ea) / 2 + Lib.PI;
            if (ang < sa) ang += Lib.TwoPI;
            if (ang > oppMid) ang -= Lib.TwoPI;
         } else {
            double oppMid = (sa + ea) / 2 - Lib.PI;
            if (ang > sa) ang -= Lib.TwoPI;
            if (ang < oppMid) ang += Lib.TwoPI;
         }
         return (ang - sa) / (ea - sa);
      }
      return pt.GetLieOn (A, B);
   }

   /// <summary>Gets the point at a given 'lie' along the segment (0 = start, 1 = end)</summary>
   public Point2 GetPointAt (double lie) {
      if (IsArc2 (out var cen, out var flags)) {
         double r = cen.DistTo (A);
         var (sa, ea) = GetStartAndEndAngles (cen, flags);
         return cen.Polar (r, lie.Along (sa, ea));
      }
      return lie.Along (A, B);
   }

   /// <summary>Gets the slope at a given 'lie' along the segment (0 = start, 1 = end)</summary>
   public double GetSlopeAt (double lie) {
      if (IsArc2 (out var cen, out var flags)) {
         var (sa, ea) = GetStartAndEndAngles (cen, flags);
         return lie.Along (sa, ea) + Lib.HalfPI * (((flags & Poly.EFlags.CCW) != 0) ? 1 : -1);
      }
      return A.AngleTo (B);
   }

   /// <summary>Gets the start and end angles of an arc</summary>
   /// - For a line, this returns (0, 0)
   /// - For a circle, this returns (0, 2*PI)
   /// - For a CCW arc this ensures end > start
   /// - For a CW arc, this ensures end < start
   /// <returns></returns>
   public (double Start, double End) GetStartAndEndAngles () {
      if (IsArc2 (out var cen, out var flags))
         return GetStartAndEndAngles (cen, flags);
      return (0, 0);
   }

   /// <summary>Coputes the intersection between this segment and an infinite line</summary>
   /// <param name="a">Start point of the infinite line</param>
   /// <param name="b">End point of the infinite line</param>
   /// <param name="buffer">Buffer that the caller should allocate (should contain at least 2 elements)</param>
   /// <param name="finite">If set, returns only the intersections that lie within the span of the segment,
   /// otherwise checks for the extrapolations of the segments as well</param>
   /// <returns>A slice of the input buffer that contains 0, 1 or 2 points</returns>
   public ReadOnlySpan<Point2> Intersect (Point2 a, Point2 b, Span<Point2> buffer, bool finite) {
      ReadOnlySpan<Point2> pts;
      if (IsArc2 (out var cen, out _)) {
         pts = Geo.CircleXLine (cen, cen.DistTo (A), a, b, buffer);
         if (!finite) return pts;

         // Limit the set to the points that lie within this span
         int n = 0;
         if (pts.Length > 0 && Contains (pts[0])) n |= 1;
         if (pts.Length > 1 && Contains (pts[1])) n |= 2;
         return n switch {
            0 => [],             // Neither of the points are contained
            1 => pts[0..1],      // Only first point is contained
            2 => pts,            // Both points are contained
            _ => pts[1..2]       // Only second point is contained
         };
      } else {
         buffer[0] = Geo.LineXLine (A, B, a, b);
         if (buffer[0].IsNil) return [];
         if (finite && !Contains (buffer[0])) return [];
         return buffer[0..1];
      }
   }

   /// <summary>Computes the intersection between this segment and another</summary>
   /// <param name="other">The other segment to intersect</param>
   /// <param name="buffer">Buffer that the caller should allocate (should contain at least 2 elements)</param>
   /// <param name="finite">If set, returns only intersections that lie within the span of the segment,
   /// otherwise checks for the extrapolations of the segments as well</param>
   /// <returns>A slice of the input buffer that contains 0, 1 or 2 points</returns>
   public ReadOnlySpan<Point2> Intersect (Seg s2, Span<Point2> buffer, bool finite) {
      if (IsArc2 (out var cen1, out _)) {
         double rad1 = cen1.DistTo (A);
         ReadOnlySpan<Point2> pts;
         if (s2.IsArc2 (out var cen2, out _)) {
            // Case: ARC x ARC
            double rad2 = s2.Radius;
            pts = Geo.CircleXCircle (cen1, rad1, cen2, rad2, buffer);
         } else {
            // Case: ARC x LINE
            pts = Geo.CircleXLine (cen1, rad1, s2.A, s2.B, buffer);
         }
         if (!finite) return pts;

         // Limit the set to the points that lie within the span
         int n = 0;
         if (pts.Length > 0 && Contains (pts[0]) && s2.Contains (pts[0])) n |= 1;
         if (pts.Length > 1 && Contains (pts[1]) && s2.Contains (pts[1])) n |= 2;
         return n switch {
            0 => [],             // Neither of the points are contained
            1 => pts[0..1],      // Only first point is contained
            2 => pts,            // Both points are contained
            _ => pts[1..2]       // Only second point is contained
         };
      } else {
         if (s2.IsArc) {
            // Case: LINE x ARC
            // Convert this to an ARC x LINE case
            return s2.Intersect (this, buffer, finite);
         } else {
            // Case: LINE x LINE
            var pt = finite ? Geo.LineSegXLineSeg (A, B, s2.A, s2.B) : Geo.LineXLine (A, B, s2.A, s2.B);
            if (!pt.IsNil) { buffer[0] = pt; return buffer[0..1]; }
            return [];
         }
      }
   }

   /// <summary>Convert the curved segment into 1 or more beziers</summary>
   /// We convert the Seg to multiple beziers, such that each bezier spans
   /// no more than a quarter of a circle
   public void ToBeziers (List<Vec2F> pts) {
      double angSpan = Abs (AngSpan);
      int cBeziers = Max (1, (int)Ceiling ((angSpan - 0.001) / Lib.HalfPI));
      double segHalfSpan = angSpan / (2 * cBeziers);
      double dist = (4.0 / 3) * (1 - Cos (segHalfSpan)) / Sin (segHalfSpan) * Radius;

      Point2 pb = A; double ang2 = GetSlopeAt (0);
      for (int i = 0; i < cBeziers; i++) {
         double lie2 = (i + 1) / (double)cBeziers;
         Point2 pa = pb; pb = GetPointAt (lie2);
         double ang1 = ang2; ang2 = GetSlopeAt (lie2);
         pts.Add (pa); pts.Add (pa.Polar (dist, ang1));
         pts.Add (pb.Polar (-dist, ang2)); pts.Add (pb);
      }
   }

   // Implementation -----------------------------------------------------------
   // Get the start and end angles of an arc (this ensures that end > start if
   // CCW, and end < start if CW)
   (double S, double E) GetStartAndEndAngles (Point2 cen, Poly.EFlags flags) {
      if ((flags & Poly.EFlags.Circle) != 0)
         return (0, (flags & Poly.EFlags.CCW) != 0 ? Lib.TwoPI : -Lib.TwoPI);
      double s = cen.AngleTo (A), e = cen.AngleTo (B);
      if ((flags & Poly.EFlags.CCW) > 0) {
         if (e < s) e += Lib.TwoPI;
      } else {
         if (e > s) e -= Lib.TwoPI;
      }
      return (s, e);
   }

   // Checks if this segment is an arc (if so, it also returns the center point
   // and the flags - useful to check if the arc is CW or CCW)
   bool IsArc2 (out Point2 cen, out Poly.EFlags flags) {
      if (N < Poly.Extra.Length) {
         var extra = Poly.Extra[N];
         flags = extra.Flags;
         if ((flags & Poly.EFlags.Arc) != 0) { cen = extra.Center; return true; }
      }
      cen = Point2.Nil; flags = 0;
      return false;
   }
}
#endregion
#endif
