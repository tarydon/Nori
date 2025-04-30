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
}

[Fixture (20, "Polygon boolean operations tests", "Geom")]
class BooleanOpsTests {
   [Test (60, "Basic boolean operations")]
   void Test1 () {
      List<Poly[]> polys = [ 
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
               0 => inset.UnionPolys (),
               1 => inset.Take (1).SubtractPolys (inset.Skip (1)),
               2 => inset.IntersectPolys (),
               _ => throw new NotImplementedException ()
            });
         }
         output.ForEach (poly => sb.AppendLine (poly.ToString ()));
         File.WriteAllText (NT.TmpTxt, sb.ToString ());
         Assert.TextFilesEqual ($"{NT.Data}/Geom/Poly/Boolean/basic-{ops[i]}.txt", NT.TmpTxt);
      }
   }
}
