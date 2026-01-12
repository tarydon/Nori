// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collision.cs
// ║║║║╬║╔╣║ Primitive collision detection methods
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

public static class Collision {
   /// <summary>Checks if 'a' Bound3 intersects with another Bound3 'b'</summary>
   public static bool Check (in Bound3 a, in Bound3 b) => Check (a.X, b.X) && Check (a.Y, b.Y) && Check (a.Z, b.Z);

   /// <summary>Checks if 'a' Bound1 intersects with another Bound1 'b'</summary>
   public static bool Check (in Bound1 a, in Bound1 b) => a.Min <= b.Max && a.Max >= b.Min;

   /// <summary>Checks if 'a' sphere intersects with another sphere 'b'</summary>
   public static bool Check (in MinSphere A, in MinSphere B) {
      var r = A.Radius + B.Radius;
      return (A.Center - B.Center).LengthSq <= r * r;
   }

   /// <summary>Checks if two OBBs intersect.</summary>
   public static bool Check (in OBB a, in OBB b) =>
      ObbObb (a.Center, a.CS.VecX, a.CS.VecY, a.Extent, b.Center, b.CS.VecX, b.CS.VecY, b.Extent);

   /// <summary>Checks if two OBBs intersect using the Separating Axis Theorem (SAT)</summary>
   /// The algorithm tests 15 potential separating axes, and if no separating axis is found, the 
   /// OBBs are intersecting. The 15 axes are the 3 axes of each OBB (face-face) and the 
   /// 9 cross products of these axes (edge-edge).
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool ObbObb (Point3 aC, Vector3 aX, Vector3 aY, Vector3 aR, Point3 bC, Vector3 bX, Vector3 bY, Vector3 bR) {
      const double E = 1e-6;
      Vector3 aZ = aX * aY, bZ = bX * bY;
      double a0 = aR.X, a1 = aR.Y, a2 = aR.Z, b0 = bR.X, b1 = bR.Y, b2 = bR.Z;

      // Test A axes: aX, aY, aZ
      // The translation vector T <t0, t1, t2> from a to b (in a's coordinate system)      
      var tmp = bC - aC; double t0 = Dot (tmp, aX);
      // The rotation matrix R (R00, R01 ... R33) represents 'b' in a's coordinate system.
      // Since the absolute values of R (denoted AR = |R|) are repeatedly used in all tests,
      // we precompute them once for efficiency. A small epsilon is added to handle
      // numerical errors that occur when two edges are nearly parallel, causing their
      // cross product to approach zero.
      // T, R, and AR are initialized in an interleaved manner to avoid redundant computations,
      // with early termination triggered on detection of a separating axis.
      double R00 = Dot (aX, bX), R01 = Dot (aX, bY), R02 = Dot (aX, bZ);
      double AR00 = Abs (R00) + E, AR01 = Abs (R01) + E, AR02 = Abs (R02) + E;
      if (Abs (t0) > a0 + b0 * AR00 + b1 * AR01 + b2 * AR02) return false;

      double t1 = Dot (tmp, aY);
      double R10 = Dot (aY, bX), R11 = Dot (aY, bY), R12 = Dot (aY, bZ);
      double AR10 = Abs (R10) + E, AR11 = Abs (R11) + E, AR12 = Abs (R12) + E;
      if (Abs (t1) > a1 + b0 * AR10 + b1 * AR11 + b2 * AR12) return false;

      double t2 = Dot (tmp, aZ);
      double R20 = Dot (aZ, bX), R21 = Dot (aZ, bY), R22 = Dot (aZ, bZ);
      double AR20 = Abs (R20) + E, AR21 = Abs (R21) + E, AR22 = Abs (R22) + E;
      if (Abs (t2) > a2 + b0 * AR20 + b1 * AR21 + b2 * AR22) return false;

      // Test B axes: bX, bY, bZ
      if (Abs (t0 * R00 + t1 * R10 + t2 * R20) > b0 + a0 * AR00 + a1 * AR10 + a2 * AR20) return false;
      if (Abs (t0 * R01 + t1 * R11 + t2 * R21) > b1 + a0 * AR01 + a1 * AR11 + a2 * AR21) return false;
      if (Abs (t0 * R02 + t1 * R12 + t2 * R22) > b2 + a0 * AR02 + a1 * AR12 + a2 * AR22) return false;

      // Test 9 (Ai x Bj) axis pairs
      if (Abs (t2 * R10 - t1 * R20) > (a1 * AR20 + a2 * AR10 + b1 * AR02 + b2 * AR01)) return false;  // aX x bX
      if (Abs (t2 * R11 - t1 * R21) > (a1 * AR21 + a2 * AR11 + b2 * AR00 + b0 * AR02)) return false;  // aX x bY
      if (Abs (t2 * R12 - t1 * R22) > (a1 * AR22 + a2 * AR12 + b0 * AR01 + b1 * AR00)) return false;  // aX x bZ

      if (Abs (t0 * R20 - t2 * R00) > (a2 * AR00 + a0 * AR20 + b1 * AR12 + b2 * AR11)) return false;  // aY x bX
      if (Abs (t0 * R21 - t2 * R01) > (a2 * AR01 + a0 * AR21 + b2 * AR10 + b0 * AR12)) return false;  // aY x bY
      if (Abs (t0 * R22 - t2 * R02) > (a2 * AR02 + a0 * AR22 + b0 * AR11 + b1 * AR10)) return false;  // aY x bZ

      if (Abs (t1 * R00 - t0 * R10) > (a0 * AR10 + a1 * AR00 + b1 * AR22 + b2 * AR21)) return false;  // aZ x bX
      if (Abs (t1 * R01 - t0 * R11) > (a0 * AR11 + a1 * AR01 + b2 * AR20 + b0 * AR22)) return false;  // aZ x bY
      if (Abs (t1 * R02 - t0 * R12) > (a0 * AR12 + a1 * AR02 + b0 * AR21 + b1 * AR20)) return false;  // aZ x bZ

      // No separating axis found, the OBBs are intersecting
      return true;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static double Dot (Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
   }
}
