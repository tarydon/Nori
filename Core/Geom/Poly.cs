// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Poly.cs
// ║║║║╬║╔╣║ Implements the Poly class (polyline), and the Seg class (segment)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Math;

#region class Poly ---------------------------------------------------------------------------------
/// <summary>Represents a polyline (composed of lines, and arcs)</summary>
public class Poly {
   // Constructor --------------------------------------------------------------
   internal Poly (ImmutableArray<Point2> pts, ImmutableArray<Extra> extra, EFlags flags)
      => (mPts, mExtra, mFlags) = (pts, extra, flags);

   /// <summary>Make a full-circle Poly</summary>
   public static Poly Circle (Point2 pt, double radius) {
      Point2 a = pt + new Vector2 (radius, 0);
      return new Poly ([a], [new Extra (pt, EFlags.CCW | EFlags.Circle)], EFlags.Closed | EFlags.HasArcs | EFlags.Circle);
   }
   /// <summary>Make a full-circle Poly</summary>
   public static Poly Circle (double x, double y, double radius)
      => Circle (new (x, y), radius);

   /// <summary>Make a single-line Poly</summary>
   public static Poly Line (Point2 pt, Point2 pt2) 
      => new ([pt, pt2], [], 0);
   /// <summary>Make a single-line Poly</summary>
   public static Poly Line (double x1, double y1, double x2, double y2)
      => Line (new (x1, y1), new (x2, y2));

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// When we do ToString on a Pline, we get an encoding of that Pline in a mini-language.
   /// This converts that encoding back into a Pline. Note that this is, in general, not a
   /// good round-tripping mechanism since the encoded string is restricted to an accuracy
   /// of Lib.Epsilon. Here are the tags that are supported.
   /// 
   /// Tag           | Meaning
   /// --------------|-----------
   /// Mx,y          | Move to x,y (must be used as the first tag)
   /// Lx,y          | Line to x,y
   /// Hx            | Horizontal line to x
   /// Vy            | Vertical line to y
   /// Qx,y,t        | Arc to x,y with t quarter-turns. t=1 is 90 degree left turn, t=-0.5 is 45 degree right turn etc.
   /// Z             | Close the pline (can be used only at the end)
   public static Poly Parse (string s) => new PolyBuilder ().Build (s);

   /// <summary>Makes a full-rectangle Poly</summary>
   public static Poly Rectangle (Bound2 bound) {
      var (m, w, h) = (bound.Midpoint, bound.Width / 2, bound.Height / 2);
      var pb = new PolyBuilder ();
      pb.Line (m.X - w, m.Y - h).Line (m.X + w, m.Y - h).Line (m.X + w, m.Y + h).Line (m.X - w, m.Y + h);
      return pb.Close ().Build ();
   }
   /// <summary>Makes a full-rectangle Poly</summary>
   public static Poly Rectangle (double x0, double y0, double x1, double y1) {
      var pb = new PolyBuilder ();
      Lib.Sort (ref x0, ref x1); Lib.Sort (ref y0, ref y1);
      pb.Line (x0, y0).Line (x1, y0).Line (x1, y1).Line (x0, y1);
      return pb.Close ().Build ();
   }

   /// <summary>Converts a string to a path-description (similar to the format used by Flux)</summary>
   public override string ToString () {
      var sb = new StringBuilder ();
      Point2 a = A, b;
      foreach (var seg in Segs) {
         b = seg.B;
         if (IsCircle) return $"C{seg.Center.X.R6 ()},{seg.Center.Y.R6 ()},{seg.Radius.R6 ()}";
         if (sb.Length == 0) sb.Append ($"M{a.X.R6 ()},{a.Y.R6 ()}");
         if (seg.IsArc) {
            double t = seg.AngSpan / (PI / 2);   // Number of quarter turns
            sb.Append ($"Q{b.X.R6 ()},{b.Y.R6 ()},{t.R6 ()}");
         } else {
            if (!(seg.IsLast && IsClosed)) {
               if (a.X.EQ (b.X)) sb.Append ($"V{b.Y.R6 ()}");
               else if (a.Y.EQ (b.Y)) sb.Append ($"H{b.X.R6 ()}");
               else sb.Append ($"L{b.X.R6 ()},{b.Y.R6 ()}");
            }
         }
         if (seg.IsLast && IsClosed) { sb.Append ('Z'); break; }
         a = b;
      }
      return sb.ToString ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Start point of the Poly</summary>
   public Point2 A => mPts[0];
   /// <summary>End point of the Poly (same as start point for a closed Poly)</summary>
   public Point2 B => mPts[IsClosed ? 0 : ^1];

   /// <summary>The count of _segments_ in this Poly</summary>
   public int Count => mPts.Length - (IsClosed ? 0 : 1);
   /// <summary>Does this Poly have any arcs?</summary>
   public bool HasArcs => (mFlags & EFlags.HasArcs) != 0;
   /// <summary>Is this a 'closed' Poly?</summary>
   public bool IsClosed => (mFlags & EFlags.Closed) != 0;
   /// <summary>Is this a full circle</summary>
   public bool IsCircle => (mFlags & EFlags.Circle) != 0;
   /// <summary>Is this a single line Poly?</summary>
   public bool IsLine => Count == 1 && !HasArcs;
   /// <summary>Is this an 'open' Poly?</summary>
   public bool IsOpen => (mFlags & EFlags.Closed) == 0;

   /// <summary>The set of nodes of this Poly</summary>
   public ImmutableArray<Point2> Pts => mPts;
   readonly ImmutableArray<Point2> mPts;

   /// <summary>Enumerates the segments in the Poly</summary>
   public IEnumerable<Seg> Segs {
      get {
         for (int i = 0, n = Count; i < n; i++) 
            yield return this[i];
      }
   }

   /// <summary>Returns the Nth segment from the Pline</summary>
   public Seg this[int i] {
      get {
         int n = Count;
         EFlags flags = (i == n - 1) ? EFlags.Last : 0;
         Point2 a = mPts[i], b = mPts[(i + 1) % mPts.Length];
         if (HasArcs && i < mExtra.Length) {
            var extra = mExtra[i];
            return new (a, b, extra.Center, extra.Flags | flags);
         } else
            return new (a, b, Point2.Zero, flags);
      }
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Discretizes the pline with a given error threshold into the given set of points</summary>
   public void Discretize (List<Point2> pts, double threshold) {
      if (!HasArcs) pts.AddRange (mPts);
      else {
         pts.Add (A);
         foreach (var seg in Segs) seg.Discretize (pts, threshold);
         if (IsClosed) pts.RemoveLast ();
      }
   }

   /// <summary>Computes the bounding rectangle of the Poly (not cached)</summary>
   public Bound2 GetBound () => new (Segs.Select (a => a.Bound));

   /// <summary>Computes the length of the Poly</summary>
   public double GetPerimeter () => Segs.Sum (a => a.Length);

   // Operators ----------------------------------------------------------------
   /// <summary>Create a new Poly by applying the transformation matrix</summary>
   public static Poly operator * (Poly p, Matrix2 xfm) {
      var pts = p.Pts.Select (a => a * xfm).ToImmutableArray ();
      var extra = ImmutableArray<Extra>.Empty;
      if (p.HasArcs) extra = p.mExtra.Select (a => a * xfm).ToImmutableArray ();
      return new Poly (pts, extra, p.mFlags);
   }

   // Nested types -------------------------------------------------------------
   [Flags]
   public enum EFlags : ushort {
      Closed = 1, HasArcs = 2,
      CW = 4, CCW = 8, Circle = 16, Last = 32
   }
   EFlags mFlags;

   /// <summary>Addition information stored for curved segments (center point, winding)</summary>
   internal readonly struct Extra (Point2 center, EFlags flags) {
      public readonly Point2 Center = center;
      public readonly EFlags Flags = flags;

      public static Extra operator * (Extra e, Matrix2 xfm) => new (e.Center * xfm, e.Flags);
   }
   /// <summary>This array is populated only if the Poly has any arcs</summary>
   readonly ImmutableArray<Extra> mExtra;
}
#endregion

#region class PolyBuilder --------------------------------------------------------------------------
/// <summary>Helper used to build Poly objects (since they are immutable once created)</summary>
public class PolyBuilder {
   // Methods ------------------------------------------------------------------
   /// <summary>Adds an Arc starting at the given point a and with center cen</summary>
   public PolyBuilder Arc (Point2 a, Point2 cen, Poly.EFlags flags) {
      PopBulge (a);
      while (mExtra.Count < mPts.Count) mExtra.Add (new ());
      mPts.Add (a); mExtra.Add (new (cen, flags));
      return this;
   }
   /// <summary>Adds an arc given starting point and center</summary>
   public PolyBuilder Arc (double x, double y, double xc, double yc, Poly.EFlags flags)
      => Arc (new (x, y), new (xc, yc), flags);

   /// <summary>Adds an arc given the starting point and DXF-style bulge</summary>
   public PolyBuilder Arc (Point2 a, double bulge) {
      PopBulge (a); mPts.Add (a); mBulge = bulge;
      return this;
   }
   /// <summary>Adds an arc given the starting point and DXF-style bulge</summary>
   public PolyBuilder Arc (double x, double y, double bulge) 
      => Arc (new (x, y), bulge);

   /// <summary>This is called finally to complete the build process to a Poly</summary>
   public Poly Build () {
      PopBulge (mPts[0]);
      Poly.EFlags flags = mClosed ? Poly.EFlags.Closed : 0;
      var extra = ImmutableArray<Poly.Extra>.Empty;
      if (mExtra.Count > 0) {
         extra = ImmutableArray.CreateRange (mExtra);
         flags |= Poly.EFlags.HasArcs;
      }
      var poly = new Poly (ImmutableArray.CreateRange (mPts), extra, flags);
      Reset ();
      return poly;
   }

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// See Poly.Parse for details
   internal Poly Build (string s) {
      var (mode, n) = ('M', 0);
      Point2 a = Point2.Zero;
      for (; ; ) {
         char ch = GetMode ();
         switch (ch) {
            case 'M': a = GetP (); break;
            case 'L': Line (a); a = GetP (); break;
            case 'H': Line (a); a = new (GetD (), a.Y); break;
            case 'V': Line (a); a = new (a.X, GetD ()); break;
            case 'Z': Line (a); Close (); return Build ();
            case '.': Line (a); return Build ();
            case 'Q':
               var (b, q) = (GetP (), GetD ());    // q is the number of quarter-turns
               if (q.IsZero ()) {
                  Line (a); a = b;
               } else {
                  double opp = a.DistTo (b) / 2, slope = a.AngleTo (b);
                  double adj = opp / Tan (q * Lib.QuarterPI);
                  Point2 cen = a.Polar (opp, slope).Polar (adj, slope + Lib.HalfPI);
                  Arc (a, cen, q > 0 ? Poly.EFlags.CCW : Poly.EFlags.CW);
                  a = b;
               }
               break;
            default: throw new NotImplementedException ();
         }
      }

      // Helpers ...........................................
      // Read the current mode character (like M, L, V, H etc). Since repeated modes can 
      // be elided, this simply returns the 'current mode' if we see a number instead
      char GetMode () {
         while (n < s.Length) {
            char ch = s[n++];
            if (IsSpace (ch)) continue;
            if (char.IsLetter (ch)) return mode = char.ToUpper (ch);
            n--; return mode;
         }
         return '.';
      }

      // Expecting two doubles (separated by whitespace or commas) to make a Point
      Point2 GetP () => new (GetD (), GetD ());

      // Expecting a double, prefixed possibly by whitespace
      double GetD () {
         while (n < s.Length && IsSpace (s[n])) n++;
         int start = n;
         while (n < s.Length) {
            char ch = s[n++];
            if (IsSpace (ch) || (char.IsLetter (ch) && ch != 'E' && ch != 'e')) { n--; break; }
         }
         return s[start..n].ToDouble ();
      }

      // Treat commas like spaces
      static bool IsSpace (char ch) => char.IsWhiteSpace (ch) || ch == ',';
   }

   /// <summary>Marks the Pline as closed</summary>
   public PolyBuilder Close () { mClosed = true; return this; }

   /// <summary>Adds the given end-point as the last node, makes a Poly and returns it</summary>
   public Poly End (Point2 e) { Line (e); return Build (); }
   /// <summary>Adds the given end-point as the last node, makes a Poly and returns it</summary>
   public Poly End (double x, double y) => End (new (x, y));

   /// <summary>Add a line starting at the given point</summary>
   public PolyBuilder Line (Point2 a) { PopBulge (a); mPts.Add (a); return this; }
   /// <summary>Add a line starting at the given point</summary>
   public PolyBuilder Line (double x, double y) => Line (new (x, y));

   // Helpers ------------------------------------------------------------------
   void PopBulge (Point2 b) {
      // The bulge is the tangent of one quarter of the turn angle
      if (!mBulge.IsNaN ()) {
         double bulge = mBulge; mBulge = double.NaN;
         if (bulge > 1e6 || bulge.IsZero ()) return;  // Only a Line

         bool ccw = bulge > 0; bulge = Abs (bulge);
         bool large = bulge > 1;
         double shift = large ? 1 / Tan (PI - Atan (bulge) * 2) : 1 / Tan (Atan (bulge) * 2);
         if (large == ccw) shift = -shift;
         Point2 a = mPts.RemoveLast ();
         double dx = (b.X - a.X) / 2, dy = (b.Y - a.Y) / 2;
         Point2 cen = new (a.X + dx - dy * shift, a.Y + dy + dx * shift);
         Arc (a, cen, ccw ? Poly.EFlags.CCW : Poly.EFlags.CW);
      }
   }

   void Reset () {
      mPts.Clear (); mExtra.Clear (); mClosed = false; mBulge = double.NaN;
   }

   // Private data -------------------------------------------------------------
   List<Point2> mPts = [];
   List<Poly.Extra> mExtra = [];
   double mBulge = double.NaN;
   bool mClosed;
}
#endregion

#region struct Seg ---------------------------------------------------------------------------------
/// <summary>Represents a single Segment of a Poly (can be a single line or an arc)</summary>
public readonly struct Seg (Point2 a, Point2 b, Point2 center, Poly.EFlags flags) {
   public override string ToString () {
      if (IsArc) return $"ARC {A} .. {B}, {Center} {IsCCW}";
      return $"LINE {A} .. {B}";
   }

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

   /// <summary>Is this a curved segment?</summary>
   public bool IsArc => (Flags & (Poly.EFlags.CW | Poly.EFlags.CCW)) != 0;
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
      } else
         return p.GetLieOn (A, B);
   }

   /// <summary>Returns a point at a given lie on the segment</summary>
   public readonly Point2 GetPointAt (double lie) {
      if (IsArc) {
         var (ang1, ang2) = GetStartAndEndAngles ();
         return Center.Polar (Radius, lie.Along (ang1, ang2));
      }
      return lie.Along (A, B);
   }

   /// <summary>Returns the tangential slope at a particular point of the segment</summary>
   public readonly double GetSlopeAt (double lie) {
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
