// ────── ╔╗                                                                                   TEST
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

      var p2 = Poly.Arc (new (0, 1), 1, 180.D2R(), 0, false);
      p2.A.Is ("(-1,1)"); p2.B.Is ("(1,1)");
      p2.Is ("M-1,1Q1,1,-2"); p2.HasArcs.IsTrue (); p2.IsOpen.IsTrue (); 
      var p3 = Poly.Arc (new (0, 1), 1, 180.D2R (), 0, true);
      p3.Is ("M-1,1Q1,1,2"); p3.HasArcs.IsTrue (); p3.IsOpen.IsTrue ();
      var p4 = Poly.Arc (new (0, 0), 90.D2R (), new (10, 0));
      p4.A.Is ("(0,0)"); p4.B.Is ("(10,0)");
      p4.Is ("M0,0Q10,0,-2"); p4.HasArcs.IsTrue (); p4.IsOpen.IsTrue ();
      var p5 = Poly.Arc (new (0, 0), 90.D2R (), new (-10, 0));
      p5.Is ("M0,0Q-10,0,2"); p5.HasArcs.IsTrue (); p5.IsOpen.IsTrue ();
      Poly.Arc (new (0, 0), 0, (10, 0)).IsLine.IsTrue ();
      var p6 = Poly.Arc (new (0, 0), 45.D2R (), (-5, -5));
      p6.HasArcs.IsFalse (); p6.Is ("M0,0L-5,-5");
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
      Poly.Parse ("M5,3 H10 V6 Z").Is ("M5,3H10V6Z");
      Poly.Parse ("M5,3 L10,3 10,6 Z").Is ("M5,3H10V6Z");

      // These should all crash
      string message = "A";
      try { Poly.Parse ("M0,0F1,2Z"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("ParseException: Unexpected mode 'F' in Poly.Parse");
      message = "B";
      try { Poly.Parse ("M0,0L"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("ParseException: At (1,6): Expecting double value");
      message = "C";
      try { Poly.Parse ("M0,0L3"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("ParseException: At (1,7): Expecting double value");
      message = "D";
      try { Poly.Parse ("M123.456,456.789\nL12.3,"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("ParseException: At (2,7): Expecting double value");
      message = "E";
      try { Poly.Parse ("L0,0"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("ParseException: Poly should start with 'M' or 'C'");
      // And this should work, thouhg the string is spread over 2 lines
      message = "OK";
      try { Poly.Parse ("M123.456,456.789\nL12.3,5"); } catch (Exception e1) { message = e1.Description (); }
      message.Is ("OK");

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

   [Test (73, "Poly.Reversed tests")]
   void Test5 () {
      // Line
      var p1 = Poly.Lines ([(0,10), (10,20), (20,30)]);
      p1.Reversed ().Is ("M20,30L10,20L0,10Z");
      // Arc
      var ccw = true;
      var p2 = Poly.Arc (new (0, 10), 20, 180.D2R (), 0, ccw);
      var p2r = p2.Reversed (); var s1 = p2r[0];
      s1.IsArc.IsTrue (); s1.IsCCW.Is (!ccw);
      p2r.A.Is (p2.B); p2r.B.Is (p2.A);
      // Circle
      var cen = new Point2 (0, 10); var rad = 10;
      var p3 = Poly.Circle (cen, rad);
      var p3r = p3.Reversed (); var s2 = p3r[0];
      p3r.IsCircle.IsTrue (); p3r.IsClosed.IsTrue ();
      s2.IsCCW.IsFalse (); s2.Center.Is (cen); s2.Radius.Is (rad);
      // A closed poly with lines and arcs.
      var p4 = Poly.Parse ("M0,0H500V200Q400,300,-1H100Q0,200,1Z");
      p4.Reversed ().Is ("M0,0V200Q100,300,-1H400Q500,200,1V0Z");
   }

   [Test (28, "Poly.GetWinding tests")]
   void Test6 () {
      var poly = Poly.Parse ("M0,0 L10,0 10,10 0,10 Z");
      Assert.IsTrue (poly.GetWinding () is Poly.EWinding.CCW);
      var poly1 = Poly.Parse ("M10,0 L0,0 0,10 10,10Z");
      Assert.IsTrue (poly1.GetWinding () is Poly.EWinding.CW);
      var poly2 = Poly.Parse ("M0,0 L10,10");
      Assert.IsTrue (poly2.GetWinding () is Poly.EWinding.Indeterminate);
      var poly3 = Poly.Parse ("C10,5,3");
      Assert.IsTrue (poly3.GetWinding () is Poly.EWinding.CCW);
   }

   [Test (74, "Poly.Append tests")]
   void Test7 () {
      Poly p = Poly.Parse ("M0,50 V0 H100 V50Z"), other = Poly.Parse ("M0,50 H10");
      p.TryAppend (other, out Poly? p1); p1?.Is (false);       // Can't append to a closed pline
      p = Poly.Parse ("M0,50 V0 H100 V50"); other = Poly.Parse ("M100,50 H110");
      p.TryAppend (other, out Poly? p2);
      p2?.Is ("M0,50V0H100V50H110");                           // Normal append
      other = Poly.Parse ("M110,50 H100");
      p.TryAppend (other, out Poly? p3);
      p3?.Is ("M0,50V0H100V50H110");                           // Flip pline then append
      other = Poly.Parse ("M10,50 H0");
      p.TryAppend (other, out Poly? p4);
      p4?.Is ("M10,50H0V0H100V50");                            // Flip pline, and prepend
      other = Poly.Parse ("M0,50 H10");
      p.TryAppend (other, out Poly? p5);
      p5?.Is ("M10,50H0V0H100V50");                            // Prepend seg
      other = Poly.Parse ("M0,50 H100");
      p.TryAppend (other, out Poly? p6);
      p6?.Is ("M0,50V0H100V50Z");                              // Result is closed
      other = Poly.Arc (new (98, 50), 2, 180.D2R (), 0, false);
      p.TryAppend (other, out Poly? P7);                       // Arc append
      P7?.Is ("M0,50V0H100V50Q96,50,2");
      other = Poly.Arc (new Point2 (2, 50), 2, 180.D2R (), 0, false);
      p.TryAppend (other, out Poly? p8);                       // Arc prepend
      p8?.Is ("M4,50Q0,50,2V0H100V50");
   }
}
