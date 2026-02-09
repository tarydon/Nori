namespace Nori;
using static Math;

public static partial class Tri {
   public static unsafe bool Collide (Point3f* ppa, ref CTri ta, Point3f * ppb, ref CTri tb, Matrix3 xfm) {
      // ---------------------------------------------------
      // 0. Fetch the ordinates of the triangle
      // Get V0, V1, V2 as the vertices of the first triangle
      float* pa = (float*)ppa;
      int v0 = ta.A * 3, v1 = ta.B * 3, v2 = ta.C * 3;
      float V0x = pa[v0], V0y = pa[v0 + 1], V0z = pa[v0 + 2];
      float V1x = pa[v1], V1y = pa[v1 + 1], V1z = pa[v1 + 2];
      float V2x = pa[v2], V2y = pa[v2 + 1], V2z = pa[v2 + 2];

      // Get U0, U1, U2 as the vertices of the second triangle
      // int u0 = tb.A * 3, u1 = tb.B * 3, u2 = tb.C * 3;
      Point3f U0 = ppb[tb.A] * xfm, U1 = ppb[tb.B] * xfm, U2 = ppb[tb.C] * xfm;

      // ---------------------------------------------------
      // 1. Check if the points U0,U1 and U2 are all on the same side of the plane formed by V0,V1 and V2
      // Now, the plane equation 1 is N1.X + d1 = 0
      // Put U0,U1 and U2 into plane equation 1 to compute signed distances to the plane.
      const double EPSILON = 0.000001;
      Vector3f N1 = ta.N; float d1 = ta.D;
      double du0 = N1.X * U0.X + N1.Y * U0.Y + N1.Z * U0.Z + d1;
      double du1 = N1.X * U1.X + N1.Y * U1.Y + N1.Z * U1.Z + d1;
      double du2 = N1.X * U2.X + N1.Y * U2.Y + N1.Z * U2.Z + d1;

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
      // Now the plane equation 2 is N2.X + d2 = 0
      // Put V0,V1 and V2 into the plane equation to compute signed distances to the plane
      Vector3f E1 = U1 - U0, E2 = U2 - U0, N2 = E1 * E2;
      float d2 = -(N2.X * U0.X + N2.Y * U0.Y + N2.Z * U0.Z);

      double dv0 = N2.X * V0x + N2.Y * V0y + N2.Z * V0z + d2;
      double dv1 = N2.X * V1x + N2.Y * V1y + N2.Z * V1z + d2;
      double dv2 = N2.X * V2x + N2.Y * V2y + N2.Z * V2z + d2;

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
      float Dx = N1.Y * N2.Z - N1.Z * N2.Y, Dy = N1.Z * N2.X - N1.X * N2.Z, Dz = N1.X * N2.Y - N1.Y * N2.X;

      // Compute the index of the largest component of D
      double max = Math.Abs (Dx); int index = 0;
      double bb = Math.Abs (Dy); if (bb > max) { max = bb; index = 1; }
      double cc = Math.Abs (Dz); if (cc > max) { index = 2; }

      // This is the simplified projection onto L
      float vp0, vp1, vp2, up0, up1, up2;
      switch (index) {
         case 0: vp0 = V0x; vp1 = V1x; vp2 = V2x; up0 = U0.X; up1 = U1.X; up2 = U2.X; break;
         case 1: vp0 = V0y; vp1 = V1y; vp2 = V2y; up0 = U0.Y; up1 = U1.Y; up2 = U2.Y; break;
         default: vp0 = V0z; vp1 = V1z; vp2 = V2z; up0 = U0.Z; up1 = U1.Z; up2 = U2.Z; break;
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
         goto Coplanar;

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
         goto Coplanar;

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

      Coplanar:
      // Fetch 2 triangles into 2D space (using only x and z components)
      float U0x, U0z, U1x, U1z, U2x, U2z;
      if (ta.K == 0b_0110) {
         V0x = V0y; V1x = V1y; V2x = V2y;
         U0x = U0.Y; U1x = U1.Y; U2x = U2.Y;
         U0z = U0.Z; U1z = U1.Z; U2z = U2.Z;
      } else if (ta.K == 0b_0001) {
         V0z = V0y; V1z = V1y; V2z = V2y;
         U0x = U0.X; U1x = U1.X; U2x = U2.X;
         U0z = U0.Y; U1z = U1.Y; U2z = U2.Y;
      } else {
         U0x = U0.X; U1x = U1.X; U2x = U2.X;
         U0z = U0.Z; U1z = U1.Z; U2z = U2.Z;
      }

      // Use separating axis theorem and test against 6 possible separation axes,
      // each axis is the perpendicular to one of the edges from both of the triangles
      for (int i = 0; i < 6; i++) {
         float X, Z;
         switch (i) {
            case 0: X = V1z - V0z; Z = V0x - V1x; break;
            case 1: X = V2z - V1z; Z = V1x - V2x; break;
            case 2: X = V0z - V2z; Z = V2x - V0x; break;
            case 3: X = U1z - U0z; Z = U0x - U1x; break;
            case 4: X = U2z - U1z; Z = U1x - U2x; break;
            default: X = U0z - U2z; Z = U2x - U0x; break;
         }

         float s0 = X * V0x + Z * V0z, s1 = X * V1x + Z * V1z, s2 = X * V2x + Z * V2z;
         float t0 = X * U0x + Z * U0z, t1 = X * U1x + Z * U1z, t2 = X * U2x + Z * U2z;

         // We want s0..s2 to be the min & max in s, and t0..t2 likewise in t
         if (s0 > s1) (s0, s1) = (s1, s0);   // Get s0,s1 in order
         if (s2 < s0) (s0, s2) = (s2, s1);   // Case: s2 is the smallest (s1 is the biggest)
         else if (s2 < s1) s2 = s1;          // Case: s1 is the largest (s0 is the smallest)

         if (t0 > t1) (t0, t1) = (t1, t0);
         if (t2 < t0) (t0, t2) = (t2, t1);
         else if (t2 < t1) t2 = t1;

         if (t2 < s0 || s2 < t0) return false;
      }
      return true;
   }
}
