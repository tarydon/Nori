// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SegOld.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#if OLDSEG
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
