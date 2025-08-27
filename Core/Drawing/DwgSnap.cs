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

   // Methods ------------------------------------------------------------------
   public Point2 Snap (Point2 pt, double aperture) {
      return pt;
   }

   public IEnumerable<(ESnap Snap, Point2 Pt)> Labels {
      get {
         yield return (ESnap.Endpoint, new (600, 600));
         yield return (ESnap.Center, new (360, 480));
      }
   }

   public IEnumerable<(Point2 Pt, double Angle)> Lines {
      get {
         yield return (new (600, 600), 0);
         yield return (new (360, 480), 180);
      }
   }
}
#endregion

#region enum ESnap ---------------------------------------------------------------------------------
/// <summary>The possible snap values</summary>
public enum ESnap {
   /// <summary>Endpoint of a segment</summary>
   Endpoint,
   /// <summary>Midpoint of a segment</summary>
   Midpoint,
   /// <summary>Center of an arc or circle</summary>
   Center,
   /// <summary>Quadrant of a segment</summary>
   Quadrant,
   /// <summary>Point, Block-Insert-Point</summary>
   Node,
   /// <summary>Intersection</summary>
   Intersection,
}
#endregion
