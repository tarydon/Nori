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

   /// <summary>The recent snap point that we computed</summary>
   public Point2 PtSnap => mPtSnap;
   Point2 mPtSnap;

   // Methods ------------------------------------------------------------------
   public Point2 Snap (Point2 ptRaw, double aperture) {
      double minDistSq = aperture * aperture;
      Point2 ptSnap = Point2.Nil;
      ESnap eSnap = ESnap.None;

      foreach (var ent in mDwg.Ents) {
         if (!ent.Bound.Contains (ptRaw, aperture)) continue;
         switch (ent) {
            case E2Poly e2p:
               foreach (var pt in e2p.Poly.Pts) Check (ESnap.Endpoint, pt);
               foreach (var seg in e2p.Poly.Segs) {
                  if (seg.IsArc) {
                     var center = seg.Center;
                     Check (ESnap.Center, center);
                     if (seg.Bound.Contains (ptRaw, aperture)) {
                        var radius = seg.Radius;
                        Check (ESnap.Midpoint, seg.Midpoint);
                        for (EDir dir = EDir.E; dir <= EDir.S; dir++) {
                           var pt = center.CardinalMoved (radius, dir);
                           Check (ESnap.Quadrant, pt);
                        }
                     }
                  } else
                     Check (ESnap.Midpoint, seg.Midpoint);
               }
               break;
            case E2Solid e2s:
               foreach (var pt in e2s.Pts) Check (ESnap.Endpoint, pt);
               break;
            case E2Text e2t: Check (ESnap.Node, e2t.Pt); break;
            case E2Insert e2i: Check (ESnap.Node, e2i.Pt); break;
            case E2Point e2e: Check (ESnap.Node, e2e.Pt); break;
         }
      }
      if ((mSnap = eSnap) == ESnap.None) return mPtSnap = ptRaw;
      return mPtSnap = ptSnap;

      // Helper ............................................
      void Check (ESnap snap, Point2 pt) {
         double distSq = pt.DistToSq (ptRaw);
         if (distSq.EQ (minDistSq) && snap <= eSnap) return;
         if (distSq > minDistSq + Lib.Epsilon) return;
         minDistSq = distSq; ptSnap = pt; eSnap = snap;
      }
   }

   public IEnumerable<(string text, Point2 Pt, bool above)> Labels {
      get {
         if (mSnap != ESnap.None)
            yield return (mSnap.ToString ().ToLower (), mPtSnap, false);
      }
   }

   public IEnumerable<(Point2 Pt, double Angle)> Lines {
      get {
         yield return (new (600, 600), 0);
         yield return (new (600, 600), Lib.QuarterPI);
      }
   }
}
#endregion

#region enum ESnap ---------------------------------------------------------------------------------
/// <summary>The possible snap values</summary>
public enum ESnap {
   None,
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
