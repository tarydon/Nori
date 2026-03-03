// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TCollision.cs
// ║║║║╬║╔╣║ Tests connected with the collision system
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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

   [Test (1001, "OBB x OBB collision checks")]
   void Test2 () {
      Vector3f ext = new (30, 20, 10);
      var a = new OBB (Point3f.Zero, Vector3f.XAxis, Vector3f.YAxis, ext);
      var b = new OBB (Point3f.Zero, Vector3f.YAxis, Vector3f.XAxis, ext);
      Collision.Check (a, b).IsTrue ();
      // Faces touching each other at X = 50
      Collision.Check (a, b * Matrix3.Translation (50, 0, 0)).IsTrue ();
      // Identical OBBs should report a collision
      Collision.Check (b, b).IsTrue ();

      // Collisions rejected by 'a' sperating axes
      Collision.Check (a, b * Matrix3.Translation (50.001, 0, 0)).IsFalse (); // aX
      Collision.Check (a, b * Matrix3.Translation (0, 50.001, 0)).IsFalse (); // aY
      Collision.Check (a, b * Matrix3.Translation (0, 0, 20.001)).IsFalse (); // aZ

      // Collisions rejected by 'b' sperating axes
      b = new OBB (new (50, 50, 0), V (1, 1, 0), V (-1, 1, 0), ext);
      Collision.Check (a, b).IsFalse (); // bX
      b = new OBB (new (45, 0, 25), V (0, 1, 0), V (1, 0, 1), ext);
      Collision.Check (a, b).IsFalse (); // bY
      b = new OBB (new (40, 0, 15), V (0, 1, 0), V (-1, 0, 1), ext);
      Collision.Check (a, b).IsFalse (); // bZ

      // Edge-Edge (fix the OBB pair and permute axes by changing both obb orientations)
      Point3f cen = new (40, 0, 40);
      // Pick two mutually perpendicular vectors as OBB axes.
      Vector3f v1 = V (1, 1, 1), v2 = V (1, 0, -1);
      ext = new (20, 20, 20);
      // Constrcut OBBs with two facing edges
      a = new OBB (Point3f.Zero, Vector3f.XAxis, Vector3f.YAxis, ext);
      b = new OBB (cen, v1, v2, ext);
      Collision.Check (a, b).IsFalse ();  // aY x bY

      a = new OBB (Point3f.Zero, Vector3f.YAxis, Vector3f.ZAxis, ext);
      Collision.Check (a, b).IsFalse ();  // aX x bY
      Collision.Check (b, a).IsFalse ();  // aY x bX

      b = new OBB (cen, v2, v1, ext);
      Collision.Check (b, a).IsFalse ();  // aX x bX

      a = new OBB (Point3f.Zero, Vector3f.XAxis, Vector3f.ZAxis, ext);
      b = new OBB (cen, v1, v2, ext);
      Collision.Check (a, b).IsFalse ();  // aZ x bY
      Collision.Check (b, a).IsFalse ();  // aY x bZ

      b = new OBB (cen, v2, v1, ext);
      Collision.Check (a, b).IsFalse ();  // aZ x bX
      Collision.Check (b, a).IsFalse ();  // aX x bZ

      b = new OBB (cen, (v1 * v2).Normalized (), v1, v2, ext);
      Collision.Check (b, a).IsFalse ();  // aZ x bZ

      static Vector3f V (float x,  float y, float z) => new Vector3f (x, y, z).Normalized ();
   }
}
