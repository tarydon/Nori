// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TPolyOps.cs
// ║║║║╬║╔╣║ Tests for the Poly mangler methods
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (19, "Poly node mangler tests", "Geom")]
class PolyOpsTests {
   [Test (58, "Poly fillet tests")]
   void Test1 () {
      Poly rect = Poly.Rectangle (0, 0, 200, 100);
      Poly? poly;
      poly = rect.Fillet (0, 25, left: true); poly!.Is ("M200,0V100H0V25Q25,0,-1Z");
      poly = rect.Fillet (0, 25, left: false); poly!.Is ("M200,0V100H0V25Q25,0,3Z");

      poly = rect.Fillet (1, 25, left: true); poly!.Is ("M0,0H175Q200,25,-1V100H0Z");
      poly = rect.Fillet (1, 25, left: false); poly!.Is ("M0,0H175Q200,25,3V100H0Z");

      poly = rect.Fillet (4, 25, left: true); poly!.Is ("M200,0V100H0V25Q25,0,-1Z");
      poly = rect.Fillet (4, 25, left: false); poly!.Is ("M200,0V100H0V25Q25,0,3Z");
   }
}