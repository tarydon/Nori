// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collision.cs
// ║║║║╬║╔╣║ Primitive collision detection methods
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Math;
namespace Nori;

/// <summary>Represents a triangle defined by three points in 3D space.</summary>
public struct Tri (Point3 a, Point3 b, Point3 c) {
   public readonly Point3 A = a;
   public readonly Point3 B = b;
   public readonly Point3 C = c;

   public Bound3 Bound => _bound ??= ComputeBound ();
   Bound3? _bound = null;

   readonly Bound3 ComputeBound () => new (A, B, C);
}

/// <summary>Provides methods for collision detection between various geometric primitives.</summary>
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
      BoxBox (a.Center, a.CS.VecX, a.CS.VecY, a.Extent, b.Center, b.CS.VecX, b.CS.VecY, b.Extent);

   public static bool Check (in Tri a, in OBB b) =>
      BoxTri (b.Center, b.CS.VecX, b.CS.VecY, b.Extent, a.A, a.B, a.C);
   //Check (a.Bound, b.Bound);

   /// <summary>Checks if two OBBs intersect using the Separating Axis Theorem (SAT)</summary>
   /// The algorithm tests 15 potential separating axes, and if no separating axis is found, the 
   /// OBBs are intersecting. The 15 axes are the 3 axes of each OBB (face-face) and the 9 axes 
   /// formed by the cross products of each pair of axes from the two OBBs (edge-edge).
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool BoxBox (Point3 aC, Vector3 aX, Vector3 aY, Vector3 aR, Point3 bC, Vector3 bX, Vector3 bY, Vector3 bR) {
      const double E = 1e-6;
      Vector3 aZ = aX * aY, bZ = bX * bY;
      double a0 = aR.X, a1 = aR.Y, a2 = aR.Z, b0 = bR.X, b1 = bR.Y, b2 = bR.Z;

      // Check 1. Test A axes: aX, aY, aZ
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

      // Check 2. Test B axes: bX, bY, bZ
      if (Abs (t0 * R00 + t1 * R10 + t2 * R20) > b0 + a0 * AR00 + a1 * AR10 + a2 * AR20) return false;
      if (Abs (t0 * R01 + t1 * R11 + t2 * R21) > b1 + a0 * AR01 + a1 * AR11 + a2 * AR21) return false;
      if (Abs (t0 * R02 + t1 * R12 + t2 * R22) > b2 + a0 * AR02 + a1 * AR12 + a2 * AR22) return false;

      // Check 3. Test 9 (Ai x Bj) axis pairs
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
   }

   /// <summary>Checks if an oriented box (OBB) intersects with a triangle defined by points p1, p2, and p3.</summary>
   /// It uses the Separating Axis Theorem (SAT) to determine if there is a separating axis between the box and the triangle.
   /// It uses 13 potential separating axes: the three face normals of the box, the triangle's face normal, and the nine 
   /// cross products of the box edges and triangle edges. If no separating axis is found, the box and triangle are intersecting.
   /// <param name="bC">Box center.</param>
   /// <param name="bX">Box X-axis</param>
   /// <param name="bY">Box Y-axis</param>
   /// <param name="bH">Box half extents</param>
   /// <param name="p0">Triangle vertex 1</param>
   /// <param name="p1">Triangle vertex 2</param>
   /// <param name="p2">Triangle vertex 3</param>
   /// <returns>True, if there is a collision. False otherwise.</returns>
   public static bool BoxTri (Point3 bC, Vector3 bX, Vector3 bY, Vector3 bH, Point3 p0, Point3 p1, Point3 p2) {
      // Transform triangle vertices (p0, p1, p2) into box's local space as (a, b, c)
      var bZ = bX * bY;
      Vector3 v0 = p0 - bC, v1 = p1 - bC, v2 = p2 - bC;
      var a = new Point3 (Dot (v0, bX), Dot (v0, bY), Dot (v0, bZ));
      var b = new Point3 (Dot (v1, bX), Dot (v1, bY), Dot (v1, bZ));
      var c = new Point3 (Dot (v2, bX), Dot (v2, bY), Dot (v2, bZ));

      // Check 1. Nine edge cross products
      Vector3 va = (Vector3)a, vb = (Vector3)b, vc = (Vector3)c;
      // Optimize the cross product calculations for the axes
      // by directly using the components of the triangle edges.
      // a x b = (ax, ay, az) x (bx, by, bz) = (ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx)
      // a x xAxis = (ax, ay, az) x (1, 0, 0) = (0, az, -ay)
      // a x yAxis = (ax, ay, az) x (0, 1, 0) = (-az, 0, ax)
      // a x zAxis = (ax, ay, az) x (0, 0, 1) = (ay, -ax, 0)

      // Edge 0
      var e0 = b - a;
      if (!TestAxis (new (0, e0.Z, -e0.Y), bH, va, vb, vc)) return false;
      if (!TestAxis (new (-e0.Z, 0, e0.X), bH, va, vb, vc)) return false;
      if (!TestAxis (new (e0.Y, -e0.X, 0), bH, va, vb, vc)) return false;

      // Edge 1
      var e1 = c - b;
      if (!TestAxis (new (0, e1.Z, -e1.Y), bH, va, vb, vc)) return false;
      if (!TestAxis (new (-e1.Z, 0, e1.X), bH, va, vb, vc)) return false;
      if (!TestAxis (new (e1.Y, -e1.X, 0), bH, va, vb, vc)) return false;

      // Edge 2
      var e2 = a - c;
      if (!TestAxis (new (0, e2.Z, -e2.Y), bH, va, vb, vc)) return false;
      if (!TestAxis (new (-e2.Z, 0, e2.X), bH, va, vb, vc)) return false;
      if (!TestAxis (new (e2.Y, -e2.X, 0), bH, va, vb, vc)) return false;

      // Check 2. Check Box's AABB vs Triangle's AABB (three face normals of the Box)
      Bound3 b1 = new (-bH.X, -bH.Y, -bH.Z, bH.X, bH.Y, bH.Z), b2 = new (a, b, c);
      if (!Check (b1, b2)) return false;

      // Check 3. Triangle's face normal (basically box to triangle plane)
      var n = ((b - a) * (c - a)).Normalized (); // Triangle normal
      // The radius of the box projected onto the triangle normal
      var r = bH.X * Abs (n.X) + bH.Y * Abs (n.Y) + bH.Z * Abs (n.Z);
      // If the distance from the box center to the triangle plane is
      // greater than the projected radius, there is a separating axis.
      if (Abs (Dot (n, new (a.X, a.Y, a.Z))) > r) return false;

      // No separating axis found. The OBB and triangle are intersecting.
      return true;

      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      static bool TestAxis (in Vector3 axis, in Vector3 bH, in Vector3 a, in Vector3 b, in Vector3 c) {
         // The radius of the box projected onto the axis
         var r = bH.X * Abs (axis.X) + bH.Y * Abs (axis.Y) + bH.Z * Abs (axis.Z);

         // Project triangle onto axis to find min and max
         double d0 = Dot (axis, a), d1 = Dot (axis, b), d2 = Dot (axis, c);
         var (min, max) = (d0, d0);
         if (d1 < min) min = d1; else if (d1 > max) max = d1;
         if (d2 < min) min = d2; else if (d2 > max) max = d2;

         // If the distance from the box center to the triangle projection
         // is greater than the projected radius, there is a separating axis.
         return !(min > r || max < -r);
      }
   }

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static double Dot (Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
