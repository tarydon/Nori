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

   [Test (185, "OBB x OBB collision checks")]
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

      static Vector3f V (float x, float y, float z) => new Vector3f (x, y, z).Normalized ();
   }

   [Test (186, "OBB x Tri collision checks")]
   void Test3 () {
      Vector3f ext = new (20, 20, 20);
      Span<Point3f> pts = [new (20, 20, 20), new (-20, 20, -20), new (20, -20, -20)];
      Span<Vector3f> axes = [Vector3f.XAxis, Vector3f.YAxis, Vector3f.ZAxis];

      CTri a = new (pts, 0, 1, 2);
      var b = new OBB (Point3f.Zero, Vector3f.XAxis, Vector3f.YAxis, ext);
      Collision.Check (pts, a, b).IsTrue (); // Fully intersecting.

      for (int i = 0; i < pts.Length; i++) pts[i] += ext;
      Collision.Check (pts, a, b).IsTrue (); // Just brushing

      // Nudge a little away from box center for edge-edge tests
      for (int i = 0; i < pts.Length; i++) pts[i] *= 1.001f;
      // 9 box-edge x tri-edge tests
      for (int i = 0; i < 3; i++) {
         a = new (pts, i, (i + 1) % 3, (i + 2) % 3);
         for (int j = 0; j < 3; j++) {
            b = new (Point3f.Zero, axes[j], axes[(j + 1) % 3], ext);
            Collision.Check (pts, a, b).IsFalse ();
         }
      }

      // Separating axis is along the triangle normal
      pts = [new (20, 20, 20), new (-20, 20, -20), new (20, -20, -20)];
      Vector3f dv = new (-30, -30, 30);
      for (int i = 0; i < pts.Length; i++) pts[i] += dv;
      a = new (pts, 0, 1, 2);
      Collision.Check (pts, a, b).IsFalse ();

      // Separating axis is along the face normals
      b = new (new (0.9, -0.5, 0.6), new (-0.24945, -0.91593, -0.31441), new (0.24941, 0.25295, -0.93478), new (0.254, 0.345, 0.045));
      pts = [new (1.0171, -0.55381, 0.30545), new (1.00904, -0.55962, 0.28594), new (1.00279, -0.54027, 0.29408)];
      a = new (pts, 0, 1, 2);
      Collision.Check (pts, a, b).IsFalse ();
   }

   [Test (187, "Tri x Tri collision checks")]
   void Test4 () {
      var lines = File.ReadAllLines (NT.File ("Sim/TriTri.txt"));
      List<bool> crashes = [];
      List<Point3f> list = [];
      for (int i = 0; i < lines.Length; i++) {
         string line = lines[i];
         if (i % 3 == 2) crashes.Add (line.Trim () == "1");
         else {
            float[] f = [.. line.Split ().Select (float.Parse)];
            for (int j = 0; j < 9; j += 3) {
               list.Add (new (f[j], f[j + 1], f[j + 2]));
            }
         }
      }
      var pts = list.AsSpan ();
      var tris = new CTri[list.Count / 3];
      for (int i = 0, j = 0; i < tris.Length; i++, j += 3)
         tris[i] = new CTri (pts, j, j + 1, j + 2);

      for (int i = 0, j = 0; i < crashes.Count; i++, j += 2)
         Collision.Check (pts, tris[j], pts, tris[j + 1]).Is (crashes[i]);
   }

   enum EDir { Left = -1, Root = 0, Right = 1 }

   [Test (188, "OBBTree test")]
   void Test5 () {
      var mesh = Mesh3.LoadTMesh (NT.File ("Geom/Mesh3/part.tmesh"));
      var tree = OBBTree.From (mesh);
      tree.EnumBoxes (5).Count ().Is (32);
      int leftH = GetHeight (tree.OBBs[0].Left), rightH = GetHeight (tree.OBBs[0].Right);
      double area = 0;
      var sb = new StringBuilder ();
      Dump (0, 0, EDir.Root);
      sb.Insert (0, $"Nodes: {tree.OBBs.Length + tree.Tris.Length - 2}. LeftH: {leftH}, RightH: {rightH}, Area = {area.R6 ()}\n");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual (NT.File ("Sim/OBBTree.txt"), NT.TmpTxt);

      // Helpers ...........................................
      void Dump (int id, int level, EDir dir) {
         string indent = new ('\t', level);
         var node = tree.OBBs[id];
         if (node.Left > 0) Dump (node.Left, level + 1, EDir.Left);
         else sb.AppendLine ($"{indent}\tLT{-node.Left}");
         string pre = dir == 0 ? "" : dir < 0 ? "L" : "R";
         area += node.Area;
         sb.AppendLine ($"{indent}{pre}B{id} ");
         if (node.Right > 0) Dump (node.Right, level + 1, EDir.Right);
         else if (node.Right < 0) sb.AppendLine ($"{indent}\tRT{-node.Right}");
      }

      int GetHeight (int id) {
         var node = tree.OBBs[id];
         int leftH = node.Left > 0 ? GetHeight (node.Left) : 0;
         int rightH = node.Right > 0 ? GetHeight (node.Right) : 0;
         return 1 + Math.Max (leftH, rightH);
      }
   }

   [Test (189, "OBBCollider test")]
   void Test6 () {
      var cow = Mesh3.LoadObj (Lib.ReadLinesFromZip (NT.File ("IO/MESH/cow.zip"), "cow.obj"));
      cow *= Matrix3.Scaling (100);
      var cowOBB = OBBTree.From (cow);
      var hand = Mesh3.LoadObj (Lib.ReadLinesFromZip (NT.File ("IO/MESH/hand.zip"), "hand.obj"));
      hand *= Matrix3.Scaling (70);
      var handOBB = OBBTree.From (hand);

      int s = 50; string txt = "";
      var mR = new Rand (42);
      StringBuilder trace = new ();

      for (int i = 1; i <= 10; i++) {
         int mX = mR.Next (-s, s), mY = mR.Next (-s, s), mZ = mR.Next (-s, s);
         int mX2 = mR.Next (-s, s), mY2 = mR.Next (-s, s), mZ2 = mR.Next (-s, s);
         int mRx = mR.Next (-180, 180), mRy = mR.Next (-180, 180), mRz = mR.Next (-180, 180);
         int mRx2 = mR.Next (-180, 180), mRy2 = mR.Next (-180, 180), mRz2 = mR.Next (-180, 180);

         var xfm1 = Matrix3.Identity;
         xfm1 *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
         xfm1 *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
         xfm1 *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
         xfm1 *= Matrix3.Translation (mX, mY, mZ);
         var cowTest = cowOBB.With (xfm1);

         var xfm2 = Matrix3.Identity;
         xfm2 *= Matrix3.Rotation (EAxis.X, mRx2.D2R ());
         xfm2 *= Matrix3.Rotation (EAxis.Y, mRy2.D2R ());
         xfm2 *= Matrix3.Rotation (EAxis.Z, mRz2.D2R ());
         xfm2 *= Matrix3.Translation (mX2, mY2, mZ2);
         var handTest = handOBB.With (xfm2);

         using var bc = OBBCollider.Borrow ();
         var crash = bc.Check (cowTest, handTest);

         txt += $"{i}\n";
         txt += $"Cow : ({mX},{mY},{mZ}) <{mRx},{mRy},{mRz}>)\n";
         txt += $"Hand: ({mX2},{mY2},{mZ2}) <{mRx2},{mRy2},{mRz2}>)\n";
         txt += crash ? "CRASH" : ".....";
         txt += "\n\n";

         if (i == 9) {
            var pts = bc.GetChalk ().ToArray ();
            pts.Select (a => a.ToString () + "\r\n").ForEach (s => trace.Append (s));
         }
      }
      File.WriteAllText (NT.TmpTxt, txt);
      Assert.TextFilesEqual ("Sim/OBBCollide.txt", NT.TmpTxt);
      File.WriteAllText (NT.TmpTxt, trace.ToString ());
      Assert.TextFilesEqual ("Sim/OBBTrace.txt", NT.TmpTxt);
   }
}
