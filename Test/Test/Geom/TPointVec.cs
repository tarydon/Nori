// ────── ╔╗ Nori.Test
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ TPoint.cs ~ Tests of point and vector classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (4, "Point2 tests", "Math")]
class Point2Tests {
   [Test (11, "class Point2")]
   void Test1 () {
      // Point2 tests
      Point2 pa = new (3, 5); pa.Is ("(3,5)");
      Point2.Zero.Is ("(0,0)");
      Point2 pa2 = new (3.000001, 5); pa.EQ (pa2).Is (false);
      Point2 pa3 = new (3.0000001, 5); pa.EQ (pa3).Is (true);
      (pa + new Vector2 (10, 20)).Is ("(13,25)");
      (pa - new Vector2 (0.1, 0.2)).Is ("(2.9,4.8)");
      (new Point2 (10, 11) - pa).Is ("<7,6>");

      // Vector2f tests
      Vec2F pfa = (Vec2F)pa2; pfa.Is ("<3,5>");
   }

   [Test (12, "Point2.DistTo, Point2.EQ")]
   void Test2 () {
      Point2 pa = new (10, 20), pb = new (40, 60);
      pa.DistTo (pb).Is (50);
      Point2 pc = new (125.5, 223.75);
      pa.DistTo (pc).Is (234.2099752358981, 1e-12);
      pa.EQ (new (10 + 2 * Lib.Epsilon, 20)).IsFalse ();
      EQ (pa, new (10 + 0.5 * Lib.Epsilon, 20));
   }

   [Test (13, "Point2.AngleTo")]
   void Test3 () {
      Point2 pa = new (10, 20), pb = new (30, 20);
      pa.AngleTo (pb).Is (0, E);
      pa = new (0, 0); pb = new (-10, -10);
      (pa.AngleTo (pb) * 180 / Math.PI).Is (-135, E);
      pa = new (30, 40); pb = new (35, 50);
      double fAngle = pa.AngleTo (pb) * 180 / Math.PI;
      fAngle.Is (63.43494882292201, E);
      pa.AngleTo (pa).Is (0, E);
      fAngle = new Point2 (30, 20).AngleTo (new (-30, 19.999999999)).R2D ();
      fAngle.Is (-180, E);
      fAngle = new Point2 (30, 20).AngleTo (new (-30, 20.000000000));
      fAngle.Is (Math.PI, E);
   }

   [Test (14, "Point2.Clamp, DistToSq, Rotate")]
   void Test4 () {
      Bound2 r = new (2, 1, 20, 10);
      EQ (PX (-2, 3).Clamped (r), PX (2, 3));
      EQ (PX (5, 5).Clamped (r), PX (5, 5));
      EQ (PX (0, 0).Clamped (r), PX (2, 1));
      EQ (PX (100, 100).Clamped (r), PX (20, 10));
      EQ (PX (10, -1000).Clamped (r), PX (10, 1));
      PX (1, 2).DistToSq (PX (1, 2)).Is (0.0);
      PX (1, 2).DistToSq (PX (10, 5)).Is (90.0);
   }

   const double E = 1e-8;

   [Test (15, "Point2.SnapToLine, Point2.DistToLine")]
   void Test5 () {
      var pa = PX (13, 14);
      var pb = pa.SnappedToLine (new (0, 0), new (10, 0));
      EQ (pb, PX (13, 0));
      pa.DistToLine (new (0, 0), new (10, 0)).Is (14, E);
      pb = pa.SnappedToLine (new (0, 0), new (0, 10));
      EQ (pb, PX (0, 14));
      pa.DistToLine (new (0, 0), new (0, 10)).Is (13, E);
      pb = pa.SnappedToLine (new (30, -10), new (-50, 25));
      EQ (pb, PX (6.91803278688525, 0.0983606557377055));
      double f = pa.DistToLine (new (30, -10), new (-50, 25));
      f.Is (15.173855859317507, E);
      Point2 pc = pb.SnappedToLine (new (30, -10), new (-50, 25));
      EQ (pb, pc);
      pb.DistToLine (new (30, -10), new (-50, 25)).Is (0, E);
      pc = pa.SnappedToLine (new (50, 50), new (50, 50));
      EQ (pc, PX (50, 50));
   }

   [Test (16, "Point2.SnapToLineSeg, Point2.DistToLineSeg")]
   void Test6 () {
      Point2 pa = new (13, 14);
      Point2 pb = pa.SnappedToLineSeg (new (0, 0), new (10, 0));
      EQ (pb, PX (10, 0));
      pa.DistToLineSeg (new (0, 0), new (10, 0)).Is (14.317821063276353, E);
      Point2 pc = new Point2 (-10, -50).SnappedToLineSeg (new (0, 0), new (10, 0));
      EQ (pc, PX (0, 0));
      pb = pa.SnappedToLineSeg (new (0, 0), new (0, 10));
      EQ (pb, PX (0, 10));
      pb = pa.SnappedToLineSeg (new (30, -10), new (-50, 25));
      EQ (pb, PX (6.91803278688525, 0.0983606557377055));
      pc = pa.SnappedToLineSeg (new (50, 50), new (50, 50));
      EQ (pc, PX (50, 50));
   }

   [Test (17, "Point2.IsLeftOf, Point2.Side, ToString, Equals, GetHashCode")]
   void Test7 () {
      Point2 pa = new (13, 14);
      Point2 pc = new (30, -10), pd = new (-50, 25);
      Point2 pe = new (13, 0), pf = new (13, 20);
      pa.LeftOf (pc, pd).IsFalse (); pa.LeftOf (pd, pc).IsTrue ();
      pa.LeftOf (pe, pf).IsFalse (); pa.LeftOf (pf, pe).IsFalse ();
      pa.Side (pc, pd).Is (-1); pa.Side (pd, pc).Is (1);
      pa.Side (pe, pf).Is (0); pa.Side (pf, pe).Is (0);
   }

   [Test (18, "Point2 operators, IsNil, Nil")]
   void Test8 () {
      Point2 pa = new (10, 5), pb = new (21, 18);
      Vector2 va = new (3, -1);
      (pa + va).EQ (PX (13, 4)).IsTrue ();
      (pa - va).EQ (PX (7, 6)).IsTrue ();
      (pb - pa).EQ (new Vector2 (11, 13)).IsTrue ();

      PX (3, 4).IsNil.Is (false);
      PX (double.NaN, double.NaN).IsNil.Is (true);
      Point2.Nil.IsNil.Is (true);
   }

   [Test (19, "Point2.GetLieOn")]
   void Test9 () {
      Point2 a = new (0, 0), b = new (10, 5), c = new (4, 12);
      a.GetLieOn (a, b).Is (0, E); b.GetLieOn (a, b).Is (1, E);
      new Point2 (-2, -1).GetLieOn (a, b).Is (-0.2, E); new Point2 (5, 2.5).GetLieOn (a, b).Is (0.5, E);
      new Point2 (20, 10).GetLieOn (a, b).Is (2.0, E);
      new Point2 (-8, -24).GetLieOn (a, c).Is (-2, E); new Point2 (1, 3).GetLieOn (a, c).Is (0.25, E);
      new Point2 (40, 120).GetLieOn (a, c).Is (10, E);
   }

   [Test (20, "Point2.Midpoint, Move, Polar, Mirror, Rotate, Scale")]
   void Test10 () {
      Point2 a = new (13, 14), b = new (22, 30);
      EQ (a.Midpoint (b), new (17.5, 22));
      EQ (a.Moved (-2, -4), new (11, 10));
      EQ (a.Polar (2, Math.PI), new (11, 14));
      EQ (a.Polar (-Math.Sqrt (2), Math.PI / 4), new (12, 13));
      EQ (a.Mirrored (new (0, 0), new (0, 1)), new (-13, 14));
      EQ (a.Mirrored (new (0, 0), new (1, 0)), new (13, -14));
      EQ (a.Mirrored (a, a), a);
      EQ (a.Scaled (2), new (26, 28));
      EQ (a.Scaled (b, 0.5), new (17.5, 22));
      EQ (a.Scaled (b, -0.5), new (26.5, 38));
      EQ (a.Rotated (90.0.D2R ()), new (-14, 13));
      EQ (a.Rotated (b, 180.0.D2R ()), new (31, 46));
   }

   static Point2 PX (double a, double b) => new (a, b);
   static void EQ (Point2 pt, double a, double b) => pt.EQ (PX (a, b)).IsTrue ();
   static void EQ (Point2 pt, Point2 pt2) => pt.EQ (pt2).IsTrue ();
}

[Fixture (5, "Point3 tests", "Math")]
class Point3Tests {
   [Test (21, "class Point3")]
   void Test1 () {
      // Point3 tests
      Point3 pa = new (3, 5, 7); pa.Is ("(3,5,7)");
      Point3.Zero.Is ("(0,0,0)");
      Point3 pa2 = new (3.000001, 5, 7); pa.EQ (pa2).IsFalse ();
      Point3 pa3 = new (3.0000001, 5, 7); pa.EQ (pa3).IsTrue ();
      (pa + new Vector3 (10, 20, 30)).Is ("(13,25,37)");
      (pa - new Vector3 (0.1, 0.2, 0.3)).Is ("(2.9,4.8,6.7)");
      (new Point3 (10, 11, 12) - pa).Is ("<7,6,5>");

      Point3.Zero.DistTo (pa).Is (9.110434);

      // Vector3f tests
      Vec3F pfa = (Vec3F)pa2; pfa.Is ("<3,5,7>");
   }
}
