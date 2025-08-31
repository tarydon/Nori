// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgSnap.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DwgSnap ------------------------------------------------------------------------------
/// <summary>DwgSnap provides 'snap' logic to features in a drawing</summary>
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
            string text = "";
            if (con.Slope.IsZero ()) text = "horz";
            else if (con.Slope.EQ (Lib.HalfPI)) text = "vert";
            else text = $"align:{Math.Round (con.Slope.R2D (), 1)}\u00b0";
            yield return (text, con.Anchor, true);
         }
      }
   }

   /// <summary>The set of construction lines to be drawn (these are active construction lines the mouse is close to)</summary>
   public IEnumerable<(Point2 Pt, double Angle)> Lines {
      get {
         foreach (var con in mVisible)
            yield return (con.Anchor, con.Slope);
      }
   }

   /// <summary>The recent snap point that we computed</summary>
   public Point2 PtSnap => mPtSnap;
   Point2 mPtSnap;

   // Methods ------------------------------------------------------------------
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
      if (HardSnaps () || ConsSegIntersections () || ConsConsIntersections () || OnCons () || OnSeg ()) return mPtSnap;
      return ptRaw;
   }
   Point2 mptRaw;
   double mAperture;

   // Implementation -----------------------------------------------------------
   void AddConsLine (Point2 pt, IEnumerable<double> angles) {
      foreach (var ang in angles) {
         // If this same infinite line does not exist already, add it
         var angle = Lib.NormalizeAngle (ang);
         if (angle < -Lib.Epsilon) angle += Lib.PI;
         if (angle.EQ (Lib.PI)) angle = 0; 
         if (mCons.Any (a => a.EQ (pt, angle))) continue;
         mCons.Add (new (pt, angle));
         Lib.Trace (pt);
         // We don't want to have more than 3 points in the drawing from which construction
         // lines are radiating, or more than 12 construction lines in all
         if (mCons.Count > 12 || mCons.Select (a => a.Anchor.R6 ()).Distinct ().Count () > 3)
            mCons.RemoveAt (0);
      }
   }
   List<ConsLine> mCons = [];    // List of all construction lines

   bool Check (Point2 pt, ESnap snap, double tangent = double.NaN) {
      if (pt.IsNil) return false;
      double distSq = pt.DistToSq (mptRaw);
      if (distSq.EQ (mMinDistSq) && snap <= mSnap) return false;
      if (distSq > mMinDistSq + Lib.Epsilon) return false;
      (mPtSnap, mMinDistSq, mSnap, mTangent) = (pt, distSq, snap, tangent);
      return true;
   }
   double mMinDistSq;            // Distance to closest snap point (so far)
   double mTangent;              // Tangent angle at that snap point (used to make construction lines)

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
                        Check (seg.Midpoint, ESnap.Midpoint, 0);
                        for (EDir dir = EDir.E; dir <= EDir.S; dir++) {
                           var pt = center.CardinalMoved (radius, dir);
                           Check (pt, ESnap.Quadrant, 0);
                        }
                        if (!seg.IsCircle) {
                           Check (seg.A, ESnap.Endpoint, seg.GetSlopeAt (0));
                           Check (seg.B, ESnap.Endpoint, seg.GetSlopeAt (1));
                        }
                        if (seg.GetDist (ptRaw, aperture) <= aperture) mSegs.Add (seg);
                     }
                  } else {
                     Check (seg.A, ESnap.Endpoint, seg.Slope);
                     Check (seg.Midpoint, ESnap.Midpoint, 0);
                     Check (seg.B, ESnap.Endpoint, seg.Slope);
                     if (seg.GetDist (ptRaw, aperture) <= aperture) mSegs.Add (seg);
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
            for (int k = 0; k < pts.Length; k++) Check (pts[k], ESnap.Intersection, 0);
         }
      }

      // If we found a snap point, add construction lines using this point a the anchor
      if (mSnap != ESnap.None) {
         AddConsLine (mPtSnap, [mTangent, mTangent + Lib.HalfPI, 0, Lib.HalfPI]);
         return true;
      } else
         return false;
   }
   List<Seg> mSegs = [];         // List of segs we're close to

   bool ConsSegIntersections () {
      mActive.Clear ();
      mActive.AddRange (mCons.Where (a => a.DistTo (mptRaw) < mAperture));
      return false;
   }
   List<ConsLine> mActive = [];  // List of construction lines we're close to
   List<ConsLine> mVisible = []; // List of construction lines that are visible

   bool ConsConsIntersections () {
      return false;
   }

   bool OnCons () {
      mVisible.Clear ();
      for (int i = 0; i < mActive.Count; i++) {
         var cons = mActive[i];
         Point2 pt = mptRaw.SnappedToLine (cons.Anchor, cons.Anchor.Polar (10, cons.Slope));
         if (Check (pt, ESnap.On)) { mVisible.Clear (); mVisible.Add (cons); }
      }
      return mSnap != ESnap.None;
   }

   bool OnSeg () {
      return false;
   }

   // Nested types -------------------------------------------------------------
   // This represents a construction line (with an anchor point and an infinite ray
   // passing through that point with a given slope)
   readonly struct ConsLine {
      public ConsLine (Point2 a, double s) => (Anchor, Slope) = (a, s);

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
   }
}
#endregion

#region enum ESnap ---------------------------------------------------------------------------------
/// <summary>The possible snap values</summary>
public enum ESnap {
   None,
   On,
   /// <summary>Intersection</summary>
   Intersection,
   /// <summary>Quadrant of a segment</summary>
   Quadrant,
   /// <summary>Midpoint of a segment</summary>
   Midpoint,
   /// <summary>Center of an arc or circle</summary>
   Center,
   /// <summary>Endpoint of a segment</summary>
   Endpoint,
   /// <summary>Point, Block-Insert-Point</summary>
   Node,
}
#endregion
