namespace Nori;

public static partial class Tri {
   public unsafe static bool TriGD (Point3f* p, in CTri t1, in CTri t2) {
      // Step 1. Check if triangle 1 is completely on one side of triangle 2's plane
      int a1 = t1.A, b1 = t1.B, c1 = t1.C, a2 = t2.A, b2 = t2.B, c2 = t2.C;
      Vector3f N2 = t2.N; float d2 = t2.D;
      int sa1 = Sign (Dot (p[a1], N2) + d2);
      int sb1 = Sign (Dot (p[b1], N2) + d2);
      int sc1 = Sign (Dot (p[c1], N2) + d2);

      // If all the signs are equal, triangle 1 is on one side of triangle 2, OR
      // they are coplanar.
      if (sa1 == sb1 && sa1 == sc1) {
         if (sa1 == 0) goto Coplanar;
         return false;     // Triangle 1 completely on one side of plane 2
      }

      // Step 2. Check if triangle 2 is completely on one side of triangle 1
      Vector3f N1 = t1.N; float d1 = t1.D;
      int sa2 = Sign (Dot (p[a2], N1) + d1);
      int sb2 = Sign (Dot (p[b2], N1) + d1);
      int sc2 = Sign (Dot (p[c2], N1) + d1);
      if (sa2 == sb2 && sa2 == sc2) {
         if (sa2 == 0) goto Coplanar;     // Defensive check for floating-point tolerance
         return false;
      }

      // Step 3. Convert triangles by reordering vertices in the 'cannonical' form. In this form, vertex 'a'
      // is the isolated vertex on one side of the plane, and it is in the positive half-space.
      // 3a. Reorder triangles such that 'a' is the isolated vertex
      ReorderTriangle (ref sa1, ref sb1, ref sc1, ref a1, ref b1, ref c1);
      ReorderTriangle (ref sa2, ref sb2, ref sc2, ref a2, ref b2, ref c2);

      // 3b. Ensure 'a' is in the positive half of the 'other' triangle by
      // swapping the other 'b' and 'c' if necessary
      if (sa1 < 0 || (sa1 == 0 && sb1 > 0)) (b2, c2) = (c2, b2);
      if (sa2 < 0 || (sa2 == 0 && sb2 > 0)) (b1, c1) = (c1, b1);

      // Step 4. Final overlap test on the intersection line
      // After the cannonical reordering, this checks if the 'min' of intersection interval
      // of triangle 1 is less than or equal to the 'max' of triangle 2 and vice-versa.
      return Side (a1, b1, a2, b2) <= 0 && Side (a1, c1, c2, a2) <= 0;

   Coplanar:
      // Now perform 2D triangle overlap tests by projecting 3D triangle points into 2D space.
      // Instead of mapping points directly onto the triangle’s plane, we project them onto
      // a principal plane that best aligns with the triangle. This approach simplifies
      // the transformation and avoids several costly arithmetic operations.
      var P2 = XY;
      if (t1.K == 0b_0110) P2 = YZ;
      else if (t1.K == 0b_0010) P2 = XZ;
      Point2f* pt2 = stackalloc Point2f[] { P2 (p[a1]), P2 (p[b1]), P2 (p[c1]), P2 (p[a2]), P2 (p[b2]), P2 (p[c2]) };
      return TriTri2Df (pt2);

      // Helpers ...........................................
      static Point2f XY (in Point3f p) => new (p.X, p.Y); // Project to XY plane
      static Point2f YZ (in Point3f p) => new (p.Y, p.Z); // Project to YZ plane
      static Point2f XZ (in Point3f p) => new (p.X, p.Z); // Project to XZ plane

      // Gets the orientation of point 'd' with respect to the plane defined by triangle 'abc'
      // +1 = d is on the positive side of the plane, -1 = d is on the negative side, 0 = d is on the plane
      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      float Side (int a, int b, int c, int d) {
         ref var pa = ref p[a]; ref var pb = ref p[b];
         ref var pc = ref p[c]; ref var pd = ref p[d];
         return Dot (pd - pa, (pb - pa) * (pc - pa));
      }

      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      static void ReorderTriangle (ref int sa, ref int sb, ref int sc, ref int a, ref int b, ref int c) {
         // Rotate until 'b' and 'c' are on the same side and 'a' is the isolated vertex
         // on the other side of the plane (or on the plane).
         if (sa == sb) {
            (sa, sb, sc, a, b, c) = (sc, sa, sb, c, a, b); // Rotate right
         } else if (sa == sc) {
            (sa, sb, sc, a, b, c) = (sb, sc, sa, b, c, a); // Rotate left
         } else if (sb != sc) {
            if (sb == 0) (sa, sb, sc, a, b, c) = (sc, sa, sb, c, a, b); // Rotate right
            else (sa, sb, sc, a, b, c) = (sb, sc, sa, b, c, a);         // Rotate left
         }
      }
   }
}
