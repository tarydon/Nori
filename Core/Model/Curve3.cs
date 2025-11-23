// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Curve3.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public abstract class Edge3 {
   protected Edge3 () { }
   protected Edge3 (int pairId) => PairId = pairId;
   public abstract Point3 Start { get; }
   public abstract Point3 End { get; }

   public readonly int PairId;

   public abstract Point3 GetPointAt (double lie);

   /// <summary>Returns a PiecewiseLinear approximation of this curve</summary>
   /// 1. The curve is approximated with the given error threshold
   /// 2. The End point of the curve is not included (it is effectively the start
   ///    point of the next Edge in the sequence
   public abstract void Discretize (List<Point3> pts, double tolerance, double maxAngStep);
}

#region class Line3 --------------------------------------------------------------------------------
public class Line3 : Edge3 {
   Line3 () { }
   public Line3 (int pairId, Point3 start, Point3 end) : base (pairId) => (mStart, mEnd) = (start, end);

   public override string ToString () => $"Line3 Len={Start.DistTo (End).Round (2)}";

   // Properties ---------------------------------------------------------------
   /// <summary>Start point of the line</summary>
   public override Point3 Start => mStart;
   readonly Point3 mStart;

   public double Length => Start.DistTo (End);

   /// <summary>End point of the line</summary>
   public override Point3 End => mEnd;
   readonly Point3 mEnd;

   // Methods ------------------------------------------------------------------
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) => pts.Add (Start);

   public override Point3 GetPointAt (double lie) => lie.Along (mStart, mEnd);
}
#endregion

#region class Arc3 ---------------------------------------------------------------------------------
public class Arc3 : Edge3 {
   // Constructors -------------------------------------------------------------
   Arc3 () { }
   public Arc3 (int pairId, CoordSystem cs, double radius, double angSpan) : base (pairId)
      => (CS, Radius, AngSpan) = (cs, radius, angSpan);

   public override string ToString ()
      => $"Arc3 R={Radius.Round (2)} Span={AngSpan.R2D ().Round (1)}\u00b0";

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
   public Point3 Center => Point3.Zero * ToXfm;

   /// <summary>End point of the Arc3</summary>
   public override Point3 End => GetPointAt (1);

   /// <summary>Radius of the arc</summary>
   public readonly double Radius;

   /// <summary>Start point of the Arc3 (is always at (Radius,0,0) in the local coordinate system)</summary>
   public override Point3 Start => new Point3 (Radius, 0, 0) * ToXfm;

   // Methods ------------------------------------------------------------------
   public override void Discretize (List<Point3> pts, double tolerance, double maxAngStep) {
      int n = Lib.GetArcSteps (Radius, AngSpan, tolerance, maxAngStep);
      if (AngSpan.EQ (Lib.TwoPI) && n.IsOdd ()) n++;
      for (int i = 0; i < n; i++) pts.Add (GetPointAt ((double)i / n));
   }

   /// <summary>Returns the point at a given lie</summary>
   public override Point3 GetPointAt (double lie) {
      var (sin, cos) = Math.SinCos (AngSpan * lie);
      return new Point3 (cos * Radius, sin * Radius, 0) * ToXfm;
   }

   // Implementation -----------------------------------------------------------
   Matrix3 ToXfm => _toXfm ??= Matrix3.To (CS);
   Matrix3? _toXfm;
}
#endregion

#region class Polyline3 -------------------------------------------------------------------------------
public class Polyline3 : Edge3 {
   // Constructors -------------------------------------------------------------
   Polyline3 () { }
   public Polyline3 (ImmutableArray<Point3> pts) { Pts = pts; } 

   // Properties ---------------------------------------------------------------
   public readonly ImmutableArray<Point3> Pts;
   public double Length => SD.TotalLength;
   private SegData SD => _segData ??= ComputeSegLength ();
   SegData? _segData = null;

   // Edge3 Implementation -----------------------------------------------------
   public override Point3 Start => Pts[0];
   public override Point3 End => Pts[^1];

   public override void Discretize (List<Point3> pts, double _, double __) => pts.AddRange (Pts);
   public override Point3 GetPointAt (double lie) {
      // First deal with the most common cases (even disregarding epsilon)
      if (lie == 0) return Start;
      else if (lie == 1) return End;

      // Get the length from lie. We want to do epsilon comparison on the length and not lie.
      var sd = SD;
      double length = lie * sd.TotalLength;
      if (length < Lib.Epsilon) return Start;
      else if (length > sd.TotalLength - Lib.Epsilon) return End;

      // Do a binary search and find the index of the first number that is greater than the given value.
      int idx = sd.SegLengths.BinarySearch (length);
      if (idx >= 0) return Pts[idx]; // Bang on a corner.
      // The point is somewhere between two points.
      idx = ~idx;
      double l1 = sd.SegLengths[idx - 1], segLie = (length - l1) / (sd.SegLengths[idx] - l1);
      return segLie.Along (Pts[idx - 1], Pts[idx]);
   }

   // Implementation -----------------------------------------------------------
   SegData ComputeSegLength () {
      double totalLength = 0;
      var segLengths = ImmutableArray.CreateBuilder<double> (); segLengths.Add (0);
      for (int i = 1; i < Pts.Length; i++)
         segLengths.Add (totalLength += Pts[i - 1].DistTo (Pts[i]));
      return new SegData(totalLength, segLengths.ToImmutable ());
   }

   // Embedded types -----------------------------------------------------------
   class SegData (double totalLength, ImmutableArray<double> segLengths) {
      public readonly double TotalLength = totalLength;
      public readonly ImmutableArray<double> SegLengths = segLengths;
   }
}
#endregion

public class Contour3 {
   public Contour3 (ImmutableArray<Edge3> edges) => mEdges = edges;

   public void Discretize (List<Point3> pts, double tolerance, double maxAngStep)
      => mEdges.ForEach (e => e.Discretize (pts, tolerance, maxAngStep));

   public Poly Flatten (CoordSystem cs) {
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

      Point2 Xfm (Point3 pt) {
         pt *= xfm; Lib.Check (pt.Z.IsZero (0.001), "Non-planar contour");
         return new (pt.X, pt.Y);
      }
   }

   public ImmutableArray<Edge3> Edges => mEdges;
   readonly ImmutableArray<Edge3> mEdges;
}
