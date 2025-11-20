// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Poly.cs
// ║║║║╬║╔╣║ Implements the Poly class (polyline), and the Seg class (segment)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
using System.Buffers;
namespace Nori;
using static Geo;
using static Lib;

#region class Poly ---------------------------------------------------------------------------------
/// <summary>Represents a polyline (composed of lines, and arcs)</summary>
[AuPrimitive]
public partial class Poly {
   // Constructor --------------------------------------------------------------
   [Used] Poly () { }
   internal Poly (ImmutableArray<Point2> pts, ImmutableArray<ArcInfo> extra, EFlags flags)
      => (mPts, Extra, mFlags) = (pts, extra, flags);

   /// <summary>Make a single-arc Poly</summary>
   public static Poly Arc (Point2 center, double radius, double startAngle, double endAngle, bool ccw) {
      Point2 a = center.Polar (radius, startAngle), b = center.Polar (radius, endAngle);
      return new Poly ([a, b], [new ArcInfo (center, ccw ? EFlags.CCW : EFlags.CW)], EFlags.HasArcs);
   }

   /// <summary>Make a single-arc Poly</summary>
   public static Poly Arc (Point2 start, double startTangentAngle, Point2 end) {
      Point2 tangentPt = start.Polar (100, startTangentAngle); // An arbitrary tangent point
      int arcDir = end.Side (start, tangentPt); // CW (-1), CCW (+1), Line (0)

      // The center of the arc is the intersection of the below two lines:
      // Line 1: Line perpendicular to the tangent (i.e the normal)
      Point2 normalEndPt = start.Polar (100, startTangentAngle + HalfPI);
      // Line 2: Perpendicular bisector of the chord connecting start and end point
      Point2 mid = start.Midpoint (end);
      Point2 bisectorEndPt = mid.Polar (100, start.AngleTo (end) + HalfPI);
      var cen = LineXLine (start, normalEndPt, mid, bisectorEndPt);

      var (sa, ea) = (cen.AngleTo (start), cen.AngleTo (end));
      if (arcDir < 0) while (ea > sa) ea -= TwoPI;
      else while (ea < sa) ea += TwoPI;

      // If the end point lies along the tangent, return a line from start to end.
      if (arcDir == 0 || cen.IsNil) return Line (start, end);
      return Arc (cen, cen.DistTo (start), sa, ea, arcDir > 0);
   }

   /// <summary>Make a full-circle Poly</summary>
   public static Poly Circle (Point2 pt, double radius) {
      Point2 a = pt + new Vector2 (radius, 0);
      return new Poly ([a], [new ArcInfo (pt, EFlags.CCW | EFlags.Circle)], EFlags.Closed | EFlags.HasArcs | EFlags.Circle);
   }

   /// <summary>Make a single-line Poly</summary>
   public static Poly Line (Point2 pt, Point2 pt2)
      => new ([pt, pt2], [], 0);
   /// <summary>Make a single-line Poly</summary>
   public static Poly Line (double x1, double y1, double x2, double y2)
      => Line (new (x1, y1), new (x2, y2));

   /// <summary>Make a multi-segment PolyLine</summary>
   public static Poly Lines (IEnumerable<Point2> points)
      => new ([.. points], [], EFlags.Closed);

   /// <summary>Create a polygon of given size at a given center, sides and rotation angle.</summary>
   public static Poly Polygon (Point2 cen, double radius, int sides, double angle = 0)
      => Lines (Enumerable.Range (0, sides).Select (i => cen.Polar (radius, angle + HalfPI + TwoPI * i / sides)));

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
      Sort (ref x0, ref x1); Sort (ref y0, ref y1);
      pb.Line (x0, y0).Line (x1, y0).Line (x1, y1).Line (x0, y1);
      return pb.Close ().Build ();
   }

   /// <summary>Converts a string to a path-description (similar to the format used by Flux)</summary>
   public override string ToString () {
      UTFWriter w = new (); Write (w);
      return Encoding.UTF8.GetString (w.Trimmed ());
   }

   void Write (UTFWriter w) {
      Point2 a = A;
      bool first = true;
      foreach (var seg in Segs) {
         if (first) {
            if (IsCircle) {
               w.Write ('C').Write (seg.Center.X.R6 ()).Write (',').
                  Write (seg.Center.Y.R6 ()).Write (',').Write (seg.Radius.R6 ());
               return;
            }
            w.Write ('M').Write (a.X.R6 ()).Write (',').Write (a.Y.R6 ());
            first = false;
         }
         Point2 b = seg.B;
         if (seg.IsArc) {
            double t = seg.AngSpan / (PI / 2);
            w.Write ('Q').Write (b.X.R6 ()).Write (',').Write (b.Y.R6 ()).Write (',').Write (t.R6 ());
         } else {
            if (!(seg.IsLast && IsClosed)) {
               if (a.X.EQ (b.X)) w.Write ('V').Write (b.Y.R6 ());
               else if (a.Y.EQ (b.Y)) w.Write ('H').Write (b.X.R6 ());
               else w.Write ('L').Write (b.X.R6 ()).Write(',').Write (b.Y.R6 ());
            }
         }
         a = b;
      }
      if (IsClosed) w.Write ('Z');
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
   /// The poly is considered as a looped collection, so a value outside the range
   /// of segs in the poly is automatically wrapped around (thus, this[Count] is
   /// the same as this[0] for a closed poly, and this[-1] returns the last
   /// segment).
   public Seg this[int i] {
      get {
#if OLDSEG
         int n = Count; i = i.Wrap (n);
         EFlags flags = (i == n - 1) ? EFlags.Last : 0;
         Point2 a = mPts[i], b = mPts[(i + 1) % mPts.Length];
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
            return new (a, b, extra.Center, extra.Flags | flags);
         }
         return new (a, b, Point2.Zero, flags);
#else
         return new (this, i);
#endif
      }
   }

   // Methods ------------------------------------------------------------------
   public Poly Clean ()
      => TryCleanup (out var tmp) ? tmp : this;

   /// <summary>Returns a 'closed' version of this Poly</summary>
   /// If the start and end points are touching (within 1e-6), the end point is 'merged' with
   /// the start point. Otherwise, a line segment is drawn from the end point to the start point
   /// closing the Poly (regardless of their gap)
   public Poly Close (double threshold = 1e-6) {
      if (IsClosed || mPts.Length < 2) return this;
      var flags = mFlags | EFlags.Closed;
      if (mPts[0].EQ (mPts[^1], threshold)) {
         if (HasArcs && mPts.Length <= 3) {
            var seg0 = this[0];
            switch (mPts.Length) {
               case 2:
                  if (seg0.Length < threshold * 2) return this;
                  return Circle (seg0.Center, seg0.Radius);
               case 3:
                  var seg1 = this[1];
                  if (seg1.Center.EQ (seg0.Center) && seg1.Radius.EQ (seg0.Radius))
                     return Circle (seg0.Center, seg0.Radius);
                  break;
            }
         }
         if (mPts[0].EQ (mPts[^1])) {
            ImmutableArray<Point2> pts = [.. mPts.Take (mPts.Length - 1)];
            if (!HasArcs) return new (pts, [], flags);
            ImmutableArray<ArcInfo> extra = [.. Extra.Take (pts.Length)];
            return new (pts, extra, flags);
         }
         Point2 pt = GetTipIntersection (this, this, threshold);
         if (!pt.IsNil) {
            ImmutableArray<Point2> pts = [.. mPts.Skip (1).Take (mPts.Length - 2), pt];
            if (!HasArcs) return new (pts, [], flags);
            ImmutableArray<ArcInfo> extra = [.. Extra.Skip (1).Take (pts.Length)];
            return new (pts, extra, flags);
         }
      }
      return new (mPts, Extra, flags);
   }

   /// <summary>Discretizes the pline with a given error threshold into the given set of points</summary>
   /// <param name="pts">The output is added to this list of points (existing points here are not disturbed)</param>
   /// <param name="threshold">Maximum lateral deviation between the chord approximation and the arc</param>
   /// <param name="angleLimit">Maximum turn angle between successive chord segments in an arc</param>
   /// The number of segments each arc is discretized into depends on both the threshold (linear deviation
   /// between the chord and the original arc) and the angleLimit (turn angle between successive chords
   /// approximating the arc) - the tighter of those two constraints will eventually determine the number
   /// of segments each arc gets divided into.
   public void Discretize (List<Point2> pts, double threshold, double angleLimit) {
      if (!HasArcs) pts.AddRange (mPts);
      else {
         pts.Add (A);
         foreach (var seg in Segs) seg.Discretize (pts, threshold, angleLimit);
         if (IsClosed) pts.RemoveLast ();
      }
   }

   /// <summary>Returns the index of the closest node</summary>
   public int GetClosestNode (Point2 pt) => mPts.MinIndexBy (a => a.DistToSq (pt));

   /// <summary>Composes a 'Logo code' description of this Poly, starting with the longest seg</summary>
   /// This is a description of this Poly, as a series of 'moves' and 'turns'. The point
   /// is that this description is independent of the position and orientation of the
   /// shape, and so can be used as an input for the ShapeRecognizer.
   ///
   /// We call it 'Logo' code since it is similar to the move and turn based description
   /// used in the Logo programming language to control the 'turtle':
   /// https://en.wikipedia.org/wiki/Turtle_graphics.
   /// For example, a description of a 10x5 rectangle could be like:
   /// "F10 L F5 L F10 L F5 L ."
   ///
   /// We start with the longest straight line segment, and use the following codes to
   /// represent each linear or curved segment, and each of the 'turns' at the corners.
   /// All numerical values in the code are rounded using the 'decimals' setting.
   /// - For a linear segment, append "F{length}"
   /// - For a curved segment, we use the codes G for CCW arcs, and D for CW arcs. These
   ///   letters come from the french terms 'Gauche (left)' or 'Droite (right)' indicating
   ///   the arc turns we are making. For a 90 degree left or right turn, we output simply
   ///   "G{radius}" or "D{radius}" respectively. Otherwise, we output "G{radius},{angle}"
   ///   or "D{radius},{angle}", where angle is the turn angle in degrees.
   /// - At each 'corner' between two segments, we have a left or right turn and we use
   ///   the letters L and R to indicate these. If we have a 90 degree turn, we simply output
   ///   "L" or "R". Otherwise, we output "L{angle}" or "R{angle}" where angle is turn angle
   ///   at the corner, in degrees. If the corner is a tangential corner, then we don't output
   ///   either L or R.
   public (int Seg, string Desc) GetLogoCode (int decimals) {
      if (IsCircle || IsOpen) return (0, "");
      var segs = Segs.ToList ();
      int longest = segs.MaxIndexBy (a => a.IsArc ? 0 : a.Length);
      if (segs[longest].IsArc) return (0, "");

      var sb = new StringBuilder ();
      for (int i = 0; i < segs.Count; i++) {
         Seg seg = segs[(i + longest) % segs.Count];
         // For a line segment, append "F{length}"
         if (seg.IsLine) sb.Append ($"F{seg.Length.Round (decimals)} ");
         else {
            // Output 'G' for CCW, and 'D' for CW arcs. The radius is always included,
            // but the angle is included only if it is not 90 degrees
            double ang = seg.AngSpan.R2D ().Round (decimals), rad = seg.Radius.Round (decimals);
            sb.Append (ang >= 0 ? 'G' : 'D').Append (rad);
            ang = Abs (ang);
            if (!ang.EQ (90)) sb.Append ($",{ang}");
            sb.Append (' ');
         }

         // Now compute the turn angle at the corner, if this is not a tangential corner.
         // For a 90 degree turn, we don't output the angle.
         double turn = GetTurnAngle (i + 1);
         if (!turn.IsZero ()) {
            sb.Append (turn >= 0 ? 'L' : 'R');
            turn = Abs (turn);
            if (!turn.EQ (HalfPI)) sb.Append (turn.R2D ().Round (decimals));
            sb.Append (' ');
         }
      }
      sb.Append ('.');
      return (longest, sb.ToString ());
   }

   /// <summary>Gets the closest distance of this Poly to the given point</summary>
   /// This also returns the closest segment and the closest node on that segment
   /// to the given point. Note that this means that we first pick the closest segment,
   /// and then pick if the closest node is the start or end of that segment. So
   /// Node will either be Seg, or Seg+1 always.
   public (double Dist, int Seg) GetDistance (Point2 pt) {
      var (minDist, nSeg) = (1e99, 0);
      for (int i = Count - 1; i >= 0; i--) {
         Seg seg = this[i];
         double dist = seg.GetDist (pt, minDist);
         if (dist < minDist) (minDist, nSeg) = (dist, i);
      }
      return (minDist, nSeg);
   }

   /// <summary>Computes the bounding rectangle of the Poly (not cached)</summary>
   public Bound2 GetBound () => new (Segs.Select (a => a.Bound));

   /// <summary>Computes the bounding rectangle of the Poly, under a given transform</summary>
   public Bound2 GetBound (Matrix2 xfm) => new (Segs.Select (a => a.GetBound (xfm)));

   /// <summary>Computes the length of the Poly</summary>
   public double GetPerimeter () => Segs.Sum (a => a.Length);

   /// <summary>This returns the 'turn angle' at a particular node</summary>
   /// This is how much the pline 'turns' at this node. The angle returned is from -PI .. +PI, and
   /// +ve values mean a left turn. If 0 is returned, then this is a tangential corner
   public double GetTurnAngle (int nNode) {
      int cSegs = Count;
      double outAngle = this[nNode.Wrap (cSegs)].GetSlopeAt (0);
      double inAngle = this[(nNode - 1).Wrap (cSegs)].GetSlopeAt (1);
      return NormalizeAngle (outAngle - inAngle);
   }

   /// <summary>Returns the winding of the Poly</summary>
   public EWinding GetWinding () {
      if (IsOpen) return EWinding.Indeterminate;
      if (IsCircle) return this[0].IsCCW ? EWinding.CCW : EWinding.CW;
      int node = mPts.MinIndexBy (pt => pt.Y);
      var pp = this[(node - 1 + Count) % Count].GetPointAt (0.9);
      var pn = this[(node + 1) % Count].GetPointAt (0.1);
      if (pp.X.EQ (pn.X)) return EWinding.Indeterminate;
      return pp.X < pn.X ? EWinding.CCW : EWinding.CW;
   }

   /// <summary>Checks for a rectangular Poly</summary>
   public bool IsRectangle () {
      if (!(Count == 4 && IsClosed) || HasArcs) return false;
      for (int i = 0; i < 4; i++) if (!GetTurnAngle (i).EQ (HalfPI)) return false;
      return true;
   }

   public bool TryCleanup ([NotNullWhen (true)] out Poly? result, double threshold = 1e-6) {
      if (IsCircle || mPts.Length < 2) { result = null; return false; }
      if (TryCleanupZeroSegs (out result, threshold) && result.Count == 0) return true; // Empty poly!
      if ((result ?? this).TryMergeConsecutiveSegs (out Poly? result2, threshold))
         result = result2;
      return result != null;
   }

   /// <summary>Attempts to cleanup given poly of any zero-length segs</summary>
   bool TryCleanupZeroSegs ([NotNullWhen (true)] out Poly? result, double threshold = 1e-6) {
      // Quick check
      bool gotZero = false;
      for (int i = 0, limit = mPts.Length - 1; i < limit; i++)
         if (mPts[i].EQ (mPts[i + 1], threshold)) { gotZero = true; break; }
      if (!gotZero) { result = null; return false; } // No zero-length segs found

      HashSet<int> skipIdxs = []; // Using mark 'n sweep to cleanup zero-length segs
      for (int i = 0, limit = mPts.Length - 1; i < limit; i++) {
         if (mPts[i].EQ (mPts[i + 1], threshold))
            skipIdxs.Add (i);
      }

      // Cleanup zero-length segs
      List<ArcInfo> extra = [];
      if (HasArcs) { // Cleanup the zero-length seg's extras
         foreach (var idx in Enumerable.Range (0, Extra.Length)) {
            if (idx >= Extra.Length) break;
            if (skipIdxs.Contains (idx)) continue;
            extra.Add (Extra[idx]);
         }
      }
      var flags = mFlags;
      if (extra.Count == 0 || extra.Any (e => (e.Flags & EFlags.Arc) != 0))
         flags &= ~EFlags.HasArcs;
      // Remove dup points
      var pts = mPts.Select ((pt, idx) => (pt, idx)).Where (a => !skipIdxs.Contains (a.idx)).Select (a => a.pt);
      result = new ([.. pts], [.. extra], flags);
      return true;
   }

   /// <summary>Attempts to cleanup given poly of collinear/concentric line/arc segments appearing sequentially</summary>
   bool TryMergeConsecutiveSegs ([NotNullWhen (true)] out Poly? result, double threshold = 1e-6) {
      if (Count < 2 || !NeedMerge (this, threshold)) { result = null; return false; }
      (Seg prev, int baseSegIdx) = (this[0], 0);
      (List<Point2> pts, List<ArcInfo> extras) = ([], []);
      bool mergedSegs = false;
      for (int i = 1; ; i++) {
         Seg curr = this[i];
         bool canMerge = CanMerge (prev, curr, threshold);
         mergedSegs |= canMerge;
         if (canMerge && !curr.IsLast)
            continue; // Continue gathering mergeable segs, as long as mergeability condition is satisfied.

         pts.Add (prev.A);
         if (HasArcs && baseSegIdx < Extra.Length)
            extras.Add (Extra[baseSegIdx]);

         if (curr.IsLast) {
            if (!canMerge)
               pts.Add (curr.A);
            if (!IsClosed)
               pts.Add (curr.B);
            break;
         }

         (prev, baseSegIdx) = (curr, i);
      }

      if (IsClosed && pts.Count == 1)
         result = Circle (extras[0].Center, extras[0].Center.DistTo (pts[0]));
      else
         result = mergedSegs ? new Poly ([.. pts], [.. extras], mFlags) : null;
      // Consider merging last and first segs
      var poly = result ?? this;
      if (poly is { IsClosed: true, Count: > 1 }) {
         var (last, first) = (poly[^1], poly[0]);
         _ = CanMerge (last, first, threshold) && poly.Roll (1).TryMergeConsecutiveSegs (out result, threshold);
      }
      return result != null;

      static bool CanMerge (Seg a, Seg b, double threshold) {
         if (a.IsArc != b.IsArc) return false; // Both must be either arcs or lines
         if (a.IsArc) {
            if (!a.Center.EQ (b.Center, threshold) || !a.Radius.EQ (b.Radius, threshold)) return false; // Concentric check
         } else {
            if (a.B.DistToLine (a.A, b.B) > threshold) return false; // Collinearity check
         }
         return true;
      }

      static bool NeedMerge (Poly p, double threshold) {
         Seg prev = p[0];
         for (int i = 1; i < p.Count; i++) {
            Seg curr = p[i];
            if (CanMerge (prev, curr, threshold)) return true;
            prev = curr;
         }
         return p.IsClosed && CanMerge (p[^1], p[0], threshold);
      }
   }

   // Implementation -----------------------------------------------------------
   static Poly Read (UTFReader ur) => new PolyBuilder ().Build (ur, true);

   // Operators ----------------------------------------------------------------
   /// <summary>Create a new Poly by applying the transformation matrix</summary>
   public static Poly operator * (Poly p, Matrix2 xfm) {
      if (p.IsCircle) {
         var cen = p.Extra[0].Center;
         double radius = cen.DistTo (p.A) * xfm.ScaleFactor;
         return Circle (cen * xfm, radius);
      } else {
         var pts = p.Pts.Select (a => a * xfm).ToImmutableArray ();
         ImmutableArray<ArcInfo> extra = [];
         if (p.HasArcs) extra = [.. p.Extra.Select (a => a * xfm)];
         return new Poly (pts, extra, p.mFlags);
      }
   }

   // Nested types -------------------------------------------------------------
   [Flags]
   public enum EFlags : ushort {
      Closed = 1, HasArcs = 2,
      CW = 4, CCW = 8, Circle = 16, Last = 32, Arc = CW | CCW
   }
   readonly EFlags mFlags;

   public enum EWinding {
      Unknown,        // We haven't computed the winding
      CW,             // Clockwise
      CCW,            // Counter-clockwise
      Indeterminate   // Cannot say - open poly, self-intersecting poly etc
   }

   /// <summary>Addition information stored for curved segments (center point, winding)</summary>
   internal readonly struct ArcInfo (Point2 center, EFlags flags) {
      public readonly Point2 Center = center;
      public readonly EFlags Flags = flags;

      public static readonly ArcInfo Nil = new (Point2.Nil, 0);

      public static ArcInfo operator * (ArcInfo e, Matrix2 xfm) => new (e.Center * xfm, e.Flags);
   }
   /// <summary>This array is populated only if the Poly has any arcs</summary>
   internal readonly ImmutableArray<ArcInfo> Extra;
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
      if (mClosed && mPts.Count > 1 && mPts[0].EQ (mPts[^1])) mPts.RemoveLast ();
      var extra = ImmutableArray<Poly.ArcInfo>.Empty;
      if (mExtra.Count > 0) {
         extra = [..mExtra];
         flags |= Poly.EFlags.HasArcs;
         if (extra[0].Flags.HasFlag (Poly.EFlags.Circle))
            flags |= Poly.EFlags.Circle;
      }
      var poly = new Poly ([..mPts], extra, flags);
      Reset ();
      return poly;
   }

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// See Poly.Parse for details
   internal Poly Build (string s) => Build (new UTFReader (Encoding.UTF8.GetBytes (s)));

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// See Poly.Parse for details
   internal Poly Build (UTFReader R, bool fromCurl = false) {
      var mode = 'M';
      Point2 a = Point2.Zero;
      if (R.Peek is not (byte)'M' and not (byte)'C') throw new ParseException ("Poly should start with 'M' or 'C'");
      for (; ; ) {
         char ch = GetMode ();
         switch (ch) {
            case 'C': a = GetP (); double r = GetD (); return Poly.Circle (a, r);
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
                  double adj = opp / Tan (q * QuarterPI);
                  Point2 cen = a.Polar (opp, slope).Polar (adj, slope + HalfPI);
                  Arc (a, cen, q > 0 ? Poly.EFlags.CCW : Poly.EFlags.CW);
                  a = b;
               }
               break;
            default: throw new ParseException ($"Unexpected mode '{ch}' in Poly.Parse");
         }
      }

      // Helpers ...........................................
      // Read the current mode character (like M, L, V, H etc). Since repeated modes can
      // be elided, this simply returns the 'current mode' if we see a number instead
      char GetMode () {
         if (!R.TryPeek (out var b)) return '.';
         if (fromCurl && sCurlSpl.Contains (R.Peek)) return '.';
         char ch = (char)b; if (char.IsLetter (ch)) { R.Skip (); return mode = char.ToUpper (ch); }
         return mode;
      }

      // Expecting two doubles (separated by whitespace or commas) to make a Point
      Point2 GetP () => new (GetD (), GetD ());
      // Expecting a double, prefixed possibly by whitespace
      double GetD () { R.Skip (sSpaceAndComma).Read (out double v); return v; }
   }
   static SearchValues<byte> sSpaceAndComma = SearchValues.Create (" \r\n\f\t,"u8);
   static SearchValues<byte> sCurlSpl = SearchValues.Create (" }]"u8);

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
      if (mBulge.IsNaN ()) return;
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

   void Reset () {
      mPts.Clear (); mExtra.Clear (); mClosed = false; mBulge = double.NaN;
   }

   // Property -----------------------------------------------------------------
   /// <summary>Returns true if no Poly is built</summary>
   public bool IsNull => mPts.Count == 0;

   public static PolyBuilder It => sIt ??= new ();
   [ThreadStatic]
   static PolyBuilder? sIt;

   // Private data -------------------------------------------------------------
   readonly List<Point2> mPts = [];
   readonly List<Poly.ArcInfo> mExtra = [];
   double mBulge = double.NaN;
   bool mClosed;
}
#endregion
