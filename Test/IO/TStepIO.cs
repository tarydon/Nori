// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TStepIO.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (28, "STEP reader tests", "STEP")]
class StepTests {
   [Test (135, "Basic STEP file import test")]
   void Test1 () {
      double old = Lib.FineTess;
      try {
         Lib.FineTess = 0.2;
         var sr = new STEPReader (NT.File ("STEP/S00178.stp"));
         var model = sr.Load ();
         var b = model.Bound;
         CurlWriter.Save (model, NT.TmpCurl, "S00178.stp");
         Assert.TextFilesEqual (NT.File ("STEP/S00178.curl"), NT.TmpCurl);

         var sb = new StringBuilder ();
         foreach (var e3s in model.Ents.OfType<E3Surface> ()) {
            var mesh = e3s.Mesh;
            sb.AppendLine ($"Entity: {e3s.GetType ().Name} #{e3s.Id}");
            sb.Append (mesh.ToTMesh ());
            sb.AppendLine ("----------------");
         }
         File.WriteAllText (NT.TmpTxt, sb.ToString ());
         Assert.TextFilesEqual (NT.File ("STEP/S00178.txt"), NT.TmpTxt);
      } finally {
         Lib.FineTess = old;
      }
   }

   [Test (184, "Import free curve from STEP file")]
   void Test2 () {
      var sr = new STEPReader (NT.File ("STEP/5X-039.stp"));
      var model = sr.Load ();

      var sb = new StringBuilder (); List<Point3> pts = [];
      foreach (var  cp in model.Ents.OfType<E3CompositePath> ()) {
         sb.AppendLine ("-----------------");
         foreach (var curve in cp.Curves) {
            sb.AppendLine (curve.GetType ().Name);
            curve.Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
            foreach (var pt in pts)
               sb.AppendLine (pt.ToString ());
            pts.Clear ();
         }
      }
      
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual (NT.File ("STEP/5X-039Curve.txt"), NT.TmpTxt);
   }
}
