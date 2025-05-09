// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TStruct.cs
// ║║║║╬║╔╣║ Tests for various structures
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (13, "Tests for various structs", "Core")]
class StructTests {
   [Test (39, "Test of Color4")]
   void Test1 () {
      new Color4 (48, 12, 24, 36).Is ("#300C1824");
      new Color4 (12, 24, 36).Is ("#0C1824");
      Color4.Nil.IsNil.Is (true); Color4.Red.IsNil.Is (false);
      Color4.Red.Is ("Red"); Color4.Nil.Is ("Nil");
      Color4.Transparent.Is ("Transparent"); Color4.Black.Is ("Black");
      Color4.Green.Is ("Green"); Color4.Blue.Is ("Blue");
      Color4.Yellow.Is ("Yellow"); Color4.Magenta.Is ("Magenta");
      Color4.Cyan.Is ("Cyan"); Color4.White.Is ("White");
      Color4.Red.EQ (Color4.Red).Is (true);
      Color4.Red.Value.Is (0xFFFF0000);
      Color4.Gray (128).Is ("#808080");
      Color4.Gray (0x33).Is ("#333");
      Vec4F v1 = (Vec4F)Color4.Magenta; v1.Is ("<1,0,1,1>");
      new Color4 (0x11, 0x22, 0x33).Is ("#123");
      Color4.Random.A.Is ((byte)255);
      Color4.Cyan.Deconstruct (out int r, out int g, out int b, out int a);
      r.Is (0); g.Is (255); b.Is (255); a.Is (255);
      new Color4 (0x12345678).Is ("#12345678");
      Color4.Parse ("Red").Is ("Red");
      Color4.Parse ("#123456").Is ("#123456");
      Color4.Parse ("#12345678").Is ("#12345678");
      Color4.Parse ("#12345678").A.Is ((byte)18);
      var c1 = Color4.Parse ("#123"); c1.Is ("#123");
      c1.R.Is ("17"); c1.G.Is ("34"); c1.B.Is ("51"); c1.A.Is ("255");
      var c2 = Color4.RandomLight; (c2.R >= 128).IsTrue (); c2.A.Is ((byte)255);
      var c3 = Color4.RandomDark; (c3.R <= 128).IsTrue (); c3.A.Is ((byte)255);
      Color4.Transparent.IsTransparent.IsTrue ();
      Color4.Red.IsTransparent.IsFalse ();
      try {
         var c4 = Color4.Parse ("#12");
      } catch (Exception e) {
         e.Message.Is ("Cannot convert '#12' to Color4");
      }
   }

   [Test (40, "PlaneDef tests")]
   void Test2 () {
      PlaneDef.XY.Normal.Is ("<0,0,1>");
      PlaneDef.YZ.Normal.Is ("<1,0,0>");
      PlaneDef.XZ.Normal.Is ("<0,1,0>");
      var pd = new PlaneDef (new (0, 0, 0), new (0, 0, 1), new (1, 1, 0));
      pd.Normal.Is ("<-0.707107,0.707107,0>");
      pd.Dist (new Point3 (10, 0, 0)).Is (7.0710678);
      pd.SignedDist (new Point3 (10, 0, 0)).Is (-7.0710678);
      pd.SignedDist (new Point3 (0, 10, 0)).Is (7.0710678);
      pd.Intersect (pd, out _, out _).IsFalse ();
      pd.Intersect (PlaneDef.XY, out var _p0, out var _v1).IsTrue ();
      _p0.Is ("(0,0,0)"); _v1.EQ (new (0.707107, 0.707107, 0)).IsTrue ();
      pd.Intersect (new (10, 0, 0), new (10, 0, 0)).IsNil.IsTrue ();
      pd.Intersect (new (10, 0, 5), new (0, 10, 5)).Is ("(5,5,5)");
      pd.Snap (new (10, 10, 7)).Is ("(10,10,7)");
      pd.Snap (new (0, 10, 8)).Is ("(5,5,8)");
      try {
         var pd1 = new PlaneDef (Point3.Zero, new (1, 1, 1), new (2, 2, 2));
         throw new Exception ("Should not reach this line");
      } catch (InvalidOperationException e) {
         e.Message.Is ("Cannot create a PlaneDef with 3 collinear points");
      }
      pd.Is ("PlaneDef:-0.707107,0.707107,0,0");
   }

   [Test (41, "Quaternion tests")]
   void Test3 () {
      Quaternion.Identity.Is ("1,0,0:0");
      Quaternion.Identity.IsIdentity.IsTrue ();
      Quaternion q1 = Quaternion.FromAxisAngle (new (1, 1, 1), Lib.QuarterPI);
      q1.Angle.Is (Lib.QuarterPI);
      q1.Axis.Is ("<0.57735,0.57735,0.57735>");
      var q2 = q1 * q1;
      q2.Axis.EQ (q1.Axis).IsTrue ();
      q2.Angle.Is (Lib.HalfPI);
      Quaternion q3 = Quaternion.FromAxisRotations (Lib.HalfPI, Lib.HalfPI, Lib.PI);
      q3.Is ("0.57735,-0.57735,0.57735:240");
      Quaternion.FromAxisAngle (Vector3.YAxis, 0).IsIdentity.IsTrue ();
      q1.EQ (q1).IsTrue (); q1.EQ (q3).IsFalse ();
      try {
         var q4 = Quaternion.FromAxisAngle (Vector3.Zero, Lib.QuarterPI);
         throw new Exception ("Should not reach this line");
      } catch (ArgumentException e) {
         e.Message.Is ("Value cannot be zero (Parameter 'axis')");
      }
      var v5 = new Vector3 (1, 1, -1).Normalized ();
      var q5 = Quaternion.Parse ($"{v5.X},{v5.Y},{v5.Z}:45");
      q5.Is ("0.57735,0.57735,-0.57735:45");
   }

   [Test (42, "CoordSystem tests")]
   void Test4 () {
      CoordSystem.World.IsWorld.IsTrue ();
      new CoordSystem (Point3.Nil).IsNil.IsTrue ();
      new CoordSystem (new (5, 5, 5)).Is ("CoordSystem:(5,5,5),<1,0,0>,<0,1,0>");
      var c1 = new CoordSystem (new (5, 5, 5), Vector3.ZAxis, Vector3.XAxis);
      c1.Is ("CoordSystem:(5,5,5),<0,0,1>,<1,0,0>");
      c1.VecZ.Is ("<0,1,0>");
      c1.PlaneDef.Is ("PlaneDef:0,1,0,-5");
      (c1 + new Vector3 (1, 2, 3)).Is ("CoordSystem:(6,7,8),<0,0,1>,<1,0,0>");
      try {
         var c7 = new CoordSystem (Point3.Zero, new (1, 2, 3), new (4, 5, 6));
         throw new Exception ("Should not reach this line");
      } catch (InvalidOperationException e) {
         e.Message.Is ("CoordSystem basis vectors are not orthogonal");
      }
   }

   [Test (43, "Tests of BlockTimer")]
   void Test5 () {
      new BlockTimer ("Test of BlockTimer").Dispose ();
   }

   [Test (44, "Tests of Bound1")]
   void Test6 () {
      Bound1 b = new (10, 20);
      b.Clamp (5f).Is (10);
      b.EQ (b).IsTrue (); b.EQ (new (10, 20.001)).IsFalse ();
      double f1 = 1.1, f2 = 2.2;
      Bound1 b2 = (Bound1)((f1, f2)); b2.Is ("1.1~2.2");
      Bound1 b3 = new (15, 25), b4 = new (5, 15);
      (b + b3).Is ("10~25"); (b * b4).Is ("10~15");
      Bound1 b5 = new (100, 200);
      (b * b5).Is ("Empty");
   }

   [Test (45, "Tests of Bound2")]
   void Test7 () {
      Point2[] pts = [new (1, 20), new (10, 2)];
      Bound2 b1 = new (pts); b1.Is ("(1~10,2~20)");
      Bound2 b2 = new (5, 50); new Bound2 ([b1, b2]).Is ("(1~10,2~50)");
      b1.Diagonal.Is (Math.Sqrt (9 * 9 + 18 * 18));
      b1.InflatedF (2, new (5, 5)).Is ("(-3~15,-1~35)");
      (new Bound2 ()).InflatedF (2, Point2.Zero).Is ("Empty");
      Bound2 b3 = new (5, 15, 25, 30);
      (b1 * b3).Is ("(5~10,15~20)");
      (b1 * Matrix2.Translation (100, 100)).Is ("(101~110,102~120)");
   }

   [Test (46, "Tests of Bound3")]
   void Test8 () {
      Vec3F[] pts = [new (1, 20, 4), new (10, 2, 5)];
      Bound3 b1 = new (pts); b1.Is ("(1~10,2~20,4~5)");
      b1.Diagonal.Is (20.149442);
      Bound3 b2 = new (5, 10, 4.5, 20, 30, 10);
      (b1 + b2).Is ("(1~20,2~30,4~10)");
      (b1 * b2).Is ("(5~10,10~20,4.5~5)");
   }

   /// <summary>Misc. struct tests.</summary>
   [Test (50, "Misc. struct tests.")]
   void Test9 () {
      Point3 a = new (10, 20, 30); 
      var (x, y, z) = a;
      a.EQ ((10, 20, 30)).IsTrue ();
      a.EQ ((x, y, z)).IsTrue ();

      Point2 b = new (40, 50);
      (x, y) = b;
      b.EQ ((40, 50)).IsTrue ();
      b.EQ ((x, y)).IsTrue ();

      Vector3 va = new (10, 20, 30);
      (x, y, z) = va;
      va.EQ (new (x, y, z)).IsTrue ();

      Vector2 vb = new (40, 50);
      (x, y) = vb;
      vb.EQ (new (x, y)).IsTrue ();
   }
}