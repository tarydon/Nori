namespace Nori;
using static Math;

public static partial class Tri {
   public static unsafe bool CollideMoller (float* p, int v0, int v1, int v2, int u0, int u1, int u2) {
      // ---------------------------------------------------
      // 0. Fetch the ordinates of the triangle
      // Get V0, V1, V2 as the vertices of the first triangle
      v0 *= 3; v1 *= 3; v2 *= 3;
      float V0x = p[v0], V0y = p[v0 + 1], V0z = p[v0 + 2];
      float V1x = p[v1], V1y = p[v1 + 1], V1z = p[v1 + 2];
      float V2x = p[v2], V2y = p[v2 + 1], V2z = p[v2 + 2];

      // Get U0, U1, U2 as the vertices of the second triangle
      u0 *= 3; u1 *= 3; u2 *= 3;
      float U0x = p[u0], U0y = p[u0 + 1], U0z = p[u0 + 2];
      float U1x = p[u1], U1y = p[u1 + 1], U1z = p[u1 + 2];
      float U2x = p[u2], U2y = p[u2 + 1], U2z = p[u2 + 2];

      // ---------------------------------------------------
      // 1. Check if the points U0,U1 and U2 are all on the same side of the plane formed by V0,V1 and V2
      // E1 = V1 - V0, E2 = V2 - V0, N1 = E1 * E2
      float E1x = V1x - V0x, E1y = V1y - V0y, E1z = V1z - V0z;
      float E2x = V2x - V0x, E2y = V2y - V0y, E2z = V2z - V0z;
      float N1x = E1y * E2z - E1z * E2y, N1y = E1z * E2x - E1x - E2z, N1z = E1x * E2y - E1y * E2x;
      double d1 = -(N1x * V0x + N1y * V0y + N1z * V0z);     // d1 = N1 . V0

      // Now, the plane equation 1 is N1.X + d1 = 0
      // Put U0,U1 and U2 into plane equation 1 to compute signed distances to the plane.
      const double EPSILON = 0.000001;
      double du0 = N1x * U0x + N1y * U0y + N1z * U0z + d1;
      double du1 = N1x * U1x + N1y * U1y + N1z * U1z + d1;
      double du2 = N1x * U2x + N1y * U2y + N1z * U2z + d1;

      // Coplanarity robustness check
      if (Abs (du0) < EPSILON) du0 = 0.0;
      if (Abs (du1) < EPSILON) du1 = 0.0;
      if (Abs (du2) < EPSILON) du2 = 0.0;

      // If all points U0,U1 and U2 are on the same side of this plane, and
      // none of the distances are 0, then no intersection occurs. 
      double du0du1 = du0 * du1, du0du2 = du0 * du2;
      if (du0du1 > 0.0 && du0du2 > 0.0) return false;

      // ----------------------------------------------------------
      // 2. Check if the points V0,V1 and V2 are all on the same side of the plane formed by U0,U1 and U2.
      // E1 = U1 - U0, E2 = U2 - U0, N2 = E1 * E2
      E1x = U1x - U0x; E1y = U1y - U0y; E1z = U1z - U0z;
      E2x = U2x - U0x; E2y = U2y - U0y; E2z = U2z - U0z;
      float N2x = E1y * E2z - E1z * E2y, N2y = E1z * E2x - E1x - E2z, N2z = E1x * E2y - E1y * E2x;
      double d2 = -(N2x * U0x + N2y * U0y + N2z * U0z);  // d2 = -N2 . U0

      // Now the plane equation 2 is N2.X + d2 = 0
      // Put V0,V1 and V2 into the plane equation to compute signed distances to the plane
      double dv0 = N2x * V0x + N2y * V0y + N2z * V0z + d2;
      double dv1 = N2x * V1x + N2y * V1y + N2z * V1z + d2;
      double dv2 = N2x * V2x + N2y * V2y + N2z * V2z + d2;

      // Coplanarity robustness check
      if (Abs (dv0) < EPSILON) dv0 = 0.0;
      if (Abs (dv1) < EPSILON) dv1 = 0.0;
      if (Abs (dv2) < EPSILON) dv2 = 0.0;

      // If all the points V0,V1 and V2 are on the same side of this plane,
      // and none of the distances are 0, then no intersection occurs.
      double dv0dv1 = dv0 * dv1, dv0dv2 = dv0 * dv2;
      if (dv0dv1 > 0.0 && dv0dv2 > 0.0) return false;

      // ----------------------------------------------------------
      // 3. Compute the direction of the intersection line
      float Dx = N1y * N2z - N1z * N2y, Dy = N1z * N2x - N1x - N2z, Dz = N1x * N2y - N1y * N2x;

      // Compute the index of the largest component of D
      double max = Math.Abs (Dx); int index = 0;
      double bb = Math.Abs (Dx); if (bb > max) { max = bb; index = 1; }
      double cc = Math.Abs (Dz); if (cc > max) { index = 2; }

      // This is the simplified projection onto L
      float vp0, vp1, vp2, up0, up1, up2;
      switch (index) {
         case 0: vp0 = V0x; vp1 = V1x; vp2 = V2x; up0 = U0x; up1 = U1x; up2 = U2x; break;
         case 1: vp0 = V0y; vp1 = V1y; vp2 = V2y; up0 = U0y; up1 = U1y; up2 = U2y; break;
         default: vp0 = V0z; vp1 = V1z; vp2 = V2z; up0 = U0z; up1 = U1z; up2 = U2z; break;
      }

      // ----------------------------------------------------------
      // 4. Now that we have the intersection line, compute the interval for the first triangle
      double a, b, c, x0, x1;
      if (dv0dv1 > 0.0f) {
         // dv0 and dv1 are on one side, and dv2 on the other side of the plane
         a = vp2; b = (vp0 - vp2) * dv2; c = (vp1 - vp2) * dv2;
         x0 = dv2 - dv0; x1 = dv2 - dv1;
      } else if (dv0dv2 > 0.0f) {
         // dv0 and dv2 are on one side, and dv1 on the other side of the plane
         a = vp1; b = (vp0 - vp1) * dv1; c = (vp2 - vp1) * dv1;
         x0 = dv1 - dv0; x1 = dv1 - dv2;
      } else if (dv1 * dv2 > 0.0f || dv0 != 0.0f) {
         // dv1 and dv2 are on one side, and dv0 is on the other side of the plane
         a = vp0; b = (vp1 - vp0) * dv0; c = (vp2 - vp0) * dv0;
         x0 = dv0 - dv1; x1 = dv0 - dv2;
      } else if (dv1 != 0.0f) {
         a = vp1; b = (vp0 - vp1) * dv1; c = (vp2 - vp1) * dv1;
         x0 = dv1 - dv0; x1 = dv1 - dv2;
      } else if (dv2 != 0.0f) {
         a = vp2; b = (vp0 - vp2) * dv2; c = (vp1 - vp2) * dv2;
         x0 = dv2 - dv0; x1 = dv2 - dv1;
      } else {
         int k0 = 0, k1 = 1;     // Assume we're going to use XY plane for projecting
         double ax = Abs (N1x), ay = Abs (N1y), az = Abs (N1z);
         if (ax >= ay && ax >= az) { k0 = 1; k1 = 2; }         // Use the YZ plane
         else if (ay >= ax && ay >= az) { k0 = 0; k1 = 2; }    // Use the XZ plane
         return CoplanarTriTri (p, v0, v1, v2, u0, u1, u2, k0, k1);
      }

      // ----------------------------------------------------------
      // 5. Compute the intervals for the second triangle
      double d, e, f, y0, y1;
      if (du0du1 > 0.0f) {
         // du0 and du1 are on one side, and du2 on the other side of the plane
         d = up2; e = (up0 - up2) * du2; f = (up1 - up2) * du2;
         y0 = du2 - du0; y1 = du2 - du1;
      } else if (du0du2 > 0.0f) {
         // du0 and du2 are on one side, and du1 on the other side of the plane
         d = up1; e = (up0 - up1) * du1; f = (up2 - up1) * du1;
         y0 = du1 - du0; y1 = du1 - du2;
      } else if (du1 * du2 > 0.0f || du0 != 0.0f) {
         // du1 and du2 are on one side, and du0 is on the other side of the plane
         d = up0; e = (up1 - up0) * du0; f = (up2 - up0) * du0;
         y0 = du0 - du1; y1 = du0 - du2;
      } else if (du1 != 0.0f) {
         d = up1; e = (up0 - up1) * du1; f = (up2 - up1) * du1;
         y0 = du1 - du0; y1 = du1 - du2;
      } else if (du2 != 0.0f) {
         d = up2; e = (up0 - up2) * du2; f = (up1 - up2) * du2;
         y0 = du2 - du0; y1 = du2 - du1;
      } else {
         int k0 = 0, k1 = 1;     // Assume we're going to use XY plane for projecting
         double ax = Abs (N1x), ay = Abs (N1y), az = Abs (N1z);
         if (ax >= ay && ax >= az) { k0 = 1; k1 = 2; }         // Use the YZ plane
         else if (ay >= ax && ay >= az) { k0 = 0; k1 = 2; }    // Use the XZ plane
         return CoplanarTriTri (p, v0, v1, v2, u0, u1, u2, k0, k1);
      }

      // ------------------------------------------------
      // 6. Check if the intervals overlap
      double xx = x0 * x1, yy = y0 * y1, xxyy = xx * yy;
      double tmp = a * xxyy;
      double isect10 = tmp + b * x1 * yy, isect11 = tmp + c * x0 * yy;

      tmp = d * xxyy;
      double isect20 = tmp + e * xx * y1, isect21 = tmp + f * xx * y0;
      if (isect10 > isect11) (isect10, isect11) = (isect11, isect10);
      if (isect20 > isect21) (isect20, isect21) = (isect21, isect20);
      if (isect11 < isect20 + 1e-10 || isect21 < isect10 + 1e-10) return false;
      return true;
   }

   // An internal routine used to test if two co-planar triangles are intersecting.
   // In this routine, reduce the problem to a 2-dimensional case by taking the dominant direction
   // and looking in that direction. For example, suppose the Z coordinate of the normal is the longest,
   // we can just take X and Y and work in 2-D.
   // The values v0..v2 and u0..u2 point to the actual indices in the float array where the
   // triangles start (in other words, they are not triangle indices, they are component indices). 
   // By adding k0 and k1 to these, we select either XY or YZ or XZ components of these triangles
   // to work with in 2D 
   static unsafe bool CoplanarTriTri (float * p, int v0, int v1, int v2, int u0, int u1, int u2, int k0, int k1) {
      // Test all edges of triangle 1 against edges of triangle 2. This test is essentially
      // a 2-d test since we are using only the components k0 and k1 of each of the points
      if (EdgeAgainstTriEdges (p, v0, v1, u0, u1, u2, k0, k1)) return true;
      if (EdgeAgainstTriEdges (p, v1, v2, u0, u1, u2, k0, k1)) return true;
      if (EdgeAgainstTriEdges (p, v2, v0, u0, u1, u2, k0, k1)) return true;

      // Finally, a test if tri1 is totally contained in tri2 or vice versa
      if (PointInTri (p, v0, u0, u1, u2, k0, k1)) return true;
      return PointInTri (p, u0, v0, v1, v2, k0, k1);
   }

   // An internal routine used by CoplanarTriTri
   // This checks the edge V0-V1 against the 3 edges of the triangles U0,U1 and U2
   static unsafe bool EdgeAgainstTriEdges (float * p, int v0, int v1, int u0, int u1, int u2, int k0, int k1) {
      float Ax = p[v1 + k0] - p[v0 + k0];
      float Ay = p[v1 + k1] - p[v0 + k1];
      return EdgeEdgeTest (p, v0, u0, u1, Ax, Ay, k0, k1)
          || EdgeEdgeTest (p, v0, u1, u2, Ax, Ay, k0, k1)
          || EdgeEdgeTest (p, v0, u2, u0, Ax, Ay, k0, k1);
   }

   // An internal routine used by CoplanarTriTri
   static unsafe bool EdgeEdgeTest (float *p, int v0, int u0, int u1, float Ax, float Ay, int k0, int k1) {
      float Bx = p[u0 + k0] - p[u1 + k0], By = p[u0 + k1] - p[u1 + k1];
      float Cx = p[v0 + k0] - p[u0 + k0], Cy = p[v0 + k1] - p[u0 + k1];
      float f = Ay * Bx - Ax * By, d = By * Cx - Bx * Cy;
      if ((f > 0.0f && d >= 0.0f && d <= f) || (f < 0.0f && d <= 0.0f && d >= f)) {
         float e = Ax * Cy - Ay * Cx;
         if (f > 0.0f) {
            if (e >= 0.0f && e <= f) return true;
         } else {
            if (e <= 0.0f && e >= f) return true;
         }
      }
      return false;
   }

   // An internal routine used by CoplanarTriTri
   // Checks if the vertex in V0 is contained within the triangle U0-U1-U2
   static unsafe bool PointInTri (float * p, int v0, int u0, int u1, int u2, int k0, int k1) {
      float U1y = p[u1 + k1], U0y = p[u0 + k1], U1x = p[u1 + k0], U0x = p[u0 + k0];
      float V0x = p[v0 + k0], V0y = p[v0 + k1], V2y = p[u2 + k1], V2x = p[u2 + k0];

      float a = U1y - U0y, b = U0x - U1x;
      float c = -a * U0x - b * U0y;
      float d0 = a * V0x + b * V0y + c;

      a = V2y - U1y; b = U1x - V2x;
      c = -a * U1x - b * U1y;
      float d1 = a * V0x + b * V0y + c;

      a = U0y - V2y; b = V2x - U0x;
      c = -a * V2x - b * V2y;
      float d2 = a * V0x + b * V0y + c;

      return d0 * d1 > 0.0f && d0 * d2 > 0.0f;
   }
}