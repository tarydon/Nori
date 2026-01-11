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
   public static bool Check (in Bound1 a, in Bound1 b) => !(a.Min > b.Max || a.Max < b.Min);

   /// <summary>Checks if 'a' sphere intersects with another sphere 'b'</summary>
   public static bool Check (in MinSphere A, in MinSphere B) {
      var r = A.Radius + B.Radius;
      return (A.Center - B.Center).LengthSq <= r * r;
   }

   /// <summary>Checks if an OBB 'a' intersects with another OBB 'b'</summary>
   public static bool Check (in OBB a, in OBB b) => 
      ObbObb (a.Center, a.CS.VecX, a.CS.VecY, a.Extent, b.Center, b.CS.VecX, b.CS.VecY, b.Extent);

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool ObbObb (Point3 aC, Vector3 aX, Vector3 aY, Vector3 aR, Point3 bC, Vector3 bX, Vector3 bY, Vector3 bR) {
      const double E = double.Epsilon;
      var aZ = aX * aY;
      ReadOnlySpan<Vector3> aAxes = [aX, aY, aZ], bAxes = [bX, bY, bX * bY];
      // The translation vector from a to b (in a's coordinate system)
      var tmp = bC - aC;
      ReadOnlySpan<double> T = [tmp.Dot (aX), tmp.Dot (aY), tmp.Dot (aZ)];
      ReadOnlySpan<double> ra = [aR.X, aR.Y, aR.Z];
      ReadOnlySpan<double> rb = [bR.X, bR.Y, bR.Z];

      // The rotation matrix expressing b in a's coordinate system
      Span2<double> R = new (stackalloc double[9], 3);
      for (int i = 0; i < 3; i++) {
         var aAxis = aAxes[i];
         for (int j = 0; j < 3; j++) {
            R[i, j] = aAxis.Dot (bAxes[j]);
         }
      }

      // Add in an epsilon term to counteract arithmetic errors when two edges are parallel.
      Span2<double> AbsR = new (stackalloc double[9], 3);
      for (int i = 0; i < 3; i++)
         for (int j = 0; j < 3; j++)
            AbsR[i, j] = Abs (R[i, j]) + E;

      // Test 3 axes A0, A1, A2
      for (int i = 0; i < 3; i++) {
         var r2 = rb[0] * AbsR[i, 0] + rb[1] * AbsR[i, 1] + rb[2] * AbsR[i, 2];
         if (Abs (T[i]) > ra[i] + r2) return false;
      }

      // Test 3 axes B0, B1, B2
      for (int i = 0; i < 3; i++) {
         var r1 = ra[0] * AbsR[0, i] + ra[1] * AbsR[1, i] + ra[2] * AbsR[2, i];
         var t = Abs (T[0] * R[0, i] + T[1] * R[1, i] + T[2] * R[2, i]);
         if (t > r1 + rb[i]) return false;
      }

      // Test 9 (Ai x Bj) axis pairs
      for (int i = 0; i < 3; i++) {
         for (int j = 0; j < 3; j++) {
            int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
            int j1 = (j + 1) % 3, j2 = (j + 2) % 3;

            var r1 = ra[i1] * AbsR[i2, j] + ra[i2] * AbsR[i1, j];
            var r2 = rb[j1] * AbsR[i, j2] + rb[j2] * AbsR[i, j1];
            var t = Abs (T[i2] * R[i1, j] - T[i1] * R[i2, j]);
            if (t > r1 + r2) return false;
         }
      }

      // No separating axis found, the OBBs are intersecting
      return true;
   }

   // A simple, stack-friendly 2D span wrapper
   readonly ref struct Span2<T> (in Span<T> data, int cols) {
      public ref T this[int r, int c] => ref Data[r * Cols + c];
      readonly Span<T> Data = data;
      readonly int Cols = cols;
   }
}
