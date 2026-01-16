namespace Nori;
using static Math;

public static partial class Tri {
   // Interface routine to do collision check between a Triangle and a Triangle.
   //    V0, V1, V2 = Vertices of the first triangle
   //    U0, U1, U2 = Vertices of the second triangle
   // Returns True if the two triangles intersect (or even if they touch), and false if they do not.
   public static unsafe bool CollideFlux (Point3f V0, Point3f V1, Point3f V2, Point3f U0, Point3f U1, Point3f U2) {
      // ----------------------------------------------------------
      // 1. Check if the points U0,U1 and U2 are all on the same side of the plane formed by V0,V1 and V2
      // Compute plane equation of triangle V0,V1,V2
      Vector3f E1 = V1 - V0, E2 = V2 - V0;
      Vector3f N1 = E1 * E2;
      double d1 = -(N1.X * V0.X + N1.Y * V0.Y + N1.Z * V0.Z);         // d1 = -N1 | V0

      // Now, the plane equation 1 is N1.X + d1 = 0
      // Put U0,U1 and U2 into plane equation 1 to compute signed distances to the plane.
      const double EPSILON = 0.000001;
      double du0 = N1.X * U0.X + N1.Y * U0.Y + N1.Z * U0.Z + d1;
      double du1 = N1.X * U1.X + N1.Y * U1.Y + N1.Z * U1.Z + d1;
      double du2 = N1.X * U2.X + N1.Y * U2.Y + N1.Z * U2.Z + d1;

      // Coplanarity robustness check
      if (Math.Abs (du0) < EPSILON) du0 = 0.0;
      if (Math.Abs (du1) < EPSILON) du1 = 0.0;
      if (Math.Abs (du2) < EPSILON) du2 = 0.0;

      // If all points U0,U1 and U2 are on the same side of this plane, and
      // none of the distances are 0, then no intersection occurs. 
      double du0du1 = du0 * du1, du0du2 = du0 * du2;
      if (du0du1 > 0.0 && du0du2 > 0.0) return false;

      // ----------------------------------------------------------
      // 2. Check if the points V0,V1 and V2 are all on the same side of the plane formed by U0,U1 and U2.
      // Compute the plane of the triangle U0,U1,U2
      E1 = U1 - U0; E2 = U2 - U0;
      Vector3f N2 = E1 * E2;
      double d2 = -(N2.X * U0.X + N2.Y * U0.Y + N2.Z * U0.Z);         // d1 = -N1 | V0

      // Now the plane equation 2 is N2.X + d2 = 0
      // Put V0,V1 and V2 into the plane equation to compute signed distances to the plane
      double dv0 = N2.X * V0.X + N2.Y * V0.Y + N2.Z * V0.Z + d2;
      double dv1 = N2.X * V1.X + N2.Y * V1.Y + N2.Z * V1.Z + d2;
      double dv2 = N2.X * V2.X + N2.Y * V2.Y + N2.Z * V2.Z + d2;

      // Coplanarity robustness check
      if (Math.Abs (dv0) < EPSILON) dv0 = 0.0;
      if (Math.Abs (dv1) < EPSILON) dv1 = 0.0;
      if (Math.Abs (dv2) < EPSILON) dv2 = 0.0;

      // If all the points V0,V1 and V2 are on the same side of this plane,
      // and none of the distances are 0, then no intersection occurs.
      double dv0dv1 = dv0 * dv1, dv0dv2 = dv0 * dv2;
      if (dv0dv1 > 0.0 && dv0dv2 > 0.0) return false;

      // ----------------------------------------------------------
      // 3. Compute the direction of the intersection line
      Vector3f D = N1 * N2;

      // Compute the index of the largest component of D
      double max = Math.Abs (D.X); int index = 0;
      double bb = Math.Abs (D.Y); if (bb > max) { max = bb; index = 1; }
      double cc = Math.Abs (D.Z); if (cc > max) { index = 2; }

      // This is the simplified projection onto L
      float vp0, vp1, vp2, up0, up1, up2;
      switch (index) {
         case 0: vp0 = V0.X; vp1 = V1.X; vp2 = V2.X; up0 = U0.X; up1 = U1.X; up2 = U2.X; break;
         case 1: vp0 = V0.Y; vp1 = V1.Y; vp2 = V2.Y; up0 = U0.Y; up1 = U1.Y; up2 = U2.Y; break;
         default: vp0 = V0.Z; vp1 = V1.Z; vp2 = V2.Z; up0 = U0.Z; up1 = U1.Z; up2 = U2.Z; break;
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
      } else
         return CoplanarTriTri (ref N1, &V0.X, &V1.X, &V2.X, &U0.X, &U1.X, &U2.X);

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
      } else
         return CoplanarTriTri (ref N1, &V0.X, &V1.X, &V2.X, &U0.X, &U1.X, &U2.X);

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
   // we can just take X and Y and work in 2-D. This routine computes and sets the member variables
   // k0 and k1 which act as the axis indices for projecting the triangles to 2-D. For example, suppose
   // k0=0 and k1=2, that means we are going to consider only the X and Z components of these triangles.      
   static unsafe bool CoplanarTriTri (ref Vector3f n, float* v0, float* v1, float* v2, float* u0, float* u1, float* u2) {
      int k0, k1;
      double A0 = Math.Abs (n.X), A1 = Math.Abs (n.Y), A2 = Math.Abs (n.Z);
      if (A0 > A1) {
         if (A0 > A2) { k0 = 1; k1 = 2; }    // A0 is the greatest
         else { k0 = 0; k1 = 1; }            // A2 is the greatest
      } else {
         if (A2 > A1) { k0 = 0; k1 = 1; }    // A2 is the greatest
         else { k0 = 0; k1 = 2; }            // A1 is the greatest
      }

      // Test all edges of triangle 1 against edges of triangle 2. This test is essentially
      // a 2-d test since we are using only the components k0 and k1 of each of the points
      if (EdgeAgainstTriEdges (v0, v1, u0, u1, u2, k0, k1)) return true;
      if (EdgeAgainstTriEdges (v1, v2, u0, u1, u2, k0, k1)) return true;
      if (EdgeAgainstTriEdges (v2, v0, u0, u1, u2, k0, k1)) return true;

      // Finally, a test if tri1 is totally contained in tri2 or vice versa
      if (PointInTri (v0, u0, u1, u2, k0, k1)) return true;
      return PointInTri (u0, v0, v1, v2, k0, k1);
   }

   // An internal routine used by CoplanarTriTri
   // This checks the edge V0-V1 against the 3 edges of the triangles U0,U1 and U2
   static unsafe bool EdgeAgainstTriEdges (float* V0, float* V1, float* U0, float* U1, float* U2, int k0, int k1) {
      float Ax = V1[k0] - V0[k0];
      float Ay = V1[k1] - V0[k1];
      return EdgeEdgeTest (V0, U0, U1, Ax, Ay, k0, k1)
         || EdgeEdgeTest (V0, U1, U2, Ax, Ay, k0, k1)
         || EdgeEdgeTest (V0, U2, U0, Ax, Ay, k0, k1);
   }

   // An internal routine used by CoplanarTriTri
   static unsafe bool EdgeEdgeTest (float* V0, float* U0, float* U1, float Ax, float Ay, int k0, int k1) {
      float Bx = U0[k0] - U1[k0], By = U0[k1] - U1[k1];
      float Cx = V0[k0] - U0[k0], Cy = V0[k1] - U0[k1];
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
   static unsafe bool PointInTri (float* V0, float* U0, float* U1, float* U2, int k0, int k1) {
      /* is T1 completly inside T2? */
      /* check if V0 is inside tri(U0,U1,U2) */
      float a = U1[k1] - U0[k1];
      float b = -(U1[k0] - U0[k0]);
      float c = -a * U0[k0] - b * U0[k1];
      float d0 = a * V0[k0] + b * V0[k1] + c;

      a = U2[k1] - U1[k1];
      b = -(U2[k0] - U1[k0]);
      c = -a * U1[k0] - b * U1[k1];
      float d1 = a * V0[k0] + b * V0[k1] + c;

      a = U0[k1] - U2[k1];
      b = -(U0[k0] - U2[k0]);
      c = -a * U2[k0] - b * U2[k1];
      float d2 = a * V0[k0] + b * V0[k1] + c;
      return d0 * d1 > 0.0f && d0 * d2 > 0.0f;
   }
}