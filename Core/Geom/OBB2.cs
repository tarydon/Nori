using System.Drawing;
using System.Net;

namespace Nori;

public readonly partial struct OBB {
   public static OBB FromAlt (ReadOnlySpan<Point3f> pts) {
      return new Builder2 (pts).Build ();
   }

   public static OBB FromAltNew (ReadOnlySpan<Point3f> pts) {
      return new Builder3 (pts).Build ();
   }

   unsafe ref struct Builder2 (ReadOnlySpan<Point3f> pts) {
      // The input points
      readonly ReadOnlySpan<Point3f> Pts = pts;

      public OBB Build () {
         fixed (Point3f* pts = Pts) {
            P = pts;
            var h = stackalloc int[14]; H = h;
            var min = stackalloc float[7]; Min = min;
            var max = stackalloc float[7]; Max = max;
            DoBuild ();
            return Box;
         }
      }

      void DoBuild () {
         Point3f pr = P[0];
         for (int i = 0; i < 14; i++) H[i] = 0;
         Min[0] = Max[0] = pr.X; Min[1] = Max[1] = pr.Y; Min[2] = Max[2] = pr.Z;
         Min[3] = Max[3] = pr.X + pr.Y + pr.Z; Min[4] = Max[4] = pr.X + pr.Y - pr.Z;
         Min[5] = Max[5] = pr.X - pr.Y + pr.Z; Min[6] = Max[6] = -pr.X + pr.Y + pr.Z;

         // 1. Compute extremal points wrt the 7 'known' axes: X, Y, Z, (1, 1, 1), (1, 1, -1), (1, -1, 1) and (-1, 1, 1)
         for (int i = 1; i < Pts.Length; i++) {
            Point3f p = P[i];
            float f = p.X;          // (1,0,0) axis
            if (f < Min[0]) (Min[0], H[0]) = (f, i);
            else if (f > Max[0]) (Max[0], H[7]) = (f, i);

            f = p.Y;                // (0,1,0) axis
            if (f < Min[1]) (Min[1], H[1]) = (f, i);
            else if (f > Max[1]) (Max[1], H[8]) = (f, i);

            f = p.Z;                // (0,0,1) axis
            if (f < Min[2]) (Min[2], H[2]) = (f, i);
            else if (f > Max[2]) (Max[2], H[9]) = (f, i);

            f = p.X + p.Y + p.Z;    // (1,1,1) axis
            if (f < Min[3]) (Min[3], H[3]) = (f, i);
            else if (f > Max[3]) (Max[3], H[10]) = (f, i);

            f = p.X + p.Y - p.Z;    // (1,1,-1) axis
            if (f < Min[4]) (Min[4], H[4]) = (f, i);
            else if (f > Max[4]) (Max[4], H[11]) = (f, i);

            f = p.X - p.Y + p.Z;    // (1,-1,-1) axis
            if (f < Min[5]) (Min[5], H[5]) = (f, i);
            else if (f > Max[5]) (Max[5], H[12]) = (f, i);

            f = -p.X + p.Y + p.Z;    // (1,-1,1)
            if (f < Min[6]) (Min[6], H[6]) = (f, i);
            else if (f > Max[6]) (Max[6], H[13]) = (f, i);
         }

         // 2. Make AABB as fallback OBB. (Note that the first three axes in the sample are world X, Y and Z)
         var aabb = Box = CreateBox (X, Y, Z); BestScore = Area (aabb.Extent);

         // 3. Build the irr. ditetrahedron now.
         // 3.a. First find the base triangle (p0, p1, p2)
         var (p0, p1, p2) = (H[0], H[7], -1);
         var dMax = P[p0].DistToSq (P[p1]);
         for (int i = 1; i < 7; i++) {
            var (a, b) = (H[i], H[7 + i]);
            var d = P[a].DistToSq (P[b]);
            if (d > (dMax + Lib.Epsilon)) (dMax, p0, p1) = (d, a, b);
         }

         // p0-p1 is the longest line in the 'set'. Now find the point
         // farthest from the this line to get the 'base triangle'
         dMax = double.MinValue; var (P0, P1) = ((Point3)P[p0], (Point3)P[p1]);
         for (int i = 0; i < 14; i++) {
            var n = H[i]; if (n == p0 || n == p1) continue;
            var d = ((Point3)P[n]).DistToLineSq (P0, P1);
            if (d > (dMax + Lib.Epsilon)) (dMax, p2) = (d, n);
         }

         // 3.b. Now we have the base of the triangle as (p0, p1, p2).
         //  Compute the top and bottom tetrahedron apex points (q0, q1).
         var (q0, q1) = GetTetraApexPoints (p0, p1, p2);

         // 4. Refine the box for the smallest surface area by locating the best
         //  axes from the ditetraheron triangles.
         ReadOnlySpan<int> tri = [p0, p1, p2, q0, q1];
         for (int i = 0; i < tri.Length - 2; i++)
            for (int j = i + 1; j < tri.Length - 1; j++)
               for (int k = j + 1; k < tri.Length; k++)
                  RefineBox (tri[i], tri[j], tri[k]);

         // 5. Final step - Refine the box by doing a final pass through all points along OBB axes
         ComputeProjections (null, Pts.Length, Box.X, Box.Y, Box.Z);
         Box = CreateBox (Box.X, Box.Y, Box.Z);
         // Snap to AABB if it is close or smaller
         if (aabb.Area < (Box.Area + E)) Box = aabb;
      }

      // The 'area' score. We are trying to minimize this.
      static float Area (in Vector3f extent) => extent.X * extent.Y + extent.Y * extent.Z + extent.Z * extent.X;
      // The 'volume' for given extent (1/8th of actual volume).
      static float Volume (in Vector3f extent) => extent.X * extent.Y * extent.Z;

      // Computes projection of given 'set' of points along given orthogonal (u,v,w) axes. 
      // The projection is evaluated for the input set 'P' if 'set' parameter is NULL.
      void ComputeProjections (int* set, int count, in Vector3f u, in Vector3f v, in Vector3f w) {
         Min[0] = Min[1] = Min[2] = float.MaxValue;
         Max[0] = Max[1] = Max[2] = float.MinValue;
         for (int j = 0; j < count; j++) {
            int n = set == null ? j : set[j];
            ref Point3f p = ref P[n];
            // Compute projection of point 'p' on axes (basically Dot (p, axis)
            var d = p.X * u.X + p.Y * u.Y + p.Z * u.Z;
            if (d < (Min[0] - E)) Min[0] = d;
            if (d > (Max[0] + E)) Max[0] = d;

            // Compute projection of point 'p' on v axis
            d = p.X * v.X + p.Y * v.Y + p.Z * v.Z;
            if (d < (Min[1] - E)) Min[1] = d;
            if (d > (Max[1] + E)) Max[1] = d;

            // Compute projection of point 'p' on w axis
            d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
            if (d < (Min[2] - E)) Min[2] = d;
            if (d > (Max[2] + E)) Max[2] = d;
         }
      }

      // Given three orthogonal axes and the projection lengths along them, creates the respective OBB.
      readonly OBB CreateBox (in Vector3f u, in Vector3f v, in Vector3f w) {
         var cen = (u * (Max[0] + Min[0]) + v * (Max[1] + Min[1]) + w * (Max[2] + Min[2])) * 0.5f;
         var ext = new Vector3f (Max[0] - Min[0], Max[1] - Min[1], Max[2] - Min[2]) * 0.5f;
         return new (new (cen.X, cen.Y, cen.Z), u, v, ext);
      }

      // Given the 'base triangle' (a, b, c), searches for the apex points (p, q) in the given point 'set'
      readonly (int p, int q) GetTetraApexPoints (int a, int b, int c) {
         // The triangle plane.
         var w = (P[b] - P[a]) * (P[c] - P[a]); // The plane normal
         // Now find min-max along normal 'w'.
         var (min, max) = (float.MaxValue, float.MinValue);
         var (q0, q1) = (-1, -1);
         for (int i = 0; i < 14; i++) {
            int n = H[i]; if (n == a || n == b || n == c) continue;
            ref var p = ref P[n];
            // Compute projection of point 'p' on normal 'w' (p.Dot (w))
            var d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
            if (d < min) (min, q0) = (d, n);
            if (d > max) (max, q1) = (d, n);
         }
         return (q0, q1);
      }

      // Given triangle (a, b, c), walks across the triangle edges to find the best OBB axis. It uses the total
      // surface area score to compare if the new OBB is better than the current 'best' and updates the 'best' if needed.
      void RefineBox (int a, int b, int c) {
         var u = P[b] - P[a]; var v = P[c] - P[a];
         var w = u * v; if (w.IsZero) return; // The plane normal
         u = u.Normalized (); w = w.Normalized ();
         v = w * u;
         // Iterate over all three triangle sides
         for (int pass = 0; pass < 3; pass++) {
            if (pass > 0) {
               (a, b, c) = (b, c, a);
               u = (P[b] - P[a]).Normalized ();
               v = w * u;
            }
            // Compute OBB from orthogonal axes (u, v, w)
            ComputeProjections (H, 14, u, v, w);
            // Half extents along the 3 axes
            var half = new Vector3f (Max[0] - Min[0], Max[1] - Min[1], Max[2] - Min[2]) * 0.5f;
            if (Volume (half).IsZero ()) continue;
            var area = Area (half);
            if (area < (BestScore - E)) (Box, BestScore) = (CreateBox (u, v, w), area);
         }
      }

      // Given Hull index 'nH' and point 'nP', updates H if nP is a new extremal.
      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      void UpdateHull (float d, int nH, int nP) {
         if (d < Min[nH] - E) (Min[nH], H[nH]) = (d, nP);
         if (d > Max[nH] + E) (Max[nH], H[7 + nH]) = (d, nP);
      }

      // The box we are preparing.
      OBB Box;
      // Best box score so far.
      float BestScore;
      // Points array
      Point3f* P;
      // The extremal point set 'H' of size 14 and min/max extents (7 each).
      // First half of H belong to the min projections whereas second half are max projections.
      // And elements 'i' and '7 + i' belong to the same axis.
      int* H; float* Min, Max;
      readonly static Vector3f X = new (1, 0, 0), Y = new (0, 1, 0), Z = new (0, 0, 1);
      const float E = 1E-6f;
   }
}
