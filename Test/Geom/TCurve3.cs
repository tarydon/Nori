// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TGeometry.cs
// ║║║║╬║╔╣║ Various geometry tests for Curve3 (3D curves)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Immutable;

namespace Nori.Testing;

[Fixture (36, "Curve3 tests", "Geom")]
class Curve3Tests {
   [Test (196, "Flipping Arc3")]
   void Test1 () {
      Arc3 arc = new (0, new CoordSystem (new Point3 (5, 0, 5), Vector3.XAxis, Vector3.ZAxis), 5, Lib.PI);
      var flipped = arc.Flipped ();
      arc.Start.EQ (flipped.End).IsTrue ();
      arc.End.EQ (flipped.Start).IsTrue ();
      List<Point3> pts1 = [], pts2 = [];
      arc.Discretize (pts1, Lib.CoarseTess, Lib.CoarseTessAngle);
      flipped.Discretize (pts2, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts2.Reverse ();
      pts1.Count.Is (pts2.Count);
      for (int i = 0; i < pts1.Count; i++)
         pts1[i].EQ (pts2[i]).IsTrue ();
   }

   [Test (197, "Flipping Ellipse3")]
   void Test2 () {
      Ellipse3 ellipse = new (0, new CoordSystem (new Point3 (0, 0, 0), Vector3.XAxis, Vector3.YAxis), 8, 4, Lib.HalfPI, Lib.PI);
      var flipped = ellipse.Flipped ();
      ellipse.Start.EQ (flipped.End).IsTrue ();
      ellipse.End.EQ (flipped.Start).IsTrue ();
      List<Point3> pts1 = [], pts2 = [];
      ellipse.Discretize (pts1, Lib.CoarseTess, Lib.CoarseTessAngle);
      flipped.Discretize (pts2, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts2.Reverse ();
      pts1.Count.Is (pts2.Count);
      for (int i = 0; i < pts1.Count; i++)
         pts1[i].EQ (pts2[i]).IsTrue ();
   }

   [Test (198, "Flipping Ellipse3 - wrapping around xaxis")]
   void Test3 () {
      Ellipse3 ellipse = new (0, new CoordSystem (new Point3 (0, 0, 0), Vector3.XAxis, Vector3.YAxis), 8, 4, Lib.HalfPI, Lib.TwoPI + 1.D2R ());
      var flipped = ellipse.Flipped ();
      ellipse.Start.EQ (flipped.End).IsTrue ();
      ellipse.End.EQ (flipped.Start).IsTrue ();
      List<Point3> pts1 = [], pts2 = [];
      ellipse.Discretize (pts1, Lib.CoarseTess, Lib.CoarseTessAngle);
      flipped.Discretize (pts2, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts2.Reverse ();
      pts1.Count.Is (pts2.Count);
      for (int i = 0; i < pts1.Count; i++)
         pts1[i].EQ (pts2[i]).IsTrue ();
   }

   [Test (178, "Flipping BSpline")]
   void Test4 () {
      ImmutableArray<Point3> controlPoints = [
         new Point3 (0, 0, 0),
         new Point3 (5, 2, 1),
         new Point3 (10, -2, 2),
         new Point3 (15, 0, 3)
      ];
      ImmutableArray<double> weights = [1, 1, 1, 1];
      ImmutableArray<double> knots = [0, 0, 0, 0, 1, 1, 1, 1];
      NurbsCurve3 spline = new (0, controlPoints, knots, weights);
      var flipped = spline.Flipped ();
      spline.Start.EQ (flipped.End).IsTrue ();
      spline.End.EQ (flipped.Start).IsTrue ();
      List<Point3> pts1 = [], pts2 = [];
      spline.Discretize (pts1, Lib.CoarseTess, Lib.CoarseTessAngle);
      flipped.Discretize (pts2, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts2.Reverse ();
      pts1.Count.Is (pts2.Count);
      for (int i = 0; i < pts1.Count; i++)
         pts1[i].EQ (pts2[i]).IsTrue ();
   }

   [Test (179, "Flipping Polyline3")]
   void Test5 () {
      ImmutableArray<Point3> pts = [
         new Point3 (0, 0, 0),
         new Point3 (2, 2, 0),
         new Point3 (4, 0, 1),
         new Point3 (6, -1, 2)
      ];
      Polyline3 polyline = new (0, pts);
      var flipped = polyline.Flipped ();
      polyline.Start.EQ (flipped.End).IsTrue ();
      polyline.End.EQ (flipped.Start).IsTrue ();
      List<Point3> pts1 = [], pts2 = [];
      polyline.Discretize (pts1, Lib.CoarseTess, Lib.CoarseTessAngle);
      flipped.Discretize (pts2, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts2.Reverse ();
      pts1.Count.Is (pts2.Count);
      for (int i = 0; i < pts1.Count; i++)
         pts1[i].EQ (pts2[i]).IsTrue ();
   }

   [Test (180, "Trimming of Circle")]
   void Test6 () {
      Arc3 arc = new (0, new CoordSystem (new Point3 (5, 0, 5), Vector3.XAxis, Vector3.ZAxis), 5, Lib.TwoPI); // Full circle
      var trimmed = arc.Trimmed (Lib.HalfPI, Lib.PI, false);
      trimmed.Start.EQ (new Point3 (5, 0, 10)).IsTrue ();
      trimmed.End.EQ (new Point3 (0, 0, 5)).IsTrue ();
      List<Point3> pts = [];
      trimmed.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      (pts[1].X < 5).IsTrue ();
      var trimmed2 = arc.Trimmed (Lib.HalfPI, Lib.PI, true);
      trimmed.Start.EQ (trimmed2.Start).IsTrue ();
      trimmed.End.EQ (trimmed2.End).IsTrue ();
      trimmed.Domain.Length.EQ (Lib.HalfPI);
      trimmed2.Domain.Length.EQ (3 * Lib.HalfPI);
      pts.Clear ();
      trimmed2.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      (pts[1].X > 5).IsTrue ();
   }

   [Test (181, "Trimming of Ellipse")]
   void Test7 () {
      Ellipse3 ellipse = new (0, new CoordSystem (Point3.Zero, Vector3.XAxis, Vector3.YAxis), 8, 4, 0, Lib.TwoPI);
      var trimmed = ellipse.Trimmed (Lib.HalfPI, Lib.PI, false);
      trimmed.Start.EQ (new Point3 (0, 4, 0)).IsTrue ();
      trimmed.End.EQ (new Point3 (-8, 0, 0)).IsTrue ();
      List<Point3> pts = [];
      trimmed.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      (pts[1].X < 0).IsTrue ();

      var trimmed2 = ellipse.Trimmed (Lib.HalfPI, Lib.PI, true);
      trimmed.Start.EQ (trimmed2.Start).IsTrue ();
      trimmed.End.EQ (trimmed2.End).IsTrue ();
      trimmed.Domain.Length.EQ (Lib.HalfPI);
      trimmed2.Domain.Length.EQ (3 * Lib.HalfPI);
      pts.Clear ();
      trimmed2.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      (pts[1].X > 0).IsTrue ();
   }

   [Test (182, "Trimming of NurbsCurve3")]
   void Test8 () {
      ImmutableArray<Point3> controlPoints = [
         new Point3 (0, 0, 0),
         new Point3 (5, 2, 1),
         new Point3 (10, -2, 2),
         new Point3 (15, 0, 3)
      ];
      ImmutableArray<double> weights = [1, 1, 1, 1];
      ImmutableArray<double> knots = [0, 0, 0, 0, 1, 1, 1, 1];
      NurbsCurve3 spline = new (0, controlPoints, knots, weights);

      double t1 = 0.25, t2 = 0.75;
      Point3 expectedStart = spline.GetPoint (t1);
      Point3 expectedEnd = spline.GetPoint (t2);

      var trimmed = spline.Trimmed (t1, t2, false);
      trimmed.Start.EQ (expectedStart).IsTrue ();
      trimmed.End.EQ (expectedEnd).IsTrue ();
      trimmed.Domain.Min.EQ (t1).IsTrue ();
      trimmed.Domain.Max.EQ (t2).IsTrue ();
      trimmed.Domain.Length.EQ (t2 - t1);

      List<Point3> pts = [];
      trimmed.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
      pts[0].EQ (expectedStart).IsTrue ();
      pts[^1].EQ (expectedEnd).IsTrue ();

      var reversed = spline.Trimmed (t2, t1, false);
      reversed.Start.EQ (expectedEnd).IsTrue ();
      reversed.End.EQ (expectedStart).IsTrue ();
      reversed.Domain.Min.EQ (t1).IsTrue ();
      reversed.Domain.Max.EQ (t2).IsTrue ();

      List<Point3> reversedPts = [];
      reversed.Discretize (reversedPts, Lib.CoarseTess, Lib.CoarseTessAngle);
      reversedPts.Reverse ();
      pts.Count.Is (reversedPts.Count);
      for (int i = 0; i < pts.Count; i++)
         pts[i].EQ (reversedPts[i]).IsTrue ();
   }

   [Test (183, "Trimming of Polyline3")]
   void Test9 () {
      ImmutableArray<Point3> pts = [
         new Point3 (0, 0, 0),
         new Point3 (4, 0, 0),
         new Point3 (4, 4, 0),
         new Point3 (8, 4, 2)
      ];
      Polyline3 polyline = new (0, pts);

      double t1 = 0.5, t2 = 2.25;
      Point3 expectedStart = polyline.GetPoint (t1);
      Point3 expectedEnd = polyline.GetPoint (t2);

      Polyline3 trimmed = (Polyline3)polyline.Trimmed (t1, t2, false);
      trimmed.Start.EQ (expectedStart).IsTrue ();
      trimmed.End.EQ (expectedEnd).IsTrue ();

      int expectedSegmentCount = trimmed.Pts.Length - 1;
      trimmed.Domain.Min.EQ (0).IsTrue ();
      trimmed.Domain.Max.EQ (expectedSegmentCount).IsTrue ();
      trimmed.Domain.Length.EQ (expectedSegmentCount);

      List<Point3> trimmedPts = [];
      trimmed.Discretize (trimmedPts, Lib.CoarseTess, Lib.CoarseTessAngle);
      trimmedPts[0].EQ (expectedStart).IsTrue ();
      trimmedPts[^1].EQ (expectedEnd).IsTrue ();

      Polyline3 reversed = (Polyline3)polyline.Trimmed (t2, t1, false);
      reversed.Start.EQ (expectedEnd).IsTrue ();
      reversed.End.EQ (expectedStart).IsTrue ();
      reversed.Domain.Min.EQ (0).IsTrue ();
      reversed.Domain.Max.EQ (expectedSegmentCount).IsTrue ();
      reversed.Domain.Length.EQ (expectedSegmentCount);

      List<Point3> reversedPts = [];
      reversed.Discretize (reversedPts, Lib.CoarseTess, Lib.CoarseTessAngle);
      reversedPts.Reverse ();
      trimmedPts.Count.Is (reversedPts.Count);
      for (int i = 0; i < trimmedPts.Count; i++)
         trimmedPts[i].EQ (reversedPts[i]).IsTrue ();
   }
}