// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TCollision.cs
// ║║║║╬║╔╣║ Tests connected with the collision system
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori.Testing;
namespace Nori.Testing;

[Fixture (35, "Collision tests", "Collide")]
class TCollision {
   [Test (174, "Generate OBB for real-world surfaces")]
   void Test1 () {
      Random r = new (1);
      var model = new T3XReader (NT.File ("IO/T3X/5X-022.t3x")).Load ();
      var surfaces = model.Ents.OfType<E3Surface> ().OrderByDescending (a => a.Mesh.GetArea ()).Take (40).ToList ();

      var sb = new StringBuilder ();
      double v1Total = 0, v2Total = 0;
      for (int i = 0; i < surfaces.Count; i++) {
         var surface = surfaces[i];
         double xR = GetAngle (), yR = GetAngle (), zR = GetAngle ();
         Vector3 mid = (Vector3)surface.Bound.Midpoint;
         var xfm = Matrix3.Translation (mid)
                 * Matrix3.Rotation (EAxis.X, xR) * Matrix3.Rotation (EAxis.Y, yR) * Matrix3.Rotation (EAxis.Z, zR)
                 * Matrix3.Translation (-mid);
         var pts = surface.Mesh.Vertex.Select (a => a.Pos * xfm).ToList ();
         var obb1 = OBB.Build (pts.AsSpan ());
         var obb2 = OBB.BuildFast (pts.AsSpan ());
         sb.AppendLine ($"{i + 1}, {pts.Count} points");
         Out ("OBB:  ", obb1);
         Out ("Fast: ", obb2);
         double aRatio = obb2.Area / obb1.Area, vRatio = obb2.Volume / obb1.Volume;
         sb.AppendLine ($"Ratio: V={vRatio.Round (3)}, A={aRatio.Round (3)}");
         v1Total += obb1.Volume; v2Total += obb2.Volume;
         sb.AppendLine ();
      }
      sb.AppendLine ($"Overall Volumes: {v2Total.Round (0)} / {v1Total.Round (0)}");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ("Sim/OBBGen.txt", NT.TmpTxt);

      // Helpers ...........................................
      void Out (string name, OBB o) {
         var c = o.Center;
         var x = o.X; var y = o.Y; var z = o.Z;
         var e = o.Extent;
         sb.AppendLine ($"{name} ({c.X.Round (2)},{c.Y.Round (2)},{c.Z.Round (2)})  {e.X.Round (2)}x{e.Y.Round (2)}x{e.Z.Round (2)}");
         sb.AppendLine ($"       <{x.X.Round (3)},{x.Y.Round (3)},{x.Z.Round (3)}>, <{y.X.Round (3)},{y.Y.Round (3)},{y.Z.Round (3)}>, <{z.X.Round (3)},{z.Y.Round (3)},{z.Z.Round (3)}>");
      }

      double GetAngle () => (r.NextDouble () - 0.5) * (90.D2R ());
   }
}
