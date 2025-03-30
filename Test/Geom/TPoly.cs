﻿// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TPoly.cs
// ║║║║╬║╔╣║ Tests for the Poly class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (15, "Poly class tests", "Geom")]
class PolyTests {
   [Test (24, "Basic constructors")]
   void Test1 () {
      Poly.Circle (new (10, 5), 3).Is ("C10,5,3");
      Poly.Circle (10, 5, 3).Is ("C10,5,3");
      Poly.Rectangle (new Bound2 (1, 2, 3, 4)).Is ("M1,2H3V4H1Z");
      Poly.Rectangle (3,4,1,2).Is ("M1,2H3V4H1Z");
      var p1 = Poly.Line (new (1, 2), new (3, 4));
      p1.Is ("M1,2L3,4");
      p1.A.Is ("(1,2)"); p1.B.Is ("(3,4)");
      p1.Pts.Length.Is (2);
      p1.IsLine.IsTrue (); p1.IsOpen.IsTrue ();
      Poly.Line (1, 2, 3, 4).Is ("M1,2L3,4");
      var p = Poly.Parse ("M0,0 H10 V3 Q8,5,1 H2 Q0,3,-1 Z");
      p.Is ("M0,0H10V3Q8,5,1H2Q0,3,-1Z");
      p.IsLine.IsFalse (); p.IsOpen.IsFalse ();
   }

   [Test (25, "Discretization, Seg enumerate, Xfm")]
   void Test2 () {
      var p = Poly.Parse ("M0,0 H10 V3 Q8,5,1 H2 Q0,3,-1 Z");
      var sb = new StringBuilder ();
      sb.Append ($"Segments of {p}:\n");
      foreach (var s in p.Segs)
         sb.Append ($"{s}  |  {s.IsArc} {s.IsCCW} {s.IsCircle} {s.IsLast}\n");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ($"{NT.Data}/Misc/poly.txt", NT.TmpTxt);

      List<Point2> pts = [];
      p.Discretize (pts, 0.05);
      sb.Clear ();
      sb.Append ($"Discretization of {p}:\n");   
      foreach (var pt in pts) sb.Append (pt.ToString () + "\n");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ($"{NT.Data}/Misc/poly2.txt", NT.TmpTxt);

      pts.Clear (); 
      Poly.Line (1, 2, 3, 4).Discretize (pts, 0.1);
      pts.Count.Is (2);
      
      Poly p1 = p * Matrix2.Translation (2, 1); p1.Is ("M2,1H12V4Q10,6,1H4Q2,4,-1Z");
      Poly p2 = p * Matrix2.Rotation (Lib.HalfPI); p2.Is ("M0,0V10H-3Q-5,8,1V2Q-3,0,-1Z");
      p.GetBound ().Is ("(0~10,0~5)");

      p.GetPerimeter ().Is (28.283185);
      p.GetBound ().Is ("(0~10,0~5)");
   }

   [Test (26, "Low level PolyBuilder tests")]
   void Test3 () {
      PB ().Line (1, 2).End (3, 4).Is ("M1,2L3,4");
      PB ().Line (new (1, 2)).End (new (3, 4)).Is ("M1,2L3,4");
      Poly.Parse ("M1,2L3,4.").Is ("M1,2L3,4");
      PB ().Arc (0, 0, 0, 5, Poly.EFlags.CCW).End (5, 5).Is ("M0,0Q5,5,1");
      PB ().Arc (0, 0, Math.Tan (90.D2R () / 4)).End (5, 5).Is ("M0,0Q5,5,1");
      Poly.Parse ("M1,2Q3,4,0").Is ("M1,2L3,4");
      Poly.Parse ("M0,0 L12,13 3,4 Z").Is ("M0,0L12,13L3,4Z");

      Exception? e = null;
      try { Poly.Parse ("M0,0F1,2Z"); } catch (Exception e1) { e = e1; }
      (e == null).IsFalse ();

      static PolyBuilder PB () => new ();
   }

   [Test (27, "Seg tests")]
   void Test4 () {
      var p = Poly.Parse ("M0,0 H10 V3 Q8,5,1 H2 Q0,3,-1 Z");
      var seg = p[0];
      seg.GetPointAt (0.25).Is ("(2.5,0)");
      seg.AngSpan.Is (0);
      seg.GetSlopeAt (0).Is (0);
      p[1].GetSlopeAt (0.5).Is (Lib.HalfPI);
      p[4].GetSlopeAt (0.5).Is (-135.D2R ());
      seg.GetLie (new (4, 0)).Is (0.4);

      p = Poly.Circle (0, 0, 5);
      seg = p[0]; seg.IsCircle.IsTrue ();
      seg.IsLast.IsTrue ();
      var (s, e) = seg.GetStartAndEndAngles ();
      s.Is (0); e.Is (Lib.TwoPI);
      seg.GetPointAt (0.75001).Is ("(0.000314,-5)");
      seg.GetSlopeAt (0.125).Is (135.D2R ());
      seg.GetLie (new (0, 5)).Is (0.25);
      seg.GetLie (new (-5, 0)).Is (0.5);

      StringBuilder sb = new ();
      List<Vec2F> bez = [];
      p = Poly.Parse ("M0,0 Q10,10,1");
      p[0].ToBeziers (bez);
      sb.Append ($"{p} to beziers:\n");
      foreach (var pt in bez) sb.Append ($"{pt}\n");

      bez.Clear ();
      p = Poly.Parse ("M0,0 Q-10,10,3");
      p[0].ToBeziers (bez);
      sb.Append ($"\n{p} to beziers:\n");
      foreach (var pt in bez) sb.Append ($"{pt}\n");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ($"{NT.Data}/Misc/poly3.txt", NT.TmpTxt);

      seg = new Seg (new (0, 10), new (-10, 0), Point2.Zero, Poly.EFlags.CCW);
      seg.Contains (new (0, 10)).IsTrue (); seg.Contains (new (-10, 0)).IsTrue ();
      seg.Contains (Point2.Zero.Polar (10, 135.D2R ())).IsTrue ();
      seg.Contains (new (0, -10)).IsFalse ();
      seg.Contains (new (10, 0)).IsFalse ();

      seg = new Seg (new (0, 10), new (-10, 0), Point2.Zero, Poly.EFlags.CW);
      seg.Contains (new (0, 10)).IsTrue (); seg.Contains (new (-10, 0)).IsTrue ();
      seg.Contains (Point2.Zero.Polar (10, 135.D2R ())).IsFalse ();
      seg.Contains (new (0, -10)).IsTrue ();
      seg.Contains (new (10, 0)).IsTrue ();
   }
}