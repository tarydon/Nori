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
   }

   [Test (59, "Poly corner-step tests")]
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

   [Test (60, "Poly chamfer tests")]
   void Test3 () {
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      Poly? poly;
      poly = rect.Chamfer (0, 20, 10); poly!.Is ("M200,0V100H0V20L10,0Z");
      poly = rect.Chamfer (0, 10, 20); poly!.Is ("M200,0V100H0V10L20,0Z");
      poly = rect.Chamfer (0, 10, 10); poly!.Is ("M200,0V100H0V10L10,0Z");
   }
}