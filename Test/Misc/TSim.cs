// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TSim.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (32, "Robot basic tests", "Sim")]
class TSim {
   TSim () {
      mMech = Mechanism.Load ("N:/Wad/FanucX/mechanism.curl");
      mTip = mMech.FindChild ("Tip")!;
      mJoints = [.. "SLURBT".Select (a => mMech.FindChild (a.ToString ())!)];
      for (int i = 0; i < 6; i++) {
         var m = mJoints[i];
         double a = m.JMin, b = m.JMax, delta = i switch { 1 => 0, 4 => 0, _ => 0 };
         mMin[i] = a + delta; mMax[i] = b + delta;
      }
      mSolver = new (150, 770, 0, 0, 1016, 175, mMin, mMax);
   }
   RBRSolver mSolver;
   Mechanism mMech, mTip;
   Mechanism[] mJoints;
   double[] mMin = new double[6], mMax = new double[6];
   CoordSystem mHome = new (new (1166, 0, 1161 - 565), Vector3.XAxis, Vector3.YAxis);

   [Test (161, "Pose with 8 solutions")]
   void Test1 () 
      => Test (503.5971223021581, 0, 0, -90, 0, 63.45323741007194, "T8");

   [Test (162, "Pose with 2 solutions")]
   void Test2 () 
      => Test (-201.4388489208634, 402.8776978417266, 57.553956834532244, -113.30935251798554, 1.2949640287769801, 142.44604316546784, "T2");

   [Test (163, "Pose with 0 solutions")]
   void Test3 ()
      => Test (1000, 0, 0, 0, 0, 0, "T0");

   [Test (164, "Pose with 6 solutions")]
   void Test4 () 
      => Test (489.20863309352495, -575.5395683453237, -359.71223021582733, -53.74100719424463, 85.46762589928056, 142.44604316546784, "T6");

   [Test (165, "Pose with 4 solutions")]
   void Test5 () 
      => Test (489.20863309352495, -575.5395683453237, -172.66187050359716, -34.31654676258993, 85.46762589928056, 71.22302158273355, "T4");

   void Test (double x, double y, double z, double rx, double ry, double rz, string file) {
      var cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, rx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, ry.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, rz.D2R ());
      cs = cs * Matrix3.Translation ((Vector3)(mHome.Org + new Vector3 (x, y, z)));
      mSolver.ComputeStances (cs.Org, cs.VecZ, cs.VecX);
      var sb = new StringBuilder ();
      sb.Append ($"X={x.R6 ()} Y={y.R6 ()} Z={z.R6 ()}\n");
      sb.Append ($"Rx={rx.R6 ()} Ry={ry.R6 ()} Rz={rz.R6 ()}\n");
      for (int j = 0; j < 8; j++) {
         var a = mSolver.Solutions[j];
         if (!a.OK) continue;
         sb.Append ($"{j} ");
         for (int i = 0; i < 6; i++) {
            sb.Append ($" {a.GetJointAngle (i).Round (3)}");
            mJoints[i].JValue = a.GetJointAngle (i);
         }
         sb.AppendLine ();
         var csTip = mTip.Xfm.ToCS ();
         sb.Append ($"   {csTip.Org.R6 ()} {csTip.VecX.R6 ()} {csTip.VecY.R6 ()}\n");
      }
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual (NT.File ($"Sim/{file}.txt"), NT.TmpTxt);
   }
}
