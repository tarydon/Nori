// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBBuild
// ║║║║╬║╔╣║ Implements OBB builder (OBBDitoBuilder, OBBPCABuilder)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Internal;

#region struct OBBDitoBuilder ----------------------------------------------------------------------
/// <summary>Builds OBBs using the DiTetrahedral algorithm</summary>
/// In brief:
/// - We take the input point set and compute a small subset of 'extremal' points
///   by projecting the points on 7 axes (aligned with the 3 defining axes, and the 
///   4 body diagonals of a unit cube). There will be 14 points (not all unique)
/// - We find the longest 'axis' among these 7 axes (by inspecting the 14 points 
///   two at time), and get the points P0,P1. Then, we find the furthest point from 
///   this one (still working with our constrained set of 14), as P2. 
/// - (P0,P1,P2) now represents the common shared triangle of a pair of tetrahedrons.
///   By finding the furthest point from this on either side, we get two tetrahedrons
///   with this base, and with apices Q0 and Q1. 
/// - Now we have two tetrahedrons sharing a base, and thus 7 unique triangles. Each 
///   triangle generates 3 sets of orthonormal trial axes:
///   = take each edge of the triangle as one axis
///   = take the perpendicular to the triangle as the other
///   = the third is perpendicular to both
/// - We pick a 'best' set of orthonormal axes from among the 21 sets we generate (3
///   from each of the 7 triangles) by using those axes to compute a quick OBB using only
///   the 14 points. 
/// - We finally compute the candidate OBB by projecting the complete set of input points
///   on these axes to update the final bounds of the OBB. 
/// Note: in some rare cases the AABB is a tighter fit than the OBB we compute by this
/// method, so we try that also as a fallback. 
/// 
/// Since OBB construction is a frequently used, performance critical primitive, this 
/// implementation is highly tuned. The code is verbose since a lot of it has been 'unrolled'
/// for better performance. Also, it is unsafe since it uses pointers extensively (rather 
/// than spans or arrays which are bounds checked). 
readonly unsafe struct OBBDitoBuilder {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize the OBB builder with a set of Point3f</summary>
   /// Note that this 'constructor' actually completes the entire building of the
   /// OBB and stores the computed result in the OBB property
   public OBBDitoBuilder (ReadOnlySpan<Point3f> input) {
      fixed (Point3f* pts = input) {
         // 1. Allocate stack memory, create pointers
         // Create a pointer to the input set of points (mP), and additional pointers:
         // mQ is a pointer to 14 points used as the extremals in the 7 axes
         // mE is a pointer to 14 floats storing these extremal projection values
         mP = pts;
         var q = stackalloc Point3f[19]; mQ = q; mR = &q[14];
         var ext = stackalloc float[14]; mE = ext;

         // 2. Compute the extents of the original point set as projected along 
         // each of the 7 axes (we are using the Dito-7 variant). 
         ComputeExtremalPoints (input.Length);

         // 3. Create a fallback (AABB) that we will use if we can't find anything
         // better
         OBB aabb = CreateBox (Vector3f.XAxis, Vector3f.YAxis, Vector3f.ZAxis);
         OBB best = aabb; float bestScore = Area (best.Extent) * 4;

         // 4. From the 14 extremal points (mQ), compute an irregular di-tetrahedron
         // as outlined in the algorithm. This stores the resulting 5 points of into 
         // mR (points 0,1,2 form the triangle at the center, and 4 and 5 are the 
         // apices of the tetrahedra on either side of this base-triangle). 
         BuildDiTetrahedron ();

         // 5. The di-tetrahedron generates many possibilities - there are effectively
         // seven triangles now (including the shared base between the two tetrahedra). We use
         // these triangles as inputs to generate sets of orthonormal axes and try them out.
         // This routine ends up updating 'best' to contain the best OBB found (though this is
         // an 'incomplete' one based on only the 14 extremal points)
         RefineOBB (ref best, ref bestScore);

         // 6. Now that we have a set of axes (in best), update the projections to include
         // the complete set of points (so we have accurate bounds now computed and stored in 
         // the mE array)
         ComputeProjection (mP, input.Length, best.X, best.Y, best.Z, mE);

         // 7. Compute the best box based on the updated projections mE, and store it 
         // in OBB (unless the originally computed AABB is better, in which case we store that)
         best = CreateBox (best.X, best.Y, best.Z);
         OBB = (aabb.Area < best.Area) ? aabb : best;
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The computed OBB</summary>
   public readonly OBB OBB;

   // Implementation -----------------------------------------------------------
   // Implements Step 2 from the explanation above - computes the extremal points 
   // along the 7 axes listed in the code below. Since we are only getting the
   // extremal points (and not the actual extents), we can simplify the projection code.
   // In addition, the axes are selected so the projections are already quite simple - 
   // for example, 3 of the projections just select one ordinate of the point, and the
   // other four are of the form "f = p.X + p.Y - p.Z", for example.
   //
   // This uses the mE array as a temporary storage to do computations, and stores 
   // the final result (the 14 extremal points) in the array mQ. 
   void ComputeExtremalPoints (int count) {
      Point3f p0 = mP[0];
      Point3f* a = mP, q = mQ;
      int g0 = 0, g1 = 0, g2 = 0, g3 = 0, g4 = 0, g5 = 0, g6 = 0;
      int h0 = 0, h1 = 0, h2 = 0, h3 = 0, h4 = 0, h5 = 0, h6 = 0;

      float* E = mE;
      E[0] = E[7] = p0.X; E[1] = E[8] = p0.Y; E[2] = E[9] = p0.Z;
      E[3] = E[10] = p0.X + p0.Y + p0.Z; E[4] = E[11] = p0.X + p0.Y - p0.Z;
      E[5] = E[12] = p0.X - p0.Y + p0.Z; E[6] = E[13] = -p0.X + p0.Y + p0.Z;

      // 1. Compute extremal points wrt the 7 'known' axes: X, Y, Z, (1, 1, 1), (1, 1, -1), (1, -1, 1) and (-1, 1, 1)
      for (int i = 1; i < count; i++) {
         Point3f p = a[i];
         float f = p.X;          // (1,0,0) axis
         if (f < E[0]) (E[0], g0) = (f, i);
         else if (f > E[7]) (E[7], h0) = (f, i);

         f = p.Y;                // (0,1,0) axis
         if (f < E[1]) (E[1], g1) = (f, i);
         else if (f > E[8]) (E[8], h1) = (f, i);

         f = p.Z;                // (0,0,1) axis
         if (f < E[2]) (E[2], g2) = (f, i);
         else if (f > E[9]) (E[9], h2) = (f, i);

         f = p.X + p.Y + p.Z;    // (1,1,1) axis
         if (f < E[3]) (E[3], g3) = (f, i);
         else if (f > E[10]) (E[10], h3) = (f, i);

         f = p.X + p.Y - p.Z;    // (1,1,-1) axis
         if (f < E[4]) (E[4], g4) = (f, i);
         else if (f > E[11]) (E[11], h4) = (f, i);

         f = p.X - p.Y + p.Z;    // (1,-1,-1) axis
         if (f < E[5]) (E[5], g5) = (f, i);
         else if (f > E[12]) (E[12], h5) = (f, i);

         f = -p.X + p.Y + p.Z;    // (1,-1,1)
         if (f < E[6]) (E[6], g6) = (f, i);
         else if (f > E[13]) (E[13], h6) = (f, i);
      }

      q[0] = a[g0]; q[1] = a[g1]; q[2] = a[g2]; q[3] = a[g3]; q[4] = a[g4]; q[5] = a[g5]; q[6] = a[g6];
      q[7] = a[h0]; q[8] = a[h1]; q[9] = a[h2]; q[10] = a[h3]; q[11] = a[h4]; q[12] = a[h5]; q[13] = a[h6];
   }

   // Implements Step 4. Given the 14 extremal points (in mQ), this computes the 
   // di-tetrahedron and stores the 5 points in mR (0,1,2 are the base triangle and 
   // 4 and 5 are the apex points on either side of this base triangle)
   void BuildDiTetrahedron () {
      // First, compute the longest axis among the 7, and we then call 
      // these two points P0..P1 (these are finally saved as mR[0] and mR[1])
      int n = 0;
      Point3f* q = mQ;
      var dMax = q[0].DistToSq (q[7]);
      for (int i = 1; i < 7; i++) {
         var d = q[i].DistToSq (q[i + 7]);
         if (d > dMax) (dMax, n) = (d, i);
      }
      var P0 = mR[0] = q[n]; var P1 = mR[1] = q[n + 7];

      // Next, find the point furthest away from the axis line P0..P1 to find a
      // base triangle for the di-tetrahedra (P2, mR[2]). 
      dMax = double.MinValue;
      for (int i = 0; i < 14; i++) {
         var d = q[i].DistToLineSq (P0, P1);
         if (d > dMax) (dMax, n) = (d, i);
      }
      var P2 = mR[2] = q[n];

      // Finally, find points mR[3] and mR[4] that lie furthest away from the
      // plane defined by (P0,P1,P2). These form the two apices of the tetrahedra
      var w = (P1 - P0) * (P2 - P0);
      // Now find min-max along normal 'w'.
      var (min, max) = (float.MaxValue, float.MinValue);
      var (q0, q1) = (0, 0);
      for (int i = 0; i < 14; i++) {
         var p = q[i];
         var d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
         if (d < min) (min, q0) = (d, i);
         if (d > max) (max, q1) = (d, i);
      }
      mR[3] = q[q0]; mR[4] = q[q1];
   }

   // Implements step 5. We start with the 5 points making up the di-tetrahedron in 
   // mR. For each of the 7 triangles in the di-tetrahedron, we try to compute 3 different OBB
   // axes, by taking each edge of the triangle and the normal of the triangle as two of the
   // perpendicular axes (and using a cross product to compute the third). We test each of these
   // 21 possible OBB orientations to pick the one that generates the smallest surface area
   // (testing with only the 14 shortlisted points in mQ)
   void RefineOBB (ref OBB best, ref float bestScore) {
      Point3f* r = mR;
      fixed (int* pr = mRefine) {
         for (int n = 0; n < 21; n += 3) {
            var (a, b, c) = (pr[n], pr[n + 1], pr[n + 2]);
            var u = r[b] - r[a]; var v = r[c] - r[a];
            var w = u * v; if (w.IsZero) continue;
            u = u.Normalized (); w = w.Normalized ();
            v = w * u;

            // Iterate over all three triangle sides
            for (int pass = 0; pass < 3; pass++) {
               if (pass > 0) {
                  (a, b, c) = (b, c, a);
                  u = (r[b] - r[a]).Normalized ();
                  v = w * u;
               }

               var area = ComputeProjection (mQ, 14, in u, in v, in w, mE);
               if (area < bestScore) (best, bestScore) = (CreateBox (in u, in v, in w), area);
            }
         }
      }
   }
   static readonly int[] mRefine = [0, 1, 2, 0, 1, 3, 1, 2, 3, 2, 0, 3, 0, 1, 4, 1, 2, 4, 2, 0, 4];

   // Helpers ------------------------------------------------------------------
   // Computes the surface area of an OBB given the extent vector
   static float Area (in Vector3f extent) => extent.X * extent.Y + extent.Y * extent.Z + extent.Z * extent.X;

   // Computes the projections of a given set of points on 3 orthogonal axes u, v, w. 
   // Each projection is simply a dot product, and this routine updates E[0,1,2] to store
   // the minimal projections along u,v,w and E[7,8,9] to store the corresponding maximal
   // projections. It also returns the 'area' of the 
   static float ComputeProjection (Point3f* P, int count, in Vector3f u, in Vector3f v, in Vector3f w, float* E) {
      Point3f p = P[0];
      float u0 = p.X * u.X + p.Y * u.Y + p.Z * u.Z, u1 = u0;
      float v0 = p.X * v.X + p.Y * v.Y + p.Z * v.Z, v1 = v0;
      float w0 = p.X * w.X + p.Y * w.Y + p.Z * w.Z, w1 = w0;
      for (int j = 1; j < count; j++) {
         p = P[j];
         // Projection on u axis (Dot(p,axis))
         var d = p.X * u.X + p.Y * u.Y + p.Z * u.Z;
         if (d < u0) u0 = d; else if (d > u1) u1 = d;
         // Projection on v axis
         d = p.X * v.X + p.Y * v.Y + p.Z * v.Z;
         if (d < v0) v0 = d; else if (d > v1) v1 = d;
         // Projection on w axis
         d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
         if (d < w0) w0 = d; else if (d > w1) w1 = d;
      }
      E[0] = u0; E[1] = v0; E[2] = w0;
      E[7] = u1; E[8] = v1; E[9] = w1;
      float du = u1 - u0, dv = v1 - v0, dw = w1 - w0;
      return du * dv + dv * dw + dw * du;
   }

   // Given three orthogonal axes and the projection lengths along them, creates the respective OBB.
   OBB CreateBox (in Vector3f u, in Vector3f v, in Vector3f w) {
      var cen = u * ((mE[7] + mE[0]) * 0.5f) + v * ((mE[8] + mE[1]) * 0.5f) + w * ((mE[9] + mE[2]) * 0.5f);
      var ext = new Vector3f ((mE[7] - mE[0]) * 0.5f, (mE[8] - mE[1]) * 0.5f, (mE[9] - mE[2]) * 0.5f);
      return new (new (cen.X, cen.Y, cen.Z), u, v, w, ext);
   }

   // Private data -------------------------------------------------------------
   // Complete set of input points to compute the OBB for
   readonly Point3f* mP;
   // Subset of 14 points from P, forming the extremal points along the 7 selected
   // axis directions. Q[N] and Q[N+7] are the minimal and maximal projection points 
   // along axis N
   readonly Point3f* mQ;
   // Set of 5 points forming the di-tetrahedra. R[0],R[1],R[2] are the shared base
   // triangle of the two tetrahedra and R[3] and R[4] are the apexes (one lying on each
   // side of that triangle plane)
   readonly Point3f* mR;
   // A work buffer with 14 floats used to store the projections of points along 
   // the up-to 7 axes that we are testing with. This is also used in later stages
   // to store the projections along 3 orthonormal axes. 
   // E[N] and E[N+7] are the projections along the Nth axis. 
   readonly float* mE;
}
#endregion

#region struct OBBPCABuilder -----------------------------------------------------------------------
/// <summary>Builds an OBB using the very fast PCA algorithm</summary>
/// OBB building is 4~8 times faster than by using the OBBDitoBuilder above. However,
/// the OBBs that are constructed can be up to 4 times larger in terms of area. In most cases, the
/// OBBDitoBuilder should be used (that is, OBB.Build, rather than OBB.BuildFast). 
readonly struct OBBPCABuilder {
   // Constructor --------------------------------------------------------------
   /// <summary>Initialie OBBPCABuilder with a set of points</summary>
   public OBBPCABuilder (ReadOnlySpan<Point3f> pts) {
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
      OBB = new (cen, axis[0], axis[1], axis[2], ext);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The OBB we've computed</summary>
   public readonly OBB OBB;

   // Implementation -----------------------------------------------------------
   // Helper used during eigenvector computation
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
