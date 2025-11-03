// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Curve3.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public abstract class Edge3 {
   public abstract Point3 Start { get; }
   public abstract Point3 End { get; }
}

#region class Line3 --------------------------------------------------------------------------------
public class Line3 : Edge3 {
   public Line3 (Point3 start, Point3 end) => (mStart, mEnd) = (start, end);
   Line3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Start point of the line</summary>
   public override Point3 Start => mStart;
   readonly Point3 mStart;

   /// <summary>End point of the line</summary>
   public override Point3 End => mEnd;
   readonly Point3 mEnd;
}
#endregion

#region class Arc3 ---------------------------------------------------------------------------------
public class Arc3 : Edge3 {
   // Constructors -------------------------------------------------------------
   public Arc3 (CoordSystem cs, double radius, double angSpan)
      => (CS, Radius, AngSpan) = (cs, radius, angSpan);
   Arc3 () { }

   // Properties ---------------------------------------------------------------
   /// <summary>Angular span of the arc (in radians)</summary>
   /// The arc always starts at the point (Radius, 0, 0) in its local
   /// coordinate system, so just the angular span is enough to specify the extent
   public readonly double AngSpan;

   /// <summary>The coordinate system in which the Arc3 is defined</summary>
   /// In this CS, the arc lies in the XY plane, and winds CCW about the Z
   /// axis. The start point of the arc is along the X axis and the angular span
   /// is in radians. So, with an angular span of +PI/2, the arc will finish
   /// at (0, Radius, 0), or exactly on the Y axis
   public readonly CoordSystem CS;

   /// <summary>Returns the center point of the arc</summary>
   public Point3 Center => Point3.Zero * ToXfm;

   /// <summary>End point of the Arc3</summary>
   public override Point3 End => GetPointAt (1);

   /// <summary>Radius of the arc</summary>
   public readonly double Radius;

   /// <summary>Start point of the Arc3 (is always at (Radius,0,0) in the local coordinate system)</summary>
   public override Point3 Start => new Point3 (Radius, 0, 0) * ToXfm;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the point at a given lie</summary>
   public Point3 GetPointAt (double lie) {
      var (sin, cos) = Math.SinCos (AngSpan * lie);
      return new Point3 (cos * Radius, sin * Radius, 0) * ToXfm;
   }

   // Implementation -----------------------------------------------------------
   Matrix3 ToXfm => _toXfm ??= Matrix3.To (CS);
   Matrix3? _toXfm;
}
#endregion

public class Contour3 {
   public Contour3 (ImmutableArray<Edge3> edges) => mEdges = edges;

   public Poly Flatten (CoordSystem cs) {
      var pb = PolyBuilder.It;
      var xfm = Matrix3.From (cs);
      foreach (var edge in mEdges) {
         switch (edge) {
            case Line3 line:
               pb.Line (Xfm (line.Start));
               break;
            case Arc3 arc:
               var (center, radius) = (Xfm (arc.Center), arc.Radius);
               if (arc.AngSpan.EQ (Lib.TwoPI)) return Poly.Circle (center, radius);
               bool ccw = (arc.CS.VecZ * xfm).Z < 0;
               pb.Arc (Xfm (arc.Start), center, ccw ? Poly.EFlags.CCW : Poly.EFlags.CW);
               break;
            default:
               throw new BadCaseException (edge);
         }
      }
      return pb.Close ().Build ();

      Point2 Xfm (Point3 pt) {
         pt *= xfm; Lib.Check (pt.Z.IsZero (0.001), "Non-planar contour");
         return new (pt.X, pt.Y);
      }
   }

   public ImmutableArray<Edge3> Edges => mEdges;
   readonly ImmutableArray<Edge3> mEdges;
}
