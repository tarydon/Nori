namespace Nori;
using static Math;

/// <summary>
/// Represents a 'collision triangle'
/// </summary>
public readonly struct CTri {
   /// <summary>
   /// Construct a CTri given a span of floats and indices into that for the 3 corners
   /// </summary>
   public CTri (Span<float> pf, int a, int b, int c) {
      A = a; B = b; C = c;

      // Fetch the three vertices
      Point3f pa = new (pf[A], pf[A + 1], pf[A + 2]);
      Point3f pb = new (pf[B], pf[B + 1], pf[B + 2]);
      Point3f pc = new (pf[C], pf[C + 1], pf[C + 2]);

      // Compute the edges AB and AC, then the normal and the intercept
      Vector3f e1 = pb - pa, e2 = pc - pa;
      N = e1 * e2;
      D = -(N.X * pa.X + N.Y * pa.Y + N.Z * pa.Z);

      K = 0b_0001;  // Assume we're using Xy plane for projecting (00 01)
      float ax = MathF.Abs (N.X), ay = MathF.Abs (N.Y), az = MathF.Abs (N.Z);
      if (ax >= ay && ax >= az) K = 0b_0110;         // Use YZ plane (01 10)
      else if (ay >= ax && ay >= az) K = 0b_0010;    // Use XZ plane (00 10)
   }

   /// <summary>
   /// Indices of the ordinates in the float-array
   /// </summary>
   /// Each index points to the X ordinate of a point, and the Y and Z
   /// ordinates are stored in successive floats
   public readonly int A, B, C;
   /// <summary>
   /// Normal vector of the plane
   /// </summary>
   public readonly Vector3f N;
   /// <summary>
   /// Intercept used for distance checks
   /// </summary>
   public readonly float D;
   /// <summary>
   /// Encoding of which 2 axes to use for a 2D projection
   /// </summary>
   /// Lowest 2 bits encode a K1 value, and next 2 bits encode a K0 value.
   /// These values are 0,1,2 for X,Y,Z axes. So, if K0=0, and K1=2 then we
   /// are using the X-Z plane for projection
   public readonly int K;
}

public static partial class Tri {
   public static unsafe bool CollideMollerFast (float* p, ref CTri ta, ref CTri tb) {
      // ---------------------------------------------------
      // 0. Fetch the ordinates of the triangle
      // Get V0, V1, V2 as the vertices of the first triangle
      int v0 = ta.A, v1 = ta.B, v2 = ta.C;
      float V0x = p[v0], V0y = p[v0 + 1], V0z = p[v0 + 2];
      float V1x = p[v1], V1y = p[v1 + 1], V1z = p[v1 + 2];
      float V2x = p[v2], V2y = p[v2 + 1], V2z = p[v2 + 2];

      // Get U0, U1, U2 as the vertices of the second triangle
      int u0 = tb.A, u1 = tb.B, u2 = tb.C;
      float U0x = p[u0], U0y = p[u0 + 1], U0z = p[u0 + 2];
      float U1x = p[u1], U1y = p[u1 + 1], U1z = p[u1 + 2];
      float U2x = p[u2], U2y = p[u2 + 1], U2z = p[u2 + 2];

      // ---------------------------------------------------
      // 1. Check if the points U0,U1 and U2 are all on the same side of the plane formed by V0,V1 and V2
      // Now, the plane equation 1 is N1.X + d1 = 0
      // Put U0,U1 and U2 into plane equation 1 to compute signed distances to the plane.
      const double EPSILON = 0.000001;
      Vector3f N1 = ta.N; float d1 = ta.D;
      double du0 = N1.X * U0x + N1.Y * U0y + N1.Z * U0z + d1;
      double du1 = N1.X * U1x + N1.Y * U1y + N1.Z * U1z + d1;
      double du2 = N1.X * U2x + N1.Y * U2y + N1.Z * U2z + d1;

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
      Vector3f N2 = tb.N; float d2 = tb.D;
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
         int k0 = ta.K >> 2, k1 = ta.K & 3;
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
         int k0 = tb.K >> 2, k1 = tb.K & 3;
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
}