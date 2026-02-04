// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBB.cs
// ║║║║╬║╔╣║ Implements minimum enclosing 'Orientend Bounding Box' in 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region struct OBB ---------------------------------------------------------------------------------
/// <summary>Represents a bounding cuboid oriented along an arbitrary axes.</summary>
public readonly struct OBB {
   public OBB (Point3f cen, Vector3f x, Vector3f y, Vector3f ext) => (Center, X, Y, Extent) = (cen, x, y, ext);

   /// <summary> The box center</summary>
   public readonly Point3f Center;
   /// <summary>The 'half extent' along the axes.</summary>
   public readonly Vector3f Extent;
   /// <summary>Bounding box's co-ordinate axes.</summary>
   public readonly Vector3f X, Y;
   public Vector3f Z => X * Y;
   /// <summary>The box area</summary>
   public double Area => 8 * (Extent.X * Extent.Y + Extent.X * Extent.Z + Extent.Y * Extent.Z);
   /// <summary>The box volume</summary>
   public double Volume => 8 * (Extent.X * Extent.Y * Extent.Z);

   /// <summary>Tries to find a tight oriented bound for a given set of points.</summary>
   /// This OBB search takes O(n) time to find 'nearly' optimal OBB orientation. It falls
   /// back to an 'AABB' if it cannot find a better fit.
   /// <param name="pts">The input point set</param>
   // The approach below samples a finite, relatively smaller set of points, uniformly spaced
   // in the 3D space. It then finds two irregular tetrahedrons sharing same base triangle and
   // facing in the opposite directions. The tetrahedron face triangles are then used to find 
   // the tightest bound and orientation (the OBB).
   public static OBB From (ReadOnlySpan<Point3f> pts) => new Builder (pts).Build ();

   unsafe ref struct Builder (ReadOnlySpan<Point3f> pts) {
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
         for (int i = 0; i < 7; i++) (Min[i], Max[i]) = (float.MaxValue, float.MinValue);
         // 1. Compute extremal points wrt the 7 'known' axes: X, Y, Z, (1, 1, 1), (1, 1, -1), (1, -1, 1) and (-1, 1, 1)
         for (int i = 0; i < Pts.Length; i++) {
            ref Point3f p = ref P[i];
            UpdateHull (p.X, 0, i);
            UpdateHull (p.Y, 1, i);
            UpdateHull (p.Z, 2, i);
            UpdateHull (p.X + p.Y + p.Z, 3, i);
            UpdateHull (p.X + p.Y - p.Z, 4, i);
            UpdateHull (p.X - p.Y + p.Z, 5, i);
            UpdateHull (-p.X + p.Y + p.Z, 6, i);
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
         if (aabb.Area < (Box.Area + Epsilon)) Box = aabb;
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
            if (d < (Min[0] - Epsilon)) Min[0] = d;
            if (d > (Max[0] + Epsilon)) Max[0] = d;

            // Compute projection of point 'p' on v axis
            d = p.X * v.X + p.Y * v.Y + p.Z * v.Z;
            if (d < (Min[1] - Epsilon)) Min[1] = d;
            if (d > (Max[1] + Epsilon)) Max[1] = d;

            // Compute projection of point 'p' on w axis
            d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
            if (d < (Min[2] - Epsilon)) Min[2] = d;
            if (d > (Max[2] + Epsilon)) Max[2] = d;
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
            if (area < (BestScore - Epsilon)) (Box, BestScore) = (CreateBox (u, v, w), area);
         }
      }

      // Given Hull index 'nH' and point 'nP', updates H if nP is a new extremal.
      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      void UpdateHull (float d, int nH, int nP) {
         if (d < Min[nH] - Epsilon) (Min[nH], H[nH]) = (d, nP);
         if (d > Max[nH] + Epsilon) (Max[nH], H[7 + nH]) = (d, nP);
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
      const float Epsilon = 1E-6f;
   }

   public static OBB FromPCA (ReadOnlySpan<Point3f> pts) {
      // Compute the mean (xc, yc, zc)
      int n = pts.Length;
      float xc = 0, yc = 0, zc = 0;
      foreach (var p in pts) { xc += p.X; yc += p.Y; zc += p.Z; }
      xc /= n; yc /= n; zc /= n;

      // Compute the covariance
      float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
      foreach (var p in pts) {
         float x = p.X - xc, y = p.Y - yc, z = p.Z - zc;
         xx += x * x; yy += y * y; zz += z * z;
         xy += x * y; xz += x * z; yz += y * z;
      }
      xx /= n; yy /= n; zz /= n;
      xy /= n; xz /= n; yz /= n;

      // Prepare for JacobiEigenDecomposition
      Span<Vector3f> axis = stackalloc Vector3f[3];
      axis[0] = new (1, 0, 0); axis[1] = new (0, 1, 0); axis[2] = new (0, 0, 1);
      Span<float> a = stackalloc float[9];
      a[0] = xx; a[1] = xy; a[2] = xy;
      a[3] = xy; a[4] = yy; a[5] = yz;
      a[6] = xz; a[7] = yz; a[8] = zz;

      // Iterate to compute the eigenvectors
      for (int iter = 0; iter < 50; iter++) {
         Rotate (a, axis, 0, 1);
         Rotate (a, axis, 0, 2);
         Rotate (a, axis, 1, 2);

         float offDiagonal = MathF.Abs (a[1]) + MathF.Abs (a[2]) + MathF.Abs (a[5]);
         if (offDiagonal < 1e-6f) break;
      }

      // Now ensure the vectors are an orthonormal basis
      axis[0] = axis[0].Normalized (); axis[1] = axis[1].Normalized ();
      axis[2] = (axis[0] * axis[1]).Normalized ();
      axis[1] = (axis[2] * axis[0]).Normalized ();

      // Project points onto axes
      Span<float> min = stackalloc float[3], max = stackalloc float[3];
      for (int i = 0; i < 3; i++) { min[i] = float.MaxValue; max[i] = float.MinValue; }
      foreach (var p in pts) {
         float x = p.X - xc, y = p.Y - yc, z = p.Z - zc;
         for (int i = 0; i < 3; i++) {
            var ax = axis[i];
            float dot = x * ax.X + y * ax.Y + z * ax.Z;
            min[i] = MathF.Min (min[i], dot);
            max[i] = MathF.Max (max[i], dot);
         }
      }

      // Compute center and half-sizes
      Vector3f ext = (new Vector3f (max[0] - min[0], max[1] - min[1], max[2] - min[2])) * 0.5f;
      Point3f cen = new (xc, yc, zc);
      for (int i = 0; i < 3; i++)
         cen += axis[i] * ((min[i] + max[i]) / 2f);

      // Now, return the OBB
      return new OBB (cen, axis[0], axis[1], ext);
   }

   static void Rotate (Span<float> a, Span<Vector3f> axis, int p, int q) {
      float aPQ = a[p * 3 + q];
      if (Math.Abs (aPQ) < 1e-6f) return;

      // Note that since we flatten a[3,3] to a flat vector, a[i,j] is effectively
      // reached as a[i * 3 + j]. Thus a[p,p] becomes a[p * 3 + p] (or a[p * 4])
      float aPP = a[p * 4], aQQ = a[q * 4];
      float diff = aQQ - aPP, t = diff == 0 ? 1 : aPQ / diff;
      float c = 1 / MathF.Sqrt (1 + t * t), s = t * c;

      a[p * 4] = c * c * aPP - 2 * s * c * aPQ + s * s * aQQ;
      a[q * 4] = s * s * aPP + 2 * s * c * aPQ + c * c * aQQ;
      a[p * 3 + q] = a[q * 3 + p] = 0;

      int r = 3 - p - q;   // Since p,q,r are selected from (0,1,2)
      float aRP = a[r * 3 + p], aRQ = a[r * 3 + q];
      a[r * 3 + p] = a[p * 3 + r] = c * aRP - s * aRQ;
      a[r * 3 + q] = a[q * 3 + r] = s * aRP + c * aRQ;

      Vector3f vp = axis[p], vq = axis[q];
      axis[p] = vp * c - vq * s;
      axis[q] = vp * s + vq * c;
   }
}
#endregion
