namespace Nori;

#region class RBRSolver ----------------------------------------------------------------------------
/// <summary>
/// Implements an inverse-kinemeatics solver for an R-B-R type robot (roll-bend-roll)
/// </summary>
public class RBRSolver {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an RBRSolver, given the parameters defining the robot</summary>
   public RBRSolver (double a12, double a23, double a34, double s2, double s4, double s6) {
      A12 = a12; A23 = a23; A34 = a34; S2 = s2; S4 = s4; S6 = s6;
      for (int i = 0; i < 7; i++) { mLimits[i, 0] = double.MinValue; mLimits[i, 1] = double.MaxValue; }
   }

   // Methods ------------------------------------------------------------------
   /// <summary>For a given end-effector orientation, this return all possible valid stances of the robot</summary>
   /// <param name="Fptool">End effector position</param>
   /// <param name="vecZ">Work vector</param>
   /// <param name="vecX">X-vector (forward facing vector)</param>
   /// <returns>A list of coordinate sets, in canonical coordinates</returns>
   /// It is possible the list returned may have zero elements, in the case where the end-effector
   /// coordinates supplied are not reachable by the robot
   public List<double[]> ComputeStances (Point3 Fptool, Vector3 vecZ, Vector3 vecX) {
      mStances.Clear ();
      Vector3 vecY = vecX * vecZ;
      vecX = (vecZ * vecY).Normalized ();

      // ------------------------------------------------
      // Start the IK analysis
      Vector3 FS6 = -vecZ, Fa67 = vecX;
      Vector3 FS1 = Vector3.ZAxis;                                // (5.5)
      Vector3 FS7 = (Fa67 * FS6).Normalized ();
      Vector3 Fa71 = FS7 * FS1;                                   // (5.10)
      if (Fa71.LengthSq.EQ (0, Lib.Epsilon * Lib.Epsilon)) {
         for (int i = 0; i < 8; i++) OK[i] = false;
         return mStances;
      }

      // ------------------------------------------------
      // First, perform the loop closure
      Fa71 = Fa71.Normalized ();                                 // (5.11)
      double c71 = FS7.Dot (FS1);
      double s71 = (FS7 * FS1).Dot (Fa71);                       // (5.12)
      double alpha71 = Math.Atan2 (s71, c71);

      double c7 = Fa67.Dot (Fa71);                               // (5.13)
      double s7 = (Fa67 * Fa71).Dot (FS7);                       // (5.14)
      double theta7 = Math.Atan2 (s7, c7);

      Vector3 XAxis = Vector3.XAxis;
      double cgamma = Fa71.X;                                    // (5.15)
      double sgamma = (Fa71 * XAxis).Dot (FS1);                  // (5.16)
      double gamma1 = Math.Atan2 (sgamma, cgamma);

      Point3 FP6orig = Fptool;                                   // (5.3)
      Vector3 vFP6orig = (Vector3)FP6orig;
      double S7 = (FS1 * vFP6orig).Dot (Fa71) / s71;             // (5.21)
      double a71 = (vFP6orig * FS1).Dot (FS7) / s71;             // (5.22)
      double S1 = (vFP6orig * FS7).Dot (Fa71) / s71;             // (5.23)

      // ------------------------------------------------
      // Having performed the loop closure, we can now proceed to the inverse analysis.
      // 1. Solve for 2 values of theta1
      s71 = Math.Sin (alpha71); c71 = Math.Cos (alpha71); double s67 = Math.Sin (alpha67), c67 = Math.Cos (alpha67);
      double c12 = Math.Cos (alpha12), s12 = Math.Sin (alpha12);
      c7 = Math.Cos (theta7); s7 = Math.Sin (theta7);
      double X7 = s67 * s7;
      double Y7 = -(s71 * c67 + c71 * s67 * c7);
      double Z7 = c71 * c67 - s71 * s67 * c7;
      double A = S6 * Y7 - S7 * s71, B = S6 * X7 + a71, D = S2;

      SolveAngleEqn (A, B, D, out double theta1a, out double theta1b);
      for (int i = 0; i < 4; i++) { TH[1, i] = theta1a; OK[i] = AngleOK (1, theta1a); }
      for (int i = 4; i < 8; i++) { TH[1, i] = theta1b; OK[i] = AngleOK (1, theta1b); }
      ComputeSinCos (1);

      // ------------------------------------------------
      // 2. For each value of theta1, we can compute two values of theta3
      for (int i = 0; i < 8; i += 4) {
         if (!OK[i]) continue;
         double c1 = COS[1, i], s1 = SIN[1, i];
         double X1 = s71 * s1, Y1 = -(s12 * c71 + c12 * s71 * c1);
         double X71 = X7 * c1 - Y7 * s1, Y71 = c12 * (X7 * s1 + Y7 * c1) - s12 * Z7;
         double RHS1 = -S6 * X71 - S7 * X1 - a71 * c1 - A12;
         double RHS2 = -S1 - S6 * Y71 - S7 * Y1;
         A = 2 * A23 * A34; B = -2 * A23 * S4;
         D = (A23 * A23) + (A34 * A34) + (S4 * S4) - (RHS1 * RHS1) - (RHS2 * RHS2);  // (11.25)
         SolveAngleEqn (A, B, D, out double theta3a, out double theta3b);
         TH[3, i] = TH[3, i + 1] = theta3a; OK[i] = OK[i + 1] = AngleOK (3, theta3a);
         TH[3, i + 2] = TH[3, i + 3] = theta3b; OK[i + 2] = OK[i + 3] = AngleOK (3, theta3b);
      }
      ComputeSinCos (3);

      // ------------------------------------------------
      // 3. Compute theta2 now
      for (int i = 0; i < 8; i += 2) {
         if (!OK[i]) continue;
         double c1 = COS[1, i], s1 = SIN[1, i], c3 = COS[3, i], s3 = SIN[3, i];
         double X1 = s71 * s1, Y1 = -(s12 * c71 + c12 * s71 * c1);
         double X71 = X7 * c1 - Y7 * s1, Y71 = c12 * (X7 * s1 + Y7 * c1) - s12 * Z7;
         double RHS1 = -S6 * X71 - S7 * X1 - a71 * c1 - A12;
         double RHS2 = -S1 - S6 * Y71 - S7 * Y1;
         double Aa = A23 + A34 * c3 - S4 * s3, Ba = -A34 * s3 - S4 * c3, C = -RHS1;  // (11.26)
         double Da = Ba, E = -Aa, F = -RHS2;
         SolveLinearPair (Aa, Ba, C, Da, E, F, out double c2, out double s2);
         if (c2.IsZero () && s2.IsZero ()) {
            OK[i] = OK[i + 1] = false;
            continue;
         }
         TH[2, i] = TH[2, i + 1] = Math.Atan2 (s2, c2); OK[i] = OK[i + 1] = AngleOK (2, TH[2, i]);
      }
      ComputeSinCos (2);
      for (int i = 0; i < 8; i++)
         if (OK[i]) {
            double c2 = COS[2, i], s2 = SIN[2, i], c3 = COS[3, i], s3 = SIN[3, i];
            KAPPA[i] = s3 * c2 + c3 * s2; LAMBDA[i] = c3 * c2 - s3 * s2;
         }

      // ------------------------------------------------
      // 4. Now, to compute theta5. This is computable from the 3 angles we have already computed
      for (int i = 0; i < 8; i += 2) {
         if (!OK[i]) continue;
         double c1 = COS[1, i], s1 = SIN[1, i];
         double Ab = -KAPPA[i], Bb = -LAMBDA[i];
         double c5 = Ab * (X7 * c1 - Y7 * s1) + Bb * Z7;
         if (c5 is < -1 - Lib.Epsilon or > 1 + Lib.Epsilon) {
            OK[i] = OK[i + 1] = false;
            continue;
         }
         OK[i] = AngleOK (5, TH[5, i] = ACos (c5));
         OK[i + 1] = AngleOK (5, TH[5, i + 1] = -TH[5, i]);
      }
      ComputeSinCos (5);

      // ------------------------------------------------
      // 5. Compute theta 4, based on the 4 angles we already computed
      for (int i = 0; i < 8; i++) {
         if (!OK[i]) continue;
         double c1 = COS[1, i], s1 = SIN[1, i], s5 = SIN[5, i];
         double Ac = LAMBDA[i], Bc = -KAPPA[i];
         double c4 = (-Ac * (X7 * c1 - Y7 * s1) - Bc * Z7) / s5;
         double s4 = -(X7 * s1 + Y7 * c1) / s5;
         OK[i] = AngleOK (4, TH[4, i] = Math.Atan2 (s4, c4));
      }
      ComputeSinCos (4);

      // ------------------------------------------------
      // 6. Compute theta 6, based on the other 5 angles.
      for (int i = 0; i < 8; i++) {
         if (!OK[i]) continue;
         double c2 = COS[2, i], s2 = SIN[2, i], c3 = COS[3, i], s3 = SIN[3, i];
         double c4 = COS[4, i], s4 = SIN[4, i], c1 = COS[1, i], s1 = SIN[1, i];
         double Ad = c2 * c3 * s4 - s2 * s3 * s4;
         double Bd = s2 * c3 * s4 + c2 * s3 * s4;
         double s6 = -c7 * (c1 * Ad - s1 * c4) + s7 * (c71 * (Ad * s1 + c1 * c4) + s71 * Bd);
         double c6 = s71 * (Ad * s1 + c1 * c4) - c71 * Bd;
         OK[i] = AngleOK (6, TH[6, i] = Math.Atan2 (s6, c6));
      }
      for (int i = 0; i < 8; i++) TH[1, i] = Lib.NormalizeAngle (TH[1, i] - gamma1);

      // Return the list of possible stances
      for (int i = 0; i < 8; i++) {
         if (OK[i]) {
            double[] a = new double[6];
            for (int j = 0; j < 6; j++) a[j] = TH[j + 1, i];
            mStances.Add (a);
         }
      }
      return mStances;
   }

   /// <summary>Tells whether a particular stanch is within axis limits</summary>
   public bool IsValid (int nSoln) => OK[nSoln];

   /// <summary>Set the limits for a particular axis of the RBRSolver (n should be 1..6)</summary>
   public void SetLimits (int n, double min, double max) {
      mLimits[n, 0] = min; mLimits[n, 1] = max;
   }

   static double ACos (double f) {
      if (f <= -1) return Lib.PI;
      if (f >= 1) return 0;
      return Math.Acos (f);
   }

   /// <summary>This checks if the angle f is acceptable for axis n</summary>
   /// canonical axis angles later
   bool AngleOK (int n, double f) {
      if (n == 1) return true;
      if (f < mLimits[n, 0] || f > mLimits[n, 1]) return false;
      return true;
   }

   /// <summary>Used during IK analsysis to compute the sin and cos of a particular set of theta</summary>
   /// <param name="n">The axis angle theta for which the sin and cos should be computed</param>
   void ComputeSinCos (int n) {
      for (int i = 0; i < 8; i++) {
         if (OK[i]) (SIN[n, i], COS[n, i]) = Math.SinCos (TH[n, i]);
      }
   }

   /// <summary>Solves a system of 2 linear equations with 2 unknowns</summary>
   /// Ax + By + C = 0
   /// Dx + Ey + F = 0
   static void SolveLinearPair (double A, double B, double C, double D, double E, double F, out double x, out double y) {
      double fHypot = A * E - D * B;
      if (fHypot.IsZero ()) { x = y = 0; return; }
      x = (B * F - E * C) / fHypot; y = (D * C - A * F) / fHypot;
   }

   /// <summary>Solves an angle equation of the form Ac + Bs + D = 0, where c = cos(t) and s = sin(t)</summary>
   /// See Equations starting (6.194)
   static void SolveAngleEqn (double A, double B, double D, out double fTheta1a, out double fTheta1b) {
      double hypot = Math.Sqrt (A * A + B * B);
      double gamma = Math.Atan2 (B / hypot, A / hypot);  // (6.196)
      double rhs = -D / hypot;
      if (rhs is < -1 - Lib.Epsilon or > 1 + Lib.Epsilon) {  // FIX This
         fTheta1a = fTheta1b = -999;
         return;
      }
      double theta_gamma = ACos (rhs);
      fTheta1a = Lib.NormalizeAngle (theta_gamma + gamma);
      fTheta1b = Lib.NormalizeAngle (-theta_gamma + gamma);
   }

   // Private data -------------------------------------------------------------
   // These are values used in the kinematic analysis.
   // These are stored in the IKParams[] set of this machine and are loaded in from
   // there when the machine is constructed
   readonly double A12, A23, A34, S2, S4, S6;

   // Constants that are fixed for this type of RBR robot
   const double alpha67 = Lib.HalfPI;
   // Constants that are fixed for this type of RBR robot
   static readonly double alpha12 = 270.D2R ();

   // The 8 possible solutions, and the 6 possible axis angles for each one.
   // We don't use the TH[0,N] (for consistency with the equations in the
   // text). TH[A,B] contains the angle of the Ath axis in the Bth solution.
   readonly double[,] TH = new double[7, 8];
   // If OK[n] is set then the solution n is valid
   readonly bool[] OK = new bool[8];
   // SIN[A,B] contains the sin of TH[A,B] (used in several places)
   readonly double[,] SIN = new double[7, 8], COS = new double[7, 8];
   // This contains the KAPPA value for each solution (an intermediate value used in computation)
   readonly double[] KAPPA = new double[8];
   // The intermediate LAMBDA value for each solution
   readonly double[] LAMBDA = new double[8];
   // These are the possible stances (6-axis solutions)
   readonly List<double[]> mStances = new ();
   // These are the limits of the axes
   readonly double[,] mLimits = new double[7, 2];
}
#endregion
