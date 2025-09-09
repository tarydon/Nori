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

[Fixture (25, "Poly edge mangler tests", "Geom")]
class PolyEdgeTests {
   [Test (113, "Poly edge-recess")]
   void Test1 () {
      Poly rect = Poly.Rectangle (0, 0, 100, 50);
      Poly? poly;
      var mode = Poly.EEdgeNotch.EdgeRecess;
      poly = rect.EdgeNotch (mode, 0, left: true, 15, 20, 10); poly!.Is ("M0,0H5V10H25V0H100V50H0Z");
      poly = rect.EdgeNotch (mode, 0, left: false, 15, 20, 10); poly!.Is ("M0,0H5V-10H25V0H100V50H0Z");

      Poly line = Poly.Line (0, 0, 100, 0);
      poly = line.EdgeNotch (mode, 0, left: true, 15, 20, 10); poly!.Is ("M0,0H5V10H25V0H100");
      poly = line.EdgeNotch (mode, 0, left: false, 15, 20, 10); poly!.Is ("M0,0H5V-10H25V0H100");

      // Rect with rounded corner.
      Poly rounded = Poly.Rectangle (0, 0, 100, 50)!.Fillet (0, 5)!;
      poly = rounded.EdgeNotch (mode, 0, left: true, 15, 20, 10); poly!.Is ("M100,0V5H90V25H100V50H0V5Q5,0,1Z");
      poly = rounded.EdgeNotch (mode, 0, left: false, 15, 20, 10); poly!.Is ("M100,0V5H110V25H100V50H0V5Q5,0,1Z");
   }

   [Test (114, "Poly Edge-U Notch")]
   void Test2 () {
      Poly rect = Poly.Rectangle (0, 0, 60, 50);
      Poly? poly;
      var mode = Poly.EEdgeNotch.EdgeU;
      poly = rect.EdgeNotch (mode, 0, left: true, 10, 10, 10, 2); poly!.Is ("M0,0H5V8Q7,10,-1H13Q15,8,-1V0H60V50H0Z");
      poly = rect.EdgeNotch (mode, 0, left: false, 10, 10, 10, 2); poly!.Is ("M0,0H5V-8Q7,-10,1H13Q15,-8,1V0H60V50H0Z");

      Poly line = Poly.Line (0, 0, 50, 0);
      poly = line.EdgeNotch (mode, 0, left: true, 5, 10, 10, 2); poly!.Is ("M0,0V8Q2,10,-1H8Q10,8,-1V0H50");
      poly = line.EdgeNotch (mode, 0, left: false, 5, 10, 10, 2); poly!.Is ("M0,0V-8Q2,-10,1H8Q10,-8,1V0H50");
   }
}