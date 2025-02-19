// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ Geometry.cs
// ║║║║╬║╔╣║ Various geometry tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (1, "Geometry tests", "Geom")]
class GeometryTests {
   [Test (5, "class Bound1")]
   void Test5 () {
      Bound1 b1 = new (), b2 = new (10, 20), b3 = new (3, 1);
      b1.IsEmpty.Is (true); b1.ToString ().Is ("Empty");
      b2.Is ("10~20"); b3.Is ("1~3");
      b2.Length.Is (10); b2.Mid.Is (15);
      b2.Contains (11).Is (true); b2.Contains (21).Is (false);
      b2.InflatedF (1.2).Is ("9~21");
      b2.InflatedL (1.2).Is ("8.8~21.2");
      (b3 + 0 + 10).Is ("0~10");
      (b2 * b3).Is ("Empty");
      (b2 * new Bound1 (15, 25)).Is ("15~20");
      b1.InflatedF (1.2).Is ("Empty");
      b1.InflatedL (1.2).Is ("Empty");
      (new Bound1 (5)).Is ("5~5");
   }

   [Test (6, "class Bound2")]
   void Test6 () {
      Bound2 b1 = new (), b2 = new (10, 100, 20, 200), b3 = new (10, 10, 1, 1);
      b1.IsEmpty.Is (true); b1.ToString ().Is ("Empty");
      b2.Is ("(10~20,100~200)"); b3.Is ("(1~10,1~10)");
      b2.Width.Is (10); b2.Height.Is (100); b2.Midpoint.Is ("(15,150)");
      b2.Contains (new (11, 110)).Is (true);
      b2.Contains (new (11, 210)).Is (false); b2.Contains (new (21, 110)).Is (false);
      b2.InflatedF (1.2).Is ("(9~21,90~210)");
      b2.InflatedL (1.2).Is ("(8.8~21.2,98.8~201.2)");
      (b2 + new Point2 (5, 205) + new Point2 (21, 99)).Is ("(5~21,99~205)");
      (b2 * new Bound2 (15, 150, 25, 250)).Is ("(15~20,150~200)");
      (new Bound2 (5, 6)).Is ("(5~5,6~6)");
   }

   [Test (7, "class Bound3")]
   void Test7 () {
      Bound3 b1 = new (), b2 = new (10, 100, 1000, 20, 200, 2000), b3 = new (10, 10, 10, 1, 1, 1);
      b1.IsEmpty.Is (true); b1.ToString ().Is ("Empty");
      b2.Is ("(10~20,100~200,1000~2000)"); b3.Is ("(1~10,1~10,1~10)");
      b2.Width.Is (10); b2.Height.Is (100); b2.Depth.Is (1000); b2.Midpoint.Is ("(15,150,1500)");
      b2.Contains (new (11, 110, 1100)).Is (true);
      b2.Contains (new (11, 210, 1100)).Is (false);
      b2.Contains (new (21, 110, 1100)).Is (false);
      b2.Contains (new (11, 110, 2100)).Is (false);
      b2.InflatedF (1.2).Is ("(9~21,90~210,900~2100)");
      b2.InflatedL (5).Is ("(5~25,95~205,995~2005)");
      (b2 + new Point3 (5, 205, 3000) + new Point3 (21, 99, 875)).Is ("(5~21,99~205,875~3000)");
      (b2 * new Bound3 (15, 150, 1500, 25, 250, 2500)).Is ("(15~20,150~200,1500~2000)");
   }
}

[Fixture (2, "Matrix2 tests", "Geom")]
class Matrix2Tests {
   [Test (8, "Matrix2 basics")]
   void Test1 () {
      new Matrix2 (1, 2, 3, 4, 5, 6).Is ("[1,2 | 3,4 | 5,6]");

      var m1 = Matrix2.Identity; m1.Is ("[1,0 | 0,1 | 0,0]");
      Point2 p1 = new (10, 12); Vector2 v1 = new (3, 5);
      (p1 * m1).Is ("(10,12)"); (v1 * m1).Is ("<3,5>");

      var m2 = Matrix2.Translation (3, 4); m2.Is ("[1,0 | 0,1 | 3,4]");
      Matrix2.Translation (new (4, 5)).Is ("[1,0 | 0,1 | 4,5]");
      (p1 * m2).Is ("(13,16)"); (v1 * m2).Is ("<3,5>");

      var m3 = Matrix2.Scaling (2); m3.Is ("[2,0 | 0,2 | 0,0]");
      (p1 * m3).Is ("(20,24)"); (v1 * m3).Is ("<6,10>");
      m3 = Matrix2.Scaling (2, 3); m3.Is ("[2,0 | 0,3 | 0,0]");
      (p1 * m3).Is ("(20,36)"); (v1 * m3).Is ("<6,15>");
      m3 = Matrix2.Scaling (new (1, 2), 2, 3); m3.Is ("[2,0 | 0,3 | -1,-4]");
      (p1 * m3).Is ("(19,32)"); (v1 * m3).Is ("<6,15>");
      (new Point2 (1, 2) * m3).Is ("(1,2)");

      var m4 = Matrix2.Rotation (45.D2R ()); m4.Is ("[0.707107,0.707107 | -0.707107,0.707107 | 0,0]");
      (p1 * m4).Is ("(-1.414214,15.556349)"); (v1 * m4).Is ("<-1.414214,5.656854>");
      var m5 = Matrix2.Rotation (new (1, 2), 90.D2R ());
      (p1 * m5).Is ("(-9,11)"); (v1 * m5).Is ("<-5,3>");

      var m6 = (Matrix3)Matrix2.Translation (1, 2);
      m6.Is ("[1,0,0, 0,1,0, 0,0,1, 1,2,0]");
   }

   [Test (9, "Matrix2 multiplication, inverse")]
   void Test2 () {
      Matrix2 m1 = Matrix2.Translation (5, 3), m2 = Matrix2.Rotation (90.D2R ());
      Point2 p1 = new (10, 15); ((p1 * m1) * m2).Is ("(-18,15)");
      (p1 * (m1 * m2)).Is ("(-18,15)");

      Matrix2 m2i = Matrix2.Rotation (-90.D2R ()), m1i = Matrix2.Translation (-5, -3);
      Point2 p1t = new (-18, 15);
      ((p1t * m2i) * m1i).Is ("(10,15)"); (p1t * (m2i * m1i)).Is ("(10,15)");

      Matrix2 mul1 = m1 * m2; (p1 * mul1).Is ("(-18,15)");
      Matrix2 mul2 = m2i * m1i; (p1t * mul2).Is ("(10,15)");

      Matrix2 inv1 = mul1.GetInverse (); (p1t * inv1).Is ("(10,15)");
      Matrix2 inv2 = mul2.GetInverse (); (p1 * inv2).Is ("(-18,15)");
      (p1 * Matrix2.Scaling (2, 4).GetInverse ()).Is ("(5,3.75)");

      bool caught = false;
      Matrix2 bad = new (0, 0, 0, 0, 0, 0);
      try { bad.GetInverse (); } catch { caught = true; }
      caught.Is (true);

      var mt = Matrix2.Translation (10, 5) * Matrix2.Rotation (new (3, -4), 45.D2R ()) * Matrix2.Scaling (3, 2);
      Point2 pa = new (10, 3), pb = new (19.6066017177982, 33.0121933088198);
      (pa * mt).Is ("(19.606602,33.012193)");
      (pb * mt.GetInverse ()).Is ("(10,3)");
      Matrix2 ma = Matrix2.Scaling (1 / 3.0, 1 / 2.0), mb = Matrix2.Rotation (new (3, -4), -45.D2R ()), mc = Matrix2.Translation (-10, -5);
      Point2 ptmp = pb * ma; ptmp *= mb; ptmp *= mc;
      ptmp.Is ("(10,3)");
   }
}

[Fixture (12, "Matrix3 tests", "Geom")]
class Matrix3Tests {
   [Test (52, "Test of Matrix3 constructors")]
   void Test1 () {
      var p0 = P (3, -4, 5);
      var v0 = V (1, 2, 3);
      (p0 * Matrix3.Identity).Is ("(3,-4,5)");
      Matrix3.Identity.IsIdentity.IsTrue ();
      (p0 * Matrix3.Translation (1, 2, 3)).Is ("(4,-2,8)");
      (p0 * Matrix3.Translation (v0)).Is ("(4,-2,8)");
      (v0 * Matrix3.Identity).Is ("<1,2,3>");
      (v0 * Matrix3.Translation (5, 6, 7)).Is ("<1,2,3>");
      (p0 * Matrix3.Rotation (EAxis.X, Lib.HalfPI)).Is ("(3,-5,-4)");
      Matrix3.Rotation (V (1, 1, 0), Lib.QuarterPI).IsRotation.IsTrue ();
      (p0 * Matrix3.Rotation (EAxis.Y, Lib.HalfPI)).Is ("(5,-4,-3)");
      (p0 * Matrix3.Rotation (EAxis.Z, Lib.HalfPI)).Is ("(4,3,5)");
      (P (10, 1, 2) * Matrix3.Rotation (V (1, 1, 0), Lib.PI)).Is ("(1,10,-2)");
      (P (10, 0, 0) * Matrix3.Rotation (V (1, 1, 0), Lib.HalfPI)).Is ("(5,5,-7.071068)");
      Matrix3.Rotation (EAxis.X, 0).IsIdentity.IsTrue ();
      Matrix3.Rotation (V (1, 1, 0), 0).IsIdentity.IsTrue ();
      (p0 * Matrix3.Scaling (1.5)).Is ("(4.5,-6,7.5)");
      (p0 * Matrix3.Scaling (1, 1.5, 2)).Is ("(3,-6,10)");
      var m2 = Matrix3.Orthographic (new Bound3 (0, 0, 1, 10, 5, 5));
      m2.Is ("[0.2,0,0, 0,0.4,0, 0,0,-0.5, -1,-1,-1.5]");
      m2.HasMirroring.IsTrue ();

      var p1 = P (1, 2, 3);
      var q1 = Quaternion.FromAxisRotations (0, 0, 0);
      var mr1 = Matrix3.Rotation (q1);
      (p1 * mr1).Is ("(1,2,3)");
      var q2 = Quaternion.FromAxisRotations (Lib.HalfPI, Lib.HalfPI, Lib.PI);
      var mr2 = Matrix3.Rotation (q2);
      (p1 * mr2).Is ("(3,-1,-2)");

      var m5 = Matrix3.Map (new Bound2 (5, 5, 85, 45), new (2000, 1000));
      m5.Is ("[0.025,0,0, 0,0.05,0, 0,0,1, -1.125,-1.25,0]");
   }

   [Test (53, "Test of Matrix3 multiplications")]
   void Test2 () {
      var p0 = P (1, 2, 3);
      Matrix3 mi = Matrix3.Identity;
      Matrix3 mt1 = Matrix3.Translation (1, 2, 3), mt2 = Matrix3.Translation (4, 5, 6);
      (mi * mt1).ToString ().Is (mt1.ToString ());
      (mt2 * mi).ToString ().Is (mt2.ToString ());
      (Point3.Zero * (mt1 * mt2)).Is ("(5,7,9)");
      ((Point3.Zero * mt1) * mt2).Is ("(5,7,9)");
      Matrix3 mr1 = Matrix3.Rotation (EAxis.X, Lib.HalfPI), mr2 = Matrix3.Rotation (EAxis.Y, Lib.HalfPI);
      (p0 * (mt1 * mr1)).Is ("(2,-6,4)");
      (p0 * (mr1 * mt1)).Is ("(2,-1,5)");
      (p0 * (mr1 * mr2)).Is ("(2,-3,-1)");
      Matrix3 ms = Matrix3.Scaling (3);
      (p0 * (mt1 * ms)).Is ("(6,12,18)");
      (p0 * (ms * mt1)).Is ("(4,8,12)");
      Matrix3 mo1 = Matrix3.Orthographic (new Bound3 (1, 3, 5, 2, 4, 6));
      Matrix3 mo2 = Matrix3.Orthographic (new Bound3 (3, 5, 7, 4, 6, 8));
      mo1.HasMirroring.IsTrue ();
      (mo1 * mo2).HasMirroring.IsFalse ();
   }

   [Test (54, "Test of Matrix3 inversion")]
   void Test3 () {
      var p0 = P (1, 2, 3);
      Matrix3 mi = Matrix3.Identity;
      Matrix3 mt = Matrix3.Translation (1, 2, 3);
      Matrix3 mr = Matrix3.Rotation (EAxis.X, Lib.HalfPI);
      Matrix3 ms = Matrix3.Scaling (2);
      mi.GetInverse ().ToString ().Is (mi.ToString ());
      (p0 * (mt.GetInverse ())).Is ("(0,0,0)");
      (p0 * (mr.GetInverse ())).Is ("(1,3,-2)");
      (p0 * (ms.GetInverse ())).Is ("(0.5,1,1.5)");
      Matrix3 m1 = mt * mr * ms;
      Point3 p1 = p0 * m1; p1.Is ("(4,-12,8)");
      Matrix3 m2 = m1.GetInverse ();
      (p1 * m2).Is ("(1,2,3)");
      Matrix3 m3 = mt * mr;
      Point3 p2 = p0 * m3; p2.Is ("(2,-6,4)");
      Matrix3 m4 = m3.GetInverse ();
      (p2 * m4).Is ("(1,2,3)");
   }

   static Point3 P (double x, double y, double z) => new (x, y, z);
   static Vector3 V (double x, double y, double z) => new (x, y, z);
}

[Fixture (9, "GPUTypes tests", "Geom")]
class GPUTypesTests {
   [Test (1, "Test of VecNf (floating point Vector types")]
   void Test1 () {
      Vec2F a = new (3.654321f, 2.2f); a.Is ("<3.65432,2.2>");
      Vec2F b = (Vec2F)new Vector2 (1, 2); b.Is ("<1,2>");
      Vec2F a1 = new (3.654321, 2.2); a1.Is ("<3.65432,2.2>");
      Vector2 c = b; c.Is ("<1,2>");
      Vec2F.Zero.Is ("<0,0>");
      a.EQ (new (3.65432f, 2.2f)).IsTrue ();
      a.EQ (new (3.6543f, 2.2f)).IsFalse ();

      Vec3F d = new (3.654321f, 2.2f, 1.1f); d.Is ("<3.65432,2.2,1.1>");
      Vec3F d1 = new (3.654321, 2.2, 1.1); d1.Is ("<3.65432,2.2,1.1>");
      Vec3F e = (Vec3F)new Vector3 (1, 2, 3); e.Is ("<1,2,3>");
      Vector3 f = e; f.Is ("<1,2,3>");
      Vec3F.Zero.Is ("<0,0,0>");
      d.EQ (new (3.65432f, 2.2f, 1.1f)).IsTrue ();
      d.EQ (new (3.6543f, 2.2f, 1.1f)).IsFalse ();

      Vec3H g = new ((Half)1.1, (Half)2.2, (Half)3.3); g.Is ("<1.1,2.199,3.301>");
      Vec3H g2 = new ((Half)1.1, (Half)2.2, (Half)3.4);
      g.EQ (g).IsTrue ();  g.EQ (g2).IsFalse ();
      Vec4H g3 = new (1, 2, 3, 4); g3.Is ("<1,2,3,4>");
      g3.EQ (g3).IsTrue (); g3.EQ (new (1, 2, 3, 4.00001f)).IsFalse ();

      Vec4F h = new (1.1, 2.2, 3.3, 4.4); h.EQ (h).IsTrue ();
      Vec4F h2 = new (1.1, 2.2, 3.3, 4.5); h.EQ (h2).IsFalse ();
      h.CompareTo (h2).Is (-1);
      h.CompareTo (new (1.2, 2.2, 3.3, 4.4)).Is (-1);
      h.CompareTo (new (1.1, 2.3, 3.3, 4.4)).Is (-1);
      h.CompareTo (new (1.1, 2.2, 3.2, 4.4)).Is (1);
      h.CompareTo (new (1.1, 2.2, 3.3, 4.3)).Is (1);

      Mat4F m0 = new (11, 12, 13, 21, 22, 23, 31, 32, 33, 1, 2, 3);
      m0.Is ("[11,12,13,0, 21,22,23,0, 31,32,33,0, 1,2,3,1]");
      Mat4F m1 = (Mat4F)Matrix3.Translation (1, 2, 3);
      m1.Is ("[1,0,0,0, 0,1,0,0, 0,0,1,0, 1,2,3,1]");
      Mat4F.Identity.Is ("[1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]");
      Mat4F.Zero.Is ("[0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,1]");
      m0.EQ (ref m0).IsTrue (); m0.EQ (ref m1).IsFalse ();

      Matrix3 m2 = new (11, 12, 13, 21, 22, 23, 31, 32, 33, 1, 2, 3);
      m2.ExtractRotation ().Is ("[11,12,13, 21,22,23, 31,32,33, 0,0,0]");
   }
}
