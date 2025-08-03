namespace Nori;

#region class RBRSolver ----------------------------------------------------------------------------
/// <summary>
/// Implements an inverse-kinemeatics solver for an R-B-R type robot (roll-bend-roll)
/// </summary>
public class RBRSolver {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an RBRSolver, given the parameters defining the robot</summary>
   public RBRSolver (double a12, double a23, double a34, double s2, double s4, double s6, double[] min, double[] max) {
      mA12 = a12; mA23 = a23; mA34 = a34; mS2 = s2; mS4 = s4; mS6 = s6;
      for (int i = 0; i < 6; i++) { mMin[i + 1] = min[i].D2R (); mMax[i + 1] = max[i].D2R (); }
      for (int i = 0; i < 8; i++) mSolutions[i] = new Soln (mMin, mMax);
   }

   public IReadOnlyList<Soln> Solutions => mSolutions;
   Soln[] mSolutions = new Soln[8];

   // Methods ------------------------------------------------------------------
   /// <summary>For a given end-effector orientation, this return all possible valid stances of the robot</summary>
   /// <param name="Fptool">End effector position</param>
   /// <param name="vecZ">Work vector</param>
   /// <param name="vecX">X-vector (forward facing vector)</param>
   /// <returns>A list of coordinate sets, in canonical coordinates</returns>
   /// It is possible the list returned may have zero elements, in the case where the end-effector
   /// coordinates supplied are not reachable by the robot
   public void ComputeStances (Point3 Fptool, Vector3 vecZ, Vector3 vecX) {
      var set = mSolutions;
      set.ForEach (a => a.OK = false);
      Vector3 vecY = vecX * vecZ;
      vecX = (vecZ * vecY).Normalized ();

      // ------------------------------------------------
      // Start the IK analysis
      Vector3 FS6 = -vecZ, Fa67 = vecX;
      Vector3 FS1 = Vector3.ZAxis;                                // (5.5)
      Vector3 FS7 = (Fa67 * FS6).Normalized ();
      Vector3 Fa71 = FS7 * FS1;                                   // (5.10)
      if (Fa71.LengthSq.EQ (0, Lib.Epsilon * Lib.Epsilon))
         return;

      // ------------------------------------------------
      // First, perform the loop closure
      Fa71 = Fa71.Normalized ();                                  // (5.11)
      double c71 = FS7.Dot (FS1);
      double s71 = (FS7 * FS1).Dot (Fa71);                        // (5.12)
      double alpha71 = Math.Atan2 (s71, c71);

      double c7 = Fa67.Dot (Fa71);                                // (5.13)
      double s7 = (Fa67 * Fa71).Dot (FS7);                        // (5.14)
      double theta7 = Math.Atan2 (s7, c7);

      double cgamma = Fa71.X;                                     // (5.15)
      double sgamma = (Fa71 * Vector3.XAxis).Dot (FS1);           // (5.16)
      double gamma1 = Math.Atan2 (sgamma, cgamma);

      Point3 FP6orig = Fptool;                                    // (5.3)
      Vector3 vFP6orig = (Vector3)FP6orig;
      double S7 = (FS1 * vFP6orig).Dot (Fa71) / s71;              // (5.21)
      double a71 = (vFP6orig * FS1).Dot (FS7) / s71;              // (5.22)
      double S1 = (vFP6orig * FS7).Dot (Fa71) / s71;              // (5.23)

      // ------------------------------------------------
      // Having performed the loop closure, we can now proceed to the inverse analysis.
      // 1. Solve for 2 values of theta1
      (s71, c71) = Math.SinCos (alpha71);       // <-- removethis?
      var (s67, c67) = Math.SinCos (alpha67);
      var (s12, c12) = Math.SinCos (alpha12);
      (s7, c7) = Math.SinCos (theta7);          // <-- removethis?
      double X7 = s67 * s7;
      double Y7 = -(s71 * c67 + c71 * s67 * c7);
      double Z7 = c71 * c67 - s71 * s67 * c7;
      double A = mS6 * Y7 - S7 * s71, B = mS6 * X7 + a71, D = mS2;
      if (SolveAngleEqn (A, B, D, out double theta1a, out double theta1b)) {
         set[0].SetTheta (1, theta1a); set[4].SetTheta (1, theta1b);
      } else
         set[0].OK = set[4].OK = false;
      for (int i = 1; i <= 3; i++) { set[i].CopyTheta (1, set[0]); set[i + 4].CopyTheta (1, set[4]); }

      // ------------------------------------------------
      // 2. For each value of theta1, we can compute two values of theta3
      for (int i = 0; i < 8; i += 4) {
         var a = set[i]; if (!a.OK) continue;
         double c1 = a.COS[1], s1 = a.SIN[1];
         double X1 = s71 * s1, Y1 = -(s12 * c71 + c12 * s71 * c1);
         double X71 = X7 * c1 - Y7 * s1, Y71 = c12 * (X7 * s1 + Y7 * c1) - s12 * Z7;
         double RHS1 = -mS6 * X71 - S7 * X1 - a71 * c1 - mA12;
         double RHS2 = -S1 - mS6 * Y71 - S7 * Y1;
         A = 2 * mA23 * mA34; B = -2 * mA23 * mS4;
         D = (mA23 * mA23) + (mA34 * mA34) + (mS4 * mS4) - (RHS1 * RHS1) - (RHS2 * RHS2);  // (11.25)
         if (SolveAngleEqn (A, B, D, out double theta3a, out double theta3b)) {
            a.SetTheta (3, theta3a); set[i + 1].CopyTheta (3, a);
         } else
            a.OK = set[i + 1].OK = false;
         set[i + 2].SetTheta (3, theta3b); set[i + 3].CopyTheta (3, set[i + 2]);
      }

      // ------------------------------------------------
      // 3. Compute theta2 now
      for (int i = 0; i < 8; i += 2) {
         var a = set[i]; if (!a.OK) continue;
         double c1 = a.COS[1], s1 = a.SIN[1], c3 = a.COS[3], s3 = a.SIN[3];
         double X1 = s71 * s1, Y1 = -(s12 * c71 + c12 * s71 * c1);
         double X71 = X7 * c1 - Y7 * s1, Y71 = c12 * (X7 * s1 + Y7 * c1) - s12 * Z7;
         double RHS1 = -mS6 * X71 - S7 * X1 - a71 * c1 - mA12;
         double RHS2 = -S1 - mS6 * Y71 - S7 * Y1;
         double Aa = mA23 + mA34 * c3 - mS4 * s3, Ba = -mA34 * s3 - mS4 * c3, C = -RHS1;  // (11.26)
         double Da = Ba, E = -Aa, F = -RHS2;
         if (!Lib.SolveLinearPair (Aa, Ba, C, Da, E, F, out double c2, out double s2)) {
            a.OK = set[i + 1].OK = false;
            continue;
         }
         a.SetTheta (2, Math.Atan2 (s2, c2)); set[i + 1].CopyTheta (2, a);
      }

      // ------------------------------------------------
      // 4. Now, to compute theta5. This is computable from the 3 angles we have already computed
      for (int i = 0; i < 8; i += 2) {
         var a = set[i]; if (!a.OK) continue;
         double c1 = a.COS[1], s1 = a.SIN[1];
         double c5 = -a.KAPPA * (X7 * c1 - Y7 * s1) - a.LAMBDA * Z7;
         if (c5 is < -1 - Lib.Epsilon or > 1 + Lib.Epsilon) {
            a.OK = set[i + 1].OK = false;
            continue;
         }
         a.SetTheta (5, ACos (c5));
         set[i + 1].SetTheta (5, -a.TH[5]);
      }

      // ------------------------------------------------
      // 5. Compute theta 4, based on the 4 angles we already computed
      for (int i = 0; i < 8; i++) {
         var a = set[i]; if (!a.OK) continue;
         double c1 = a.COS[1], s1 = a.SIN[1], s5 = a.SIN[5];
         double c4 = (-a.LAMBDA * (X7 * c1 - Y7 * s1) + a.KAPPA * Z7) / s5;
         double s4 = -(X7 * s1 + Y7 * c1) / s5;
         a.SetTheta (4, Math.Atan2 (s4, c4));
      }

      // ------------------------------------------------
      // 6. Compute theta 6, based on the other 5 angles.
      for (int i = 0; i < 8; i++) {
         var a = set[i]; if (!a.OK) continue;
         double c2 = a.COS[2], s2 = a.SIN[2], c3 = a.COS[3], s3 = a.SIN[3];
         double c4 = a.COS[4], s4 = a.SIN[4], c1 = a.COS[1], s1 = a.SIN[1];
         double Ad = c2 * c3 * s4 - s2 * s3 * s4;
         double Bd = s2 * c3 * s4 + c2 * s3 * s4;
         double s6 = -c7 * (c1 * Ad - s1 * c4) + s7 * (c71 * (Ad * s1 + c1 * c4) + s71 * Bd);
         double c6 = s71 * (Ad * s1 + c1 * c4) - c71 * Bd;
         a.SetTheta (6, Math.Atan2 (s6, c6));
         a.TH[1] = Lib.NormalizeAngle (a.TH[1] - gamma1);
      }
   }

   static double ACos (double f) {
      if (f <= -1) return Lib.PI;
      if (f >= 1) return 0;
      return Math.Acos (f);
   }

   // Solves an angle equation of the form Ac + Bs + D = 0, where c = cos(t) and s = sin(t)
   // See Equations starting (6.194)
   static bool SolveAngleEqn (double A, double B, double D, out double fTheta1a, out double fTheta1b) {
      double hypot = Math.Sqrt (A * A + B * B);
      double gamma = Math.Atan2 (B / hypot, A / hypot);  // (6.196)
      double rhs = -D / hypot;
      if (rhs is < -1 - Lib.Epsilon or > 1 + Lib.Epsilon) {
         fTheta1a = fTheta1b = -999;
         return false;
      }
      double theta_gamma = ACos (rhs);
      fTheta1a = Lib.NormalizeAngle (theta_gamma + gamma);
      fTheta1b = Lib.NormalizeAngle (-theta_gamma + gamma);
      return true;
   }

   // Private data -------------------------------------------------------------
   // These are values used in the kinematic analysis.
   readonly double mA12, mA23, mA34, mS2, mS4, mS6;

   // Constants that are fixed for this type of RBR robot
   const double alpha67 = Lib.HalfPI;
   const double alpha12 = 3 * Lib.HalfPI; // 270 degrees)

   // The limits of the 6 axes (mMin[0] and mMax[0] are not used)
   double[] mMin = new double[7], mMax = new double[7];

   public class Soln {
      public Soln (double[] min, double[] max) { mMin = min; mMax = max; }
      double[] mMin, mMax;

      public bool OK { get => mOK; internal set => mOK = value; }
      bool mOK = true;

      public double GetJointAngle (int n) => TH[n + 1].R2D ();

      // The 6 axis angles for this solution (we don't use TH[0] for consistency with the
      // equations in the text)
      internal readonly double[] TH = new double[7];
      // Sin[A] and Cos[A] are the sin/cos of TH[A] (only indices 1..6 are used)
      internal readonly double[] SIN = new double[7], COS = new double[7];
      // Intermediate values KAPPA and LAMBDA used in the solution
      internal double KAPPA, LAMBDA;

      internal void SetTheta (int n, double f) {
         TH[n] = f; (SIN[n], COS[n]) = Math.SinCos (f);
         switch (n) {
            case 2:
               double c2 = COS[2], s2 = SIN[2], c3 = COS[3], s3 = SIN[3];
               KAPPA = s3 * c2 + c3 * s2; LAMBDA = c3 * c2 - s3 * s2;
               break;
         }
         if (n == 1) OK = true;
         else OK &= f >= mMin[n] && f <= mMax[n];
      }

      internal void CopyTheta (int n, Soln b) {
         OK = b.OK; if (!OK) return;
         TH[n] = b.TH[n]; SIN[n] = b.SIN[n]; COS[n] = b.COS[n];
         if (n == 2) { KAPPA = b.KAPPA; LAMBDA = b.LAMBDA; }
      }
   }
}
#endregion
