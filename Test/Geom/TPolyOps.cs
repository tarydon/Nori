// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TPolyOps.cs
// ║║║║╬║╔╣║ Tests for the Poly mangler methods
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (19, "Poly node mangler tests", "Geom")]
class PolyOpsTests {
   [Test (58, "Poly in-fillet tests")]
   void Test1 () {
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      Poly? poly;
      poly = rect.InFillet (0, 25, left: true); poly!.Is ("M200,0V100H0V25Q25,0,-1Z");
      poly = rect.InFillet (0, 25, left: false); poly!.Is ("M200,0V100H0V25Q25,0,3Z");

      poly = rect.InFillet (1, 25, left: true); poly!.Is ("M0,0H175Q200,25,-1V100H0Z");
      poly = rect.InFillet (1, 25, left: false); poly!.Is ("M0,0H175Q200,25,3V100H0Z");

      poly = rect.InFillet (4, 25, left: true); poly!.Is ("M200,0V100H0V25Q25,0,-1Z");
      poly = rect.InFillet (4, 25, left: false); poly!.Is ("M200,0V100H0V25Q25,0,3Z");

      Poly cir = Poly.Circle ((0, 0), 50);
      poly = cir.InFillet (0, 10, true);
      Assert.IsTrue (poly is null);
   }

   [Test (60, "Poly corner-step tests")]
   void Test2 () {
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      Poly? poly;
      poly = rect.CornerStep (0, 20, 10, Poly.ECornerOpFlags.SameSideOfBothSegments); poly!.Is ("M200,0V100H0V20H10V0Z");
      poly = rect.CornerStep (0, 10, 20, Poly.ECornerOpFlags.SameSideOfBothSegments); poly!.Is ("M200,0V100H0V10H20V0Z");
      poly = rect.CornerStep (0, 20, 10, Poly.ECornerOpFlags.NearLeadOut); poly!.Is ("M200,0V100H0V-20H10V0Z");
      poly = rect.CornerStep (0, 20, 10, Poly.ECornerOpFlags.None); poly!.Is ("M200,0V100H0V20H-10V0Z");

      rect = Poly.Rectangle (0, 0, 40, 80);
      poly = rect.CornerStep (3, 50, 40, Poly.ECornerOpFlags.NearLeadOut); poly!.Is ("M0,0H40V80H-50V40H0Z");

      rect = Poly.Parse ("M0,0V100H200V0");
      poly = rect.CornerStep (1, 20, 10, Poly.ECornerOpFlags.SameSideOfBothSegments); poly!.Is ("M0,0V80H10V100H200V0");
      poly = rect.CornerStep (1, 20, 10, Poly.ECornerOpFlags.None); poly!.Is ("M0,0V80H-10V100H200V0");
   }

   [Test (67, "Poly chamfer tests")]
   void Test3 () {
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      Poly? poly;
      poly = rect.Chamfer (0, 20, 10); poly!.Is ("M200,0V100H0V20L10,0Z");
      poly = rect.Chamfer (0, 10, 20); poly!.Is ("M200,0V100H0V10L20,0Z");
      poly = rect.Chamfer (0, 10, 10); poly!.Is ("M200,0V100H0V10L10,0Z");
   }

   [Test (72, "Poly fillet tests")]
   void Test4 () {
      Poly tri = new ([new Point2 (0, 0), new Point2 (30, 0), new Point2 (15, 15)], [], Poly.EFlags.Closed);
      Poly? poly;
      poly = tri.Fillet (2, 5); poly?.Is ("M0,0H30L18.535534,11.464466Q11.464466,11.464466,1Z"); // 90 deg at node 2
      poly = tri.Fillet (0, 5); poly?.Is ("M30,0L15,15L8.535534,8.535534Q12.071068,0,1.5Z"); // 45 deg at node 0
      tri = new ([new Point2 (0, 0), new Point2 (50, 0), new Point2 (-25, 43.301)], [], Poly.EFlags.Closed);
      poly = tri.Fillet (0, 5); poly?.Is ("M50,0L-25,43.301L-1.443378,2.499988Q2.886742,0,0.666665Z"); // 120 deg at node 0
      // Reordered the points to verify the winding direction is changed
      tri = new ([new Point2 (0, 0), new Point2 (-25, 43.301), new Point2 (50, 0)], [], Poly.EFlags.Closed);
      poly = tri.Fillet (0, 5); poly?.Is ("M-25,43.301L50,0H2.886742Q-1.443378,2.499988,-0.666665Z"); // 120 deg at node 0
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      poly = rect.Fillet (0, 5); poly?.Is ("M200,0V100H0V5Q5,0,1Z");
      poly = rect.Fillet (3, 10); poly?.Is ("M0,0H200V100H10Q0,90,1Z");
   }
}

[Fixture (21, "Polygon boolean operations tests", "Geom")]
class BooleanOpsTests {
   [Test (69, "Basic boolean operations")]
   void Test1 () {
      List<List<Poly>> polys = [
         // Union => Star
         [Poly.Polygon ((300, 350), 300, 3),
         Poly.Polygon ((300, 350), 300, 3, Lib.PI)],

         // Rect - Rect contour overlap
         [Poly.Rectangle (1850, 150, 2300, 550),
         Poly.Rectangle (1850, 275, 2225, 425)],

         // Venn diagram (circle - circle - circle)
         [Poly.Circle ((375, 1250), 200),
         Poly.Circle ((250, 1000), 200),
         Poly.Circle ((500, 1000), 200)],

         // Multiple subtraction polys (rect - circle)
         [Poly.Rectangle (850, 50, 1350, 550),
         Poly.Circle ((1100, 300), 275)],

         // Multiple subtraction polys (circle - rect)
         [Poly.Circle ((1150, 1050), 250),
         Poly.Rectangle (850, 950, 1450, 1150)],

         // Results in multiple intersection polys
         [Poly.Rectangle (1700, 50, 2150, 650),
         Poly.Rectangle (1850, 150, 2300, 550).Subtract (Poly.Rectangle (1850, 275, 2225, 425)).First ()],

         // Combine outer with hole in subration
         [Poly.Parse ("M1700,850H2500V1250Q2300,1450,-1H1900Q1700,1250,1Z"),
         Poly.Circle ((2000, 1150), 180)]];

      string[] ops = ["union", "subtract", "intersect"];
      List<Poly> output = [];
      StringBuilder sb = new ();
      for (int i = 0; i < ops.Length; i++) {
         sb.Clear (); output.Clear ();
         foreach (var inset in polys) {
            output.AddRange (i switch {
               0 => inset.AsSpan ().Union (),
               1 => inset.AsSpan ()[..1].Subtract (inset.AsSpan ()[1..]),
               2 => inset.AsSpan ().Intersect (),
               _ => throw new NotImplementedException ()
            });
         }
         output.ForEach (poly => sb.AppendLine (poly.ToString ()));
         File.WriteAllText (NT.TmpTxt, sb.ToString ());
         Assert.TextFilesEqual1 ($"Geom/Poly/Boolean/basic-{ops[i]}.txt", NT.TmpTxt);
      }
   }
}

[Fixture (26, "Poly trim and extend tests", "Geom")]
class PolyTrimExtendTests {
   [Test (116, "Trim line seg")]
   void Test1 () {
      // Line seg in closed poly containing only lines
      Poly p = Poly.Rectangle (0, 0, 100, 50);
      List<Poly> polySoup = [p];
      List<Poly> resPolys = [.. p.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M100,0V50H0V0");

      polySoup.Add (Poly.Line (50, 0, 50, 50)); // Chop the rect at X == 50
      resPolys = [.. p.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M50,0H100V50H0V0");

      polySoup.Add (Poly.Line (75, 0, 75, 50)); // Chop the rect at X == 75
      resPolys = [.. p.TrimmedSeg (0, 0.6, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M75,0H100V50H0V0H50");

      // Line seg in closed poly containing arcs
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0 Q0,0,2 Z"); // Obround 120 x 50
      polySoup = [p];
      resPolys = [.. p.TrimmedSeg (0, 0.5, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M80,0Q80,50,2H0Q0,0,2");

      polySoup.Add (Poly.Line (20, 0, 20, 50)); // Chop the obround at X == 20
      polySoup.Add (Poly.Line (60, 0, 60, 50)); // Chop the obround at X == 60
      resPolys = [.. p.TrimmedSeg (0, 0.5, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M60,0H80Q80,50,2H0Q0,0,2H20");

      // Single line seg poly
      p = Poly.Line (0, 0, 100, 0);
      polySoup = [p];
      resPolys = [.. p.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (0);
   }

   [Test (117, "Trim circle")]
   void Test2 () {
      Poly c = Poly.Circle (0, 0, 40);
      List<Poly> polySoup = [c];
      List<Poly> resPolys = [.. c.TrimmedSeg (0, lie: 0, polySoup)]; // Left-over arc segment, if any.
      resPolys.Count.Is (0);
      resPolys = [.. c.TrimmedSeg (0, lie: 1, polySoup)];
      resPolys.Count.Is (0);
      resPolys = [.. c.TrimmedSeg (0, lie: 0.5, polySoup)];
      resPolys.Count.Is (0);

      Poly line0 = Poly.Line (0, 0, 50, 0), line180 = Poly.Line (0, 0, -50, 0);

      polySoup = [c, line0]; // Line cutting circle at 0
      resPolys = [.. c.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (0);

      polySoup = [c, line180];  // Line cutting circle at 180
      resPolys = [.. c.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (0);

      polySoup = [c, line0, line180];  // Line cutting circle at 0 and 180
      resPolys = [.. c.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M40,0Q-40,0,-2");
      resPolys = [.. c.TrimmedSeg (0, 0.8, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-40,0Q40,0,-2");

      polySoup = [c, Poly.Line (0, 0, 0, 50), Poly.Line (0, 0, 0, -50)];  // Line cutting circle at 90 and -90
      resPolys = [.. c.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-0,-40Q0,40,-2");
      resPolys = [.. c.TrimmedSeg (0, 0.8, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-0,-40Q0,40,-2");
      resPolys = [.. c.TrimmedSeg (0, 0.5, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M0,40Q-0,-40,-2");
   }

   [Test (118, "Trim arc seg")]
   void Test3 () {
      // Single arc seg poly
      Poly p = Poly.Arc (new Point2 (20, 0), Lib.HalfPI, new Point2 (0, 20));
      List<Poly> polySoup = [p];
      List<Poly> resPolys = [.. p.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (0);
      polySoup.Add (Poly.Line (0, 0, 20, 20));
      resPolys = [.. p.TrimmedSeg (0, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M14.142136,14.142136Q0,20,0.5");
      resPolys = [.. p.TrimmedSeg (0, 0.8, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M20,0Q14.142136,14.142136,0.5");

      // Arc seg in closed poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0 Q0,0,2 Z"); // Obround 120 x 50
      polySoup = [p];
      resPolys = [.. p.TrimmedSeg (1, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M80,50H0Q0,0,2H80");

      polySoup.Add (Poly.Line (-25, 10, 145, 10));
      resPolys = [.. p.TrimmedSeg (1, 0.2, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M100,10Q80,50,1.409666H0Q0,0,2H80");
      resPolys = [.. p.TrimmedSeg (1, 0.8, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M80,50H0Q0,0,2H80Q100,10,0.590334");

      // Arc seg in open poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0"); // Obround-like, with left arc missing
      polySoup = [p, Poly.Line (-25, 10, 145, 10)];
      resPolys = [.. p.TrimmedSeg (1, 0.2, polySoup)];
      resPolys.Count.Is (2);
      resPolys[0].Is ("M0,0H80");
      resPolys[1].Is ("M100,10Q80,50,1.409666H0");
      resPolys = [.. p.TrimmedSeg (1, 0.8, polySoup)];
      resPolys.Count.Is (2);
      resPolys[0].Is ("M0,0H80Q100,10,0.590334");
      resPolys[1].Is ("M80,50H0");
   }

   [Test (119, "Extend line seg")]
   void Test5 () {
      // Single line seg
      Poly p = Poly.Line (0, 0, 100, 0);
      List<Poly> polySoup = [p];
      List<Poly> resPolys = [.. p.ExtendedSeg (0, 0.2, dist: 0, polySoup)];
      resPolys.Count.Is (0);
      resPolys = [.. p.ExtendedSeg (0, 0.2, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-10,0H100");
      resPolys = [.. p.ExtendedSeg (0, 0.8, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M0,0H110");

      // Closed poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0 Q0,0,2 Z"); // Obround 120 x 50
      polySoup = [p];
      resPolys = [.. p.ExtendedSeg (0, 0.2, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-10,0H80Q80,50,2H0Q0,0,2");
      resPolys = [.. p.ExtendedSeg (0, 0.8, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M80,0Q80,50,2H0Q0,0,2H90");
      resPolys = [.. p.ExtendedSeg (2, 0.2, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M90,50H0Q0,0,2H80Q80,50,2");
      resPolys = [.. p.ExtendedSeg (2, 0.8, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M0,50Q0,0,2H80Q80,50,2H-10");

      // Open poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0"); // Obround-like, with left arc missing
      polySoup = [p];
      resPolys = [.. p.ExtendedSeg (0, 0.2, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M-10,0H80Q80,50,2H0");
      resPolys = [.. p.ExtendedSeg (0, 0.8, dist: 10, polySoup)];
      resPolys.Count.Is (2);
      resPolys[0].Is ("M0,0H90");
      resPolys[1].Is ("M80,0Q80,50,2H0");
      resPolys = [.. p.ExtendedSeg (2, 0.2, dist: 10, polySoup)];
      resPolys.Count.Is (2);
      resPolys[0].Is ("M0,0H80Q80,50,2");
      resPolys[1].Is ("M90,50H0");
      resPolys = [.. p.ExtendedSeg (2, 0.8, dist: 10, polySoup)];
      resPolys.Count.Is (1); resPolys[0].Is ("M0,0H80Q80,50,2H-10");
   }

   [Test (120, "Extend arc seg")]
   void Test6 () {
      // Unobstructed arc seg extension - single arc
      Poly p = Poly.Arc (new Point2 (50, 0), Lib.HalfPI, new Point2 (0, 50));
      List<Poly> polySoup = [p];
      List<Poly> resPoly = [.. p.ExtendedSeg (0, 0.2, dist: 0, polySoup)];
      resPoly.Count.Is (1); resPoly[0].Is ("C-0,0,50");
      resPoly = [.. p.ExtendedSeg (0, 0.8, dist: 0, polySoup)];
      resPoly.Count.Is (1); resPoly[0].Is ("C-0,0,50");

      // Unobstructed arc seg extension - closed poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0 Q0,0,2 Z"); // Obround 120 x 50
      polySoup = [p];
      resPoly = [.. p.ExtendedSeg (1, 0.2, dist: 0, polySoup)];
      resPoly.Count.Is (2);
      resPoly[0].Is ("C80,25,25");
      resPoly[1].Is ("M80,50H0Q0,0,2H80");
      resPoly = [.. p.ExtendedSeg (1, 0.8, dist: 0, polySoup)];
      resPoly.Count.Is (2); // Observe this result is identical to 0.2 case!
      resPoly[0].Is ("C80,25,25");
      resPoly[1].Is ("M80,50H0Q0,0,2H80");

      // Unobstructed arc seg extension - open poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0"); // Obround-like, with left arc missing
      polySoup = [p];
      resPoly = [.. p.ExtendedSeg (1, 0.2, dist: 0, polySoup)];
      resPoly.Count.Is (3);
      resPoly[0].Is ("C80,25,25");
      resPoly[1].Is ("M0,0H80");
      resPoly[2].Is ("M80,50H0");
      resPoly = [.. p.ExtendedSeg (1, 0.8, dist: 0, polySoup)];
      resPoly.Count.Is (3); // Observe this result is identical to 0.2 case!
      resPoly[0].Is ("C80,25,25");
      resPoly[1].Is ("M0,0H80");
      resPoly[2].Is ("M80,50H0");

      // Obstructed arc seg extension - closed poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0 Q0,0,2 Z"); // Obround 120 x 50
      polySoup = [p, Poly.Line (0, 10, 80, 10), Poly.Line (0, 40, 80, 40)];
      resPoly = [.. p.ExtendedSeg (1, 0.2, dist: 0, polySoup)];
      resPoly.Count.Is (1);
      resPoly[0].Is ("M60,10Q80,50,2.590334H0Q0,0,2H80");
      resPoly = [.. p.ExtendedSeg (1, 0.8, dist: 0, polySoup)];
      resPoly.Count.Is (1);
      resPoly[0].Is ("M80,50H0Q0,0,2H80Q60,40,2.590334");

      // Obstructed arc seg extension - open poly
      p = Poly.Parse ("M0,0 H80 Q80,50,2 H0"); // Obround-like, with left arc missing
      polySoup = [p, Poly.Line (0, 10, 80, 10), Poly.Line (0, 40, 80, 40)];
      resPoly = [.. p.ExtendedSeg (1, 0.2, dist: 0, polySoup)];
      resPoly.Count.Is (2);
      resPoly[0].Is ("M0,0H80");
      resPoly[1].Is ("M60,10Q80,50,2.590334H0");
      resPoly = [.. p.ExtendedSeg (1, 0.8, dist: 0, polySoup)];
      resPoly.Count.Is (2);
      resPoly[0].Is ("M0,0H80Q60,40,2.590334");
      resPoly[1].Is ("M80,50H0");
   }
}

[Fixture (25, "Poly edge mangler tests", "Geom")]
class PolyEdgeTests {
   [Test (113, "Poly edge-recess")]
   void Test1 () {
      Poly rect = Poly.Rectangle (0, 0, 100, 50);
      Poly? poly;
      poly = rect.EdgeRecess (0, left: true, 15, 20, 10); poly!.Is ("M0,0H5V10H25V0H100V50H0Z");
      poly = rect.EdgeRecess (0, left: false, 15, 20, 10); poly!.Is ("M0,0H5V-10H25V0H100V50H0Z");

      Poly line = Poly.Line (0, 0, 100, 0);
      poly = line.EdgeRecess (0, left: true, 15, 20, 10); poly!.Is ("M0,0H5V10H25V0H100");
      poly = line.EdgeRecess (0, left: false, 15, 20, 10); poly!.Is ("M0,0H5V-10H25V0H100");

      // Rect with rounded corner.
      Poly rounded = Poly.Rectangle (0, 0, 100, 50)!.Fillet (0, 5)!;
      poly = rounded.EdgeRecess (0, left: true, 15, 20, 10); poly!.Is ("M100,0V5H90V25H100V50H0V5Q5,0,1Z");
      poly = rounded.EdgeRecess (0, left: false, 15, 20, 10); poly!.Is ("M100,0V5H110V25H100V50H0V5Q5,0,1Z");
   }

   [Test (114, "Poly Edge-V")]
   void Test2 () {
      Poly rect = Poly.Rectangle (0, 0, 100, 50);
      Poly? poly;
      poly = rect.VNotch (0, 15, 20, 10); poly!.Is ("M0,0H5L15,10L25,0H100V50H0Z");
      poly = rect.VNotch (0, 15, 20, -10); poly!.Is ("M0,0H5L15,-10L25,0H100V50H0Z");
      poly = rect.VNotch (0, 15, 20, 0); Assert.IsTrue (poly is null);

      Poly line = Poly.Line (0, 0, 100, 0);
      poly = line.VNotch (0, 15, 20, 10); poly!.Is ("M0,0H5L15,10L25,0H100");
      poly = line.VNotch (0, 15, 20, -10); poly!.Is ("M0,0H5L15,-10L25,0H100");
   }

   [Test (115, "Poly Edge U-Notch")]
   void Test3 () {
      Poly rect = Poly.Rectangle (0, 0, 60, 50);
      Poly? poly;
      poly = rect.UNotch (0, 10, 10, 10, 2); poly!.Is ("M0,0H5V8Q7,10,-1H13Q15,8,-1V0H60V50H0Z");
      poly = rect.UNotch (0, 10, 10, -10, 2); poly!.Is ("M0,0H5V-8Q7,-10,1H13Q15,-8,1V0H60V50H0Z");
      poly = rect.UNotch (0, 10, 10, 10, 0); poly!.Is ("M0,0H5V5Q10,10,-1V10Q15,5,-1V0H60V50H0Z");
      poly = rect.UNotch (0, 30, 70, 10, 10); Assert.IsTrue (poly is null); // Notch doesn't fit in the seg
      poly = rect.UNotch (0, 10, 10, 0, 2); Assert.IsTrue (poly is null); // Depth is zero

      Poly line = Poly.Line (0, 0, 50, 0);
      poly = line.UNotch (0, 5, 10, 10, 2); poly!.Is ("M0,0V8Q2,10,-1H8Q10,8,-1V0H50");
      poly = line.UNotch (0, 5, 10, -10, 2); poly!.Is ("M0,0V-8Q2,-10,1H8Q10,-8,1V0H50");
   }

   [Test (121, "Poly Key Slot")]
   void Test4 () {
      int width = 20, depth = 10, radius = 30;
      double startAng = 340.D2R (), endAng = 320.D2R (), rad90 = 90.D2R (), rad180 = 180.D2R (), rad270 = 270.D2R ();
      Poly circle = Poly.Circle (0, 0, radius), arcCCW = Poly.Arc (Point2.Zero, radius, startAng, endAng, true),
           arcCW = Poly.Arc (Point2.Zero, radius, endAng, startAng, false);
      Poly? poly;

      // Check key slot operation for a circle entity
      poly = circle.KeySlot (0, width, depth, 0); poly?.Is ("M28.284271,-10H20V10H28.284271Q28.284271,-10,3.567306Z");
      poly = circle.KeySlot (0, width, -depth, rad90); poly?.Is ("M10,28.284271V40H-10V28.284271Q10,28.284271,3.567306Z");
      poly = circle.KeySlot (0, width, depth, rad180); poly?.Is ("M-28.284271,10H-20V-10H-28.284271Q-28.284271,10,3.567306Z");
      poly = circle.KeySlot (0, width, -depth, rad270); poly?.Is ("M-10,-28.284271V-40H10V-28.284271Q-10,-28.284271,3.567306Z");

      // Check key slot operation for an arc entity
      // Counter clockwise direction arc
      poly = arcCCW.KeySlot (0, width, depth, 0);
      poly?.Is ("M28.190779,-10.260604Q28.284271,-10,0.005875H20V10H28.284271Q22.981333,-19.283628,3.339209");
      poly = arcCCW.KeySlot (0, width, -depth, rad90);
      poly?.Is ("M28.190779,-10.260604Q10,28.284271,1.005875V40H-10V28.284271Q22.981333,-19.283628,2.339209");

      // Clockwise direction arc
      poly = arcCW.KeySlot (0, width, depth, rad180);
      poly?.Is ("M22.981333,-19.283628Q-28.284271,-10,-1.339209H-20V10H-28.284271Q28.190779,-10.260604,-2.005875");
      poly = arcCW.KeySlot (0, width, -depth, rad270);
      poly?.Is ("M22.981333,-19.283628Q10,-28.284271,-0.339209V-40H-10V-28.284271Q28.190779,-10.260604,-3.005875");

      // Check fail scenario
      poly = circle.KeySlot (0, 3 * width, depth, 0); Assert.IsTrue (poly is null); // Notch doesn't fit in the seg
      poly = circle.KeySlot (0, width, 0, 0); Assert.IsTrue (poly is null); // depth is zero
      poly = arcCW.KeySlot (0, width, depth, startAng); Assert.IsTrue (poly is null); // Notch doesn't fit in the seg
   }
}