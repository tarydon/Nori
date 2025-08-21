// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Seg.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

#if NEWSEG
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
            bool ccw = (flags & Poly.EFlags.CCW) > 0;
            double sa = cen.AngleTo (a), ea = cen.AngleTo (b);
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

   /// <summary>Index of this seg within the Poly</summary>
   public readonly int N;

   /// <summary>The Poly this seg belongs to</summary>
   public readonly Poly Poly;

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
      if ((flags & Poly.EFlags.Circle) != 0) return (0, Lib.TwoPI);
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

#else 
#region struct Seg ---------------------------------------------------------------------------------
/// <summary>Represents a single Segment of a Poly (can be a single line or an arc)</summary>
public readonly struct Seg (Point2 a, Point2 b, Point2 center, Poly.EFlags flags) {
   public override string ToString ()
      => IsArc
         ? $"ARC {A} .. {B}, {Center} {IsCCW}"
         : $"LINE {A} .. {B}";

   // Properties ---------------------------------------------------------------
   public readonly Point2 A = a;
   public readonly Point2 B = b;
   public readonly Point2 Center = center;
   public readonly Poly.EFlags Flags = flags;

   /// <summary>The subtended angle (+ for CCW, - for CW arcs)</summary>
   public double AngSpan {
      get {
         if (!IsArc) return 0;
         var (sa, ea) = GetStartAndEndAngles ();
         return ea - sa;
      }
   }

   /// <summary>The Bound of this segment</summary>
   public Bound2 Bound {
      get {
         Bound2 bound = new (A.X, A.Y, B.X, B.Y);
         if (IsArc) {
            // Here, we are repeating the code that is in GetLie because
            // Segment.Bound is a time-critical routine, and we don't want to
            // compute stuff like GetStartAndEndAngles 4 times
            var (sa, ea) = GetStartAndEndAngles ();
            double oppMid = (sa + ea) / 2 + Lib.PI * (IsCCW ? 1 : -1);
            for (EDir dir = EDir.E; dir <= EDir.S; dir++) {
               Point2 pt = Center.CardinalMoved (Radius, dir);
               double ang = Center.AngleTo (pt);
               if (IsCCW) {
                  if (ang < sa) ang += Lib.TwoPI;
                  if (ang > oppMid) ang -= Lib.TwoPI;
               } else {
                  if (ang > sa) ang -= Lib.TwoPI;
                  if (ang < oppMid) ang += Lib.TwoPI;
               }
               double lie = (ang - sa) / (ea - sa);
               if (lie is >= 0 and <= 1) bound += pt;
            }
         }
         return bound;
      }
   }

   /// <summary>Computes the bounding rectangle of the Seg, under a given transform</summary>
   public Bound2 GetBound (Matrix2 xfm) {
      Bound2 bound = new ([A * xfm, B * xfm]);
      if (IsArc) {
         // Some repeated code here again because of the time-critical nature
         // of the Bound code - we don't want to keep that routine as tight as possible
         double dAngle = (Vector2.XAxis * xfm).Heading;
         var (sa, ea) = GetStartAndEndAngles ();
         (sa, ea) = (sa + dAngle, ea + dAngle);
         double oppMid = (sa + ea) / 2 + Lib.PI * (IsCCW ? 1 : -1);
         for (EDir dir = EDir.E; dir <= EDir.S; dir++) {
            Point2 pt = Center.CardinalMoved (Radius, dir);
            double ang = Center.AngleTo (pt);
            if (IsCCW) {
               if (ang < sa) ang += Lib.TwoPI;
               if (ang > oppMid) ang -= Lib.TwoPI;
            } else {
               if (ang > sa) ang -= Lib.TwoPI;
               if (ang < oppMid) ang += Lib.TwoPI;
            }
            double lie = (ang - sa) / (ea - sa);
            if (lie is >= 0 and <= 1) bound += pt * xfm;
         }
      }
      return bound;
   }

   /// <summary>Is this a curved segment?</summary>
   public bool IsArc => (Flags & (Poly.EFlags.CW | Poly.EFlags.CCW)) != 0;
   /// <summary>Is this a line segment?</summary>
   public bool IsLine => (Flags & (Poly.EFlags.CW | Poly.EFlags.CCW)) == 0;
   /// <summary>If curved, does this curve CCW</summary>
   public bool IsCCW => (Flags & Poly.EFlags.CCW) != 0;
   /// <summary>Is this a full-circle segment</summary>
   public bool IsCircle => (Flags & Poly.EFlags.Circle) != 0;
   /// <summary>Is this the last segment in a Poly</summary>
   public bool IsLast => (Flags & Poly.EFlags.Last) != 0;

   /// <summary>The length of the segment</summary>
   public double Length {
      get {
         if (IsArc) return Radius * Abs (AngSpan);
         return A.DistTo (B);
      }
   }

   /// <summary>Gets the slope at the middle of the line / arc</summary>
   public double Slope => GetSlopeAt (0.5);

   /// <summary>The radius (of a curved segment)</summary>
   public double Radius => IsArc ? Center.DistTo (A) : 0;

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
      if (IsArc) {
         var (sa, ea) = GetStartAndEndAngles ();
         double radius = Radius;
         int cSteps = Lib.GetArcSteps (radius, Abs (ea - sa), threshold);
         double angstep = (ea - sa) / cSteps;
         for (int j = 1; j <= cSteps; j++)
            pts.Add (Center.Polar (radius, sa += angstep));
      } else
         pts.Add (B);
   }

   /// <summary>Returns the closest distance of the given point pt to this seg</summary>
   /// If the distance is more than the cutoff distance, this does not return
   /// the exact distance, but a conservative distance (greater than actual distance)
   public double GetDist (Point2 p, double cutoff) {
      if (IsArc) {
         double dist = Abs (Center.DistTo (p) - Radius);
         if (dist >= cutoff) return dist;
         double lie = GetLie (p).Clamp ();
         return p.DistTo (GetPointAt (lie));
      } else {
         double dist = p.DistToLine (A, B);
         if (dist >= cutoff) return dist;
         return p.DistToLineSeg (A, B);
      }
   }

   public double GetDist (Point2 p) => GetDist (p, 1e99);

   /// <summary>Returns the lie of a given point</summary>
   public double GetLie (Point2 p) {
      if (IsArc) {
         var (sa, ea) = GetStartAndEndAngles ();
         double ang = Center.AngleTo (p);
         if (IsCCW) {
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
      return p.GetLieOn (A, B);
   }

   /// <summary>Returns a point at a given lie on the segment</summary>
   public Point2 GetPointAt (double lie) {
      if (IsArc) {
         var (ang1, ang2) = GetStartAndEndAngles ();
         return Center.Polar (Radius, lie.Along (ang1, ang2));
      }
      return lie.Along (A, B);
   }

   /// <summary>Returns the tangential slope at a particular point of the segment</summary>
   public double GetSlopeAt (double lie) {
      if (IsArc) {
         var (ang1, ang2) = GetStartAndEndAngles ();
         double ang = ang1 * (1 - lie) + ang2 * lie;
         if (IsCCW) ang += Lib.HalfPI; else ang -= Lib.HalfPI;
         return Lib.NormalizeAngle (ang);
      }
      return A.AngleTo (B);
   }

   /// <summary>Get the start and end angles (only for a curved segment)</summary>
   public (double S, double E) GetStartAndEndAngles () {
      double s = Center.AngleTo (A), e = Center.AngleTo (B);
      if (IsCircle)
         e = s + Lib.TwoPI * (IsCCW ? 1 : -1);
      else {
         if (IsCCW && e < s) e += Lib.TwoPI;
         if (!IsCCW && e > s) e -= Lib.TwoPI;
      }
      return (s, e);
   }

   /// <summary>Returns true if the given point is to the 'left' of this segment</summary>
   public bool IsPointOnLeft (Point2 pt) {
      if (IsArc) {
         double dist = Center.DistTo (pt);
         return (dist > Radius) ^ IsCCW;
      } else
         return pt.LeftOf (A, B);
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
}
#endregion
#endif
