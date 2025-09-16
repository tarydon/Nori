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

   [Test (118, "Parallel Poly tests")]
   void Test5 () {
      Poly line = Poly.Line (new Point2 (10, 10), new Point2 (45.355339, 45.355339));
      var lineseg = new Seg (line, 0);
      Poly result = lineseg.MakeParallel (10, 0, 0, 0);
      result.Is ("M2.928932,17.071068L38.284271,52.426407");
      result = lineseg.MakeParallel (-10, 0, 0, 0);
      result.Is ("M17.071068,2.928932L52.426407,38.284271");
      result = lineseg.MakeParallel (15, 10, 5, 0.3);
      result.Is ("M-7.67767,13.535534L38.284271,59.497475");
      result = lineseg.MakeParallel (-15, 10, 5, 0.8);
      result.Is ("M17.071068,-4.142136L63.033009,41.819805");
      Poly arc = Poly.Arc (new Point2 (120, 35), 25, -2.4980915447965089, -0.64350110879328437, true);
      var arcseg = new Seg (arc, 0);
      result = arcseg.MakeParallel (-10, 0, 0, 0);
      result.Is ("M92,14Q148,14,1.180669");
      result = arcseg.MakeParallel (10, 0, 0, 0);
      result.Is ("M108,26Q132,26,1.180669");
      result = arcseg.MakeParallel (30, 0, 0, 0);
      result.Is ("M124,38Q116,38,-2.819331");
      Poly circle = Poly.Circle (new Point2 (40, 150), 50);
      var circleseg = new Seg (circle, 0);
      result = circleseg.MakeParallel (30, 0, 0, 0);
      result.Is ("C40,150,20");
      result = circleseg.MakeParallel (-10, 0, 0, 0);
      result.Is ("C40,150,60");
      result = circleseg.MakeParallel (55, 0, 0, 0);
      result.Is ("C40,150,5");
      Poly rect = Poly.Rectangle (150, 100, 230, 150);
      rect = rect.Fillet (2, 5)!;
      rect = rect.Chamfer (4, 5, 5)!;
      rect = rect.InFillet (0, 5, true)!;
      rect = rect.CornerStep (0, 5, 5,Poly.ECornerOpFlags.SameSideOfBothSegments)!;
      result = rect[4].MakeParallel (5, 0, 0, 0);
      result.Is ("M150,110Q160,100,-1");
      result = rect[7].MakeParallel (5, 0, 0, 0);
      result.Is ("M225,110H230");
      result = rect[0].MakeParallel (-5, 0, 0, 0);
      result.Is ("M235,145Q225,155,1");
      result = rect[1].MakeParallel (-5, 0, 0, 0);
      result.Is ("M225,155H155");
      result = rect[2].MakeParallel (-5, 0, 0, 0);
      result.Is ("M151.464466,153.535534L146.464466,148.535534");
      Poly poly = Poly.Polygon (new Point2 (240, 40), 20, 6, 45);
      poly = poly.Fillet (0, -5)!;
      poly = poly.Chamfer (1, 5, 5)!;
      poly = poly.InFillet (4, 5, true)!;
      result = poly[7].MakeParallel (-5, 0, 0, 0);
      result.Is ("M223.152193,56.277431Q217.898973,47.768396,0.666667");
      result = poly[1].MakeParallel (-5, 0, 0, 0);
      result.Is ("M235.008222,17.637483L243.664709,17.382089");
      result = poly[4].MakeParallel (2, 0, 0, 0);
      result.Is ("M257.401446,42.487906Q251.651554,53.162115,-1.333333");
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
}