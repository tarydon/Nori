// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Curve3.cs
// ║║║║╬║╔╣║ Implements Edge3 and various types of derived edges
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Edge3 --------------------------------------------------------------------------------
/// <summary>Base class for various types of Edge3</summary>
public abstract class Edge3 {
   // Constructors -------------------------------------------------------------
   protected Edge3 () { }
   protected Edge3 (int pairId) => PairId = pairId;

   // Properties ---------------------------------------------------------------
   /// <summary>Get the start point of the Edge3</summary>
   public abstract Point3 Start { get; }
   /// <summary>Get the end point of the Edge3</summary>
   public abstract Point3 End { get; }

   /// <summary>If non-zero, this is the pair-ID of this edge</summary>
   /// In each fully connected manifold model, there are exactly two edges with
   /// the same pair-ID. These are the two co-edges on two adjacent faces that are
   /// touching each other. If these two edges are E1 and E2, then 
   /// E1.Start==E2.End, and E1.End==E2.Start, and they run against each other. 
   public readonly int PairId;

   // Methods ------------------------------------------------------------------
   public abstract Point3 GetPointAt (double lie);

   /// <summary>Returns a PiecewiseLinear approximation of this curve</summary>
   /// 1. The curve is approximated with the given error threshold
   /// 2. The End point of the curve is not included (it is effectively the start
   ///    point of the next Edge in the sequence
   public abstract void Discretize (List<Point3> pts, double tolerance, double maxAngStep);

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{GetType ().Name} PairID={PairId}";
}
#endregion

#region class Line3 --------------------------------------------------------------------------------
/// <summary>Line3 implements a linear edge between two points</summary>
public sealed class Line3 : Edge3 {
   // Constructors -------------------------------------------------------------
   Line3 () { }
   public Line3 (int pairId, Point3 start, Point3 end) : base (pairId) => (mStart, mEnd) = (start, end);

   // Properties ---------------------------------------------------------------
   /// <summary>Start point of the line</summary>
   public override Point3 Start => mStart;
   readonly Point3 mStart;

   /// <summary>End point of the line</summary>
   public override Point3 End => mEnd;
   readonly Point3 mEnd;

   /// <summary>Length of the line</summary>
   public double Length => Start.DistTo (End);

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize just adds the start point of the Line3</summary>
   /// The convention is that the end point is never included (it is added as part of the next Edge3)
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) 
      => pts.Add (Start);

   /// <summary>Gets the point at a given lie</summary>
   public override Point3 GetPointAt (double lie) => lie.Along (mStart, mEnd);

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{base.ToString ()} Len={Start.DistTo (End).Round (2)}";
}
#endregion

#region class Arc3 ---------------------------------------------------------------------------------
/// <summary>Arc3 represents a section of a circular arc in space</summary>
/// The Arc3 is defined on the XY plane - always clockwise, with the center at the
/// origin and winding counter-clockwise about the Z axis. Then, it is lofted into space
/// using the specified coord-system CS.
public class Arc3 : Edge3 {
   // Constructors -------------------------------------------------------------
   Arc3 () { }
   public Arc3 (int pairId, CoordSystem cs, double radius, double angSpan) : base (pairId)
      => (CS, Radius, AngSpan) = (cs, radius, angSpan);

   // Properties ---------------------------------------------------------------
   /// <summary>Angular span of the arc (in radians)</summary>
   /// The arc always starts at the point (Radius, 0, 0) in its local
   /// coordinate system, so just the angular span is enough to specify the extent
   [Radian]
   public readonly double AngSpan;

   /// <summary>The coordinate system in which the Arc3 is defined</summary>
   /// In this CS, the arc lies in the XY plane, and winds CCW about the Z
   /// axis. The start point of the arc is along the X axis and the angular span
   /// is in radians. So, with an angular span of +PI/2, the arc will finish
   /// at (0, Radius, 0), or exactly on the Y axis
   public readonly CoordSystem CS;

   /// <summary>Returns the center point of the arc</summary>
   public Point3 Center => CS.Org;

   /// <summary>End point of the Arc3</summary>
   public override Point3 End => GetPointAt (1);

   /// <summary>Radius of the arc</summary>
   public readonly double Radius;

   /// <summary>Start point of the Arc3 (is always at (Radius,0,0) in the local coordinate system)</summary>
   public override Point3 Start => CS.Org + CS.VecX * Radius;

   // Methods ------------------------------------------------------------------
   /// <summary>Converts Arc3 to a piecewise-linear approximation</summary>
   /// <param name="pts">Points are added to this list</param>
   /// <param name="tolerance">Linear tolerance - the chords formed by the PWL approximation
   /// don't deviate from the original arc by more than this</param>
   /// <param name="maxAngStep">Angular tolerance - successive chords in the PWL approximation
   /// don't have a turn angle more than this between them</param>
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      int n = Lib.GetArcSteps (Radius, AngSpan, tolerance, maxAngStep);
      if (AngSpan.EQ (Lib.TwoPI) && n.IsOdd ()) n++;
      for (int i = 0; i < n; i++) pts.Add (GetPointAt ((double)i / n));
   }

   /// <summary>Returns the point at a given lie</summary>
   public override Point3 GetPointAt (double lie) {
      var (sin, cos) = Math.SinCos (AngSpan * lie);
      return CS.Org + CS.VecX * (Radius * cos) + CS.VecY * (Radius * sin);
   }

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"{base.ToString ()} R={Radius.Round (2)} Span={AngSpan.R2D ().Round (1)}\u00b0";
}
#endregion

#region class Contour3 -----------------------------------------------------------------------------
/// <summary>Contour3 is a collection of Edge3 connected end-to-end</summary>
/// Typically surfaces are bounded by a set of Contour3
public class Contour3 {
   // Constructors -------------------------------------------------------------
   Contour3 () { }
   public Contour3 (ImmutableArray<Edge3> edges) => mEdges = edges;

   // Properties ---------------------------------------------------------------
   /// <summary>The list of Edge3 in this Contour3</summary>
   public ImmutableArray<Edge3> Edges => mEdges;
   readonly ImmutableArray<Edge3> mEdges;

   // Methods ------------------------------------------------------------------
   /// <summary>Discretize this Contour3 into a piecewise-linear approximation</summary>
   public void Discretize (List<Point3> pts, double tolerance, double maxAngStep)
      => mEdges.ForEach (e => e.Discretize (pts, tolerance, maxAngStep));

   /// <summary>Project the Contour3 into a</summary>
   public Poly Flatten (CoordSystem cs, double tolerance, double maxAngStep) {
      var pb = PolyBuilder.It;
      var xfm = Matrix3.From (cs);
      foreach (var edge in mEdges) {
         switch (edge) {
            case Line3 line:
               pb.Line (Xfm (line.Start));
               break;
            case Arc3 arc:
               var (center, start, _) = (Xfm (arc.Center), Xfm (arc.Start), arc.Radius);
               var flags = (arc.CS.VecZ * xfm).Z > 0 ? Poly.EFlags.CCW : Poly.EFlags.CW;
               if (arc.AngSpan.EQ (Lib.TwoPI)) {
                  pb.Arc (start, center, flags);
                  pb.Arc (center + (center - start), center, flags);
               } else
                  pb.Arc (start, center, flags);
               break;
            default:
               throw new BadCaseException (edge);
         }
      }
      return pb.Close ().Build ();

      // Helpers ...........................................
      Point2 Xfm (Point3 pt) {
         pt *= xfm; Lib.Check (pt.Z.IsZero (0.001), "Non-planar contour");
         return new (pt.X, pt.Y);
      }
   }
}
#endregion
