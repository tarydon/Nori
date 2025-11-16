// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgSnap.cs
// ║║║║╬║╔╣║ Implements the DwgSnap class, ESnap enumeration
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DwgSnap ------------------------------------------------------------------------------
/// <summary>DwgSnap provides *snap* logic to features in a drawing</summary>
/// The core method takes a point (and a snap aperture in drawing units) and returns a
/// 'snapped' point. If the input point is close to a feature like an endpoint, midpoint,
/// center point, quadrant etc then it 'snaps' to that (favoring the closest of such snap
/// points, and also weighting some types of snaps like 'endpoint' over other types like 'on').
///
/// This also maintains some state - whenever we have provided a snap like 'endpoint',
/// 'midpoint' or 'interesection', that point is remembered as an 'anchor'. (We maintain
/// a few anchors, and discard the oldest ones).
///
/// From each anchor, horizontal and vertical construction lines are implied and whenever
/// the mouse is close to them they are drawn, and can be used for an 'on' snap. Intersections
/// betweeen construction lines, or between construction lines and geometry are also snap points
/// that are generated.
///
/// When we are at the 'endpoint' of a line or arc, then there is also an implied construction
/// line in the tangent direction so we can use that to extend a line or arc along the end
/// tangent.
///
/// The DwgSnap class also maintains all the state required to draw the actual snap marker text
/// (like on / endpoint / midpoint etc) as well as to draw the construction lines themselves as
/// dotted lines.
///
/// The basic workflow for snapping is this:
/// - Call `Snap(ptRaw, aperture)` to snap a raw point and return a snapped point (if there
///   are no snaps nearby, this returns ptRaw)
/// - After this PtSnap holds the snapped point (this is also the return value from the Snap
///   method), and ESnap holds the type of snap that was applied (or ESnap.None)
/// - Read the Labels and Lines properties to get the annotations to draw to indicate the snap.
///   This includes the snap indicator square and accompanying text (like "endpoint"), as well
///   as the dotted construction lines and the anchor indicators (like "horz" or "align:35").
///
/// Thus, the DwgSnap class here implements all the actual logic of snapping in a UI independent
/// fashion, and a VNode can be trivially written to read the Labels and Lines property and draw
/// the snap indicators.
public class DwgSnap {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a DwgSnap object given the drawing to work with</summary>
   public DwgSnap (Dwg2 dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   // Properties ---------------------------------------------------------------
   /// <summary>The recent snap that we computed</summary>
   public ESnap ESnap => mSnap;
   ESnap mSnap;

   /// <summary>The set of labels we need to draw to represent the current snap situation</summary>
   public IEnumerable<(string text, Point2 Pt, bool above)> Labels {
      get {
         if (mSnap != ESnap.None)
            yield return (mSnap.ToString ().ToLower (), mPtSnap, false);
         foreach (var con in mVisible) {
            string text;
            if (con.Slope.IsZero ()) text = "horz";
            else if (con.Slope.EQ (Lib.HalfPI)) text = "vert";
            else {
               text = con.Perpendicular ? "perp" : "align";
               text = $"{text}:{Math.Round (con.Slope.R2D (), 1)}\u00b0";
            }
            yield return (text, con.Anchor, true);
         }
      }
   }

   /// <summary>The set of construction lines to be drawn (these are active construction lines the mouse is close to)</summary>
   public IEnumerable<(Point2 Pt, double Angle)> Lines 
      => mVisible.Select(con => (con.Anchor, con.Slope));

   /// <summary>The recent snap point that we computed</summary>
   public Point2 PtSnap => mPtSnap;
   Point2 mPtSnap;

   // Methods ------------------------------------------------------------------
   /// <summary>Snaps a given point (ptRaw) to the closest feature in the drawing</summary>
   /// <param name="ptRaw">The raw point to snap</param>
   /// <param name="aperture">The snap aperture in world coordinates. Only features closer than this
   /// value to ptRaw are considered for snapping. This value is typically set to a fixed number of
   /// device-independent pixels, so you have to scale it by the current pixel-to-world scaling
   /// for consistency.</param>
   public Point2 Snap (Point2 ptRaw, double aperture) {
      // Prepare for the snapping
      (mptRaw, mAperture) = (ptRaw, aperture);
      (mPtSnap, mSnap, mMinDistSq, mTangent) = (ptRaw, ESnap.None, aperture * aperture, double.NaN);
      mSegs.Clear (); mActive.Clear (); mVisible.Clear ();

      // There are 5 classes of snaps, in descending order of priority:
      // 1. Hard snaps like endpoint, midpoint, center, quadrant etc directly on Poly segs,
      //    and snaps like insert point of block, base point of text, etc. These hard snaps,
      //    if issued, will lead to the creation of new construction lines anchored at that
      //    point
      // 2. Intersections between construction lines and actual segments in the drawing geometry
      // 3. Intersections between pairs of construction lines
      // 4. ON snaps along the segments of polys
      // 5. ON snaps along construction lines
      // Let's check all these 5 classes in sequence (note that we are depending on the short-circuiting
      // behavior of the || operator for this).
      if (HardSnaps () || ConsSegIntersections () || ConsConsIntersections () || OnSeg () || OnCons ()) return mPtSnap;
      return ptRaw;
   }
   Point2 mptRaw;
   double mAperture;

   // Implementation -----------------------------------------------------------
   // Adds a construction line to the list of all construction lines.
   // We don't want too many construction lines in this list so we try to limit this:
   // - No more than 3 unique points through which construction lines are drawn (there
   //   could be multiple construction lines with the same anchor point, at different
   //   angles).
   // - No more than 12 construction lines in all
   // The 'different angles' needs some explanation. Suppose we have an 'endpoint' snap at the
   // end of a line, then the
   void AddConsLine (Point2 pt, IList<double> angles) {
      for (int i = 0; i < angles.Count; i++) {
         // If this same infinite line does not exist already, add it
         double ang = angles[i];
         if (ang.IsNaN ()) continue;
         var angle = Lib.NormalizeAngle (ang);
         if (angle < -Lib.Epsilon) angle += Lib.PI;
         if (angle.EQ (Lib.PI)) angle = 0;
         if (mCons.Any (a => a.EQ (pt, angle))) continue;
         mCons.Add (new (pt, angle, i is 1 or 5));
         // We don't want to have more than 3 points in the drawing from which construction
         // lines are radiating, or more than 12 construction lines in all
         if (mCons.Count > 12 || mCons.Select (a => a.Anchor).Distinct (PointComparer.Epsilon).Count () > 3)
            mCons.RemoveAt (0);
      }
   }
   List<ConsLine> mCons = [];    // List of all construction lines

   // Helper used to check if a given point is closer to the input point than the
   // closest point we have so far
   bool Check (Point2 pt, ESnap snap, double tangent = double.NaN) {
      if (pt.IsNil) return false;
      double distSq = pt.DistToSq (mptRaw);
      if (distSq.EQ (mMinDistSq) && snap <= mSnap) return false;
      if (distSq > mMinDistSq + Lib.Epsilon) return false;
      (mPtSnap, mMinDistSq, mSnap, mTangent, mTangent2) = (pt, distSq, snap, tangent, double.NaN);
      return true;
   }
   double mMinDistSq;               // Distance to closest snap point (so far)
   double mTangent;                 // Tangent angle at that snap point (used to make construction lines)
   double mTangent2 = double.NaN;   // Second tangent angle at that snap point

   // Checks the intersections between pairs of construction lines
   bool ConsConsIntersections () {
      for (int i = 1; i < mActive.Count; i++) {
         var con1 = mActive[i];
         Point2 a = con1.Anchor, b = a.Polar (10, con1.Slope);
         for (int j = 0; j < i; j++) {
            var con2 = mActive[j];
            Point2 c = con2.Anchor, d = c.Polar (10, con2.Slope);
            if (Check (Geo.LineXLine (a, b, c, d), ESnap.Intersection, 0)) {
               mVisible.Clear (); mVisible.Add (con1); mVisible.Add (con2);
            }
         }
      }
      return mSnap != ESnap.None;
   }

   // Checks the intersections between construction lines and existing segments
   bool ConsSegIntersections () {
      mActive.Clear ();
      mActive.AddRange (mCons.Where (a => a.DistTo (mptRaw) < mAperture));
      Span<Point2> buffer = stackalloc Point2[2];
      foreach (var cons in mActive) {
         Point2 a = cons.Anchor, b = a.Polar (10, cons.Slope);
         foreach (var seg in mSegs) {
            var pts = seg.Intersect (a, b, buffer, true);
            foreach (var pt in pts)
               if (Check (pt, ESnap.Intersection, 0)) {
                  mVisible.Clear (); mVisible.Add (cons);
               }
         }
      }
      return mSnap != ESnap.None;
   }
   List<ConsLine> mActive = [];  // List of construction lines we're close to
   List<ConsLine> mVisible = []; // List of construction lines that are visible

   // This checks for hard snaps (like endpoint, midpoint, center, quadrant) etc.
   // Segments of Polys are checked, and also nodes of inserts, dimensions, text etc are checked.
   // This routine also updates some members as a side effect
   // - mSegs is populated with the list of segments that are close to the mouse point
   // - mCons is updated with one or more additional construction lines (if we found a snap, that
   //   snap point is used as an anchor for additional construction lines)
   bool HardSnaps () {
      mSegs.Clear ();
      var (ptRaw, aperture) = (mptRaw, mAperture);
      foreach (var ent in mDwg.Ents) {
         if (!ent.Bound.Contains (mptRaw, mAperture)) continue;
         switch (ent) {
            case E2Poly e2p:
               foreach (var seg in e2p.Poly.Segs) {
                  if (seg.IsArc) {
                     var center = seg.Center;
                     Check (center, ESnap.Center, 0);
                     if (seg.Bound.Contains (ptRaw, aperture)) {
                        var radius = seg.Radius;
                        for (EDir dir = EDir.E; dir <= EDir.S; dir++) {
                           var pt = center.CardinalMoved (radius, dir);
                           Check (pt, ESnap.Quadrant, 0);
                        }
                        if (!seg.IsCircle) {
                           Check (seg.A, ESnap.Endpoint, seg.GetSlopeAt (0));
                           Check (seg.B, ESnap.Endpoint, seg.GetSlopeAt (1));
                           Check (seg.Midpoint, ESnap.Midpoint, 0);
                        }
                        if (seg.GetDist (ptRaw, aperture) <= aperture) mSegs.Add (seg);
                     }
                  } else {
                     if (seg.GetDist (ptRaw, aperture) <= aperture) {
                        mSegs.Add (seg);
                        if (Check (seg.A, ESnap.Endpoint, seg.Slope))
                           if (seg.Prev is { } seg2) mTangent2 = seg2.GetSlopeAt (1);
                        Check (seg.Midpoint, ESnap.Midpoint, 0);
                        if (Check (seg.B, ESnap.Endpoint, seg.Slope))
                           if (seg.Next is { } seg2) mTangent2 = seg2.GetSlopeAt (0);
                     }
                  }
               }
               break;
            case E2Solid e2s:
               foreach (var pt in e2s.Pts) Check (pt, ESnap.Endpoint, 0);
               break;
            case E2Text e2t: Check (e2t.Pt, ESnap.Node, 0); break;
            case E2Insert e2i: Check (e2i.Pt, ESnap.Node, 0); break;
            case E2Point e2e: Check (e2e.Pt, ESnap.Node, 0); break;
         }
      }

      // Check the intersections of segments we're close to
      Span<Point2> buffer = stackalloc Point2[2];
      for (int i = 1; i < mSegs.Count; i++) {
         Seg s1 = mSegs[i];
         for (int j = 0; j < i; j++) {
            Seg s2 = mSegs[j];
            var pts = s1.Intersect (s2, buffer, true);
            foreach (var pt in pts)
               Check (pt, ESnap.Intersection, 0);
         }
      }

      // If we found a snap point, add construction lines using this point a the anchor
      if (mSnap != ESnap.None) {
         AddConsLine (mPtSnap, [mTangent, mTangent + Lib.HalfPI, 0, Lib.HalfPI, mTangent2, mTangent2 + Lib.HalfPI]);
         return true;
      } else
         return false;
   }
   List<Seg> mSegs = [];         // List of segs we're close to

   // Check if the given input point is ON any of the construction lines
   bool OnCons () {
      mVisible.Clear ();
      foreach (var cons in mActive) {
         Point2 pt = mptRaw.SnappedToLine (cons.Anchor, cons.Anchor.Polar (10, cons.Slope));
         if (Check (pt, ESnap.On)) { mVisible.Clear (); mVisible.Add (cons); }
      }
      return mSnap != ESnap.None;
   }

   // Check if the given input point is ON any of the segs
   bool OnSeg () {
      foreach (var seg in mSegs)
         Check (seg.GetClosestPoint (mptRaw), ESnap.On);
      return mSnap != ESnap.None;
   }

   // Nested types -------------------------------------------------------------
   // This represents a construction line (with an anchor point and an infinite ray
   // passing through that point with a given slope)
   readonly struct ConsLine {
      public ConsLine (Point2 a, double s, bool perp) => (Anchor, Slope, Perpendicular) = (a, s, perp);

      public double DistTo (Point2 pt)
         => pt.DistToLine (Anchor, Anchor.Polar (10, Slope));

      public bool EQ (Point2 pt, double ang) {
         if (!ang.EQ (Slope, 0.001)) return false;
         return pt.DistToLine (Anchor, Anchor.Polar (10, Slope)).IsZero (0.001);
      }

      public Point2 GetIntersection (ConsLine other)
         => Geo.LineXLine (Anchor, Anchor.Polar (10, Slope), other.Anchor, other.Anchor.Polar (10, other.Slope));

      public readonly Point2 Anchor;
      public readonly double Slope;
      public readonly bool Perpendicular;
   }
}
#endregion

#region enum ESnap ---------------------------------------------------------------------------------
/// <summary>The possible snap values</summary>
public enum ESnap {
   None,
   /// <summary>On a segment or a construction line</summary>
   On,
   /// <summary>Intersection between segments (or construction lines)</summary>
   Intersection,
   /// <summary>Quadrant of a segment</summary>
   Quadrant,
   /// <summary>Midpoint of a segment</summary>
   Midpoint,
   /// <summary>Center of an arc or circle</summary>
   Center,
   /// <summary>Endpoint of a segment</summary>
   Endpoint,
   /// <summary>Point, Block-Insert-Point, Text-Base-Point</summary>
   Node
}
#endregion
