namespace Nori;

public static partial class Tri {
   // Triangle-Triangle intersection test routine based on the Guigue-Devillers algorithm.
   public static unsafe bool CollideGD (float* p, ref CTri ta, ref CTri tb) {
      // v0, v1, v2 = Vertices of the first triangle T1 in the plane P1
      var (v0, v1, v2) = (FetchVertex (ta.A), FetchVertex (ta.B), FetchVertex (ta.C));
      // u0, u1, u2 = Vertices of the second triangle T2 in the plane P2
      var (u0, u1, u2) = (FetchVertex (tb.A), FetchVertex (tb.B), FetchVertex (tb.C));

      // An extra AABB intersection test for early check out. (This step is not suggested in the algorithm)
      //if (((new Bound3 () + v0 + v1 + v2) * (new Bound3 () + u0 + u1 + u2)).IsEmpty) return false;

      // --------------------------------------------------------------------------------
      // 1. Check if the vertices of T1 are all on the same side of the plane P2
      var n2 = tb.N; // normal of P2
      var d0 = Side (n2, v0 - u0);
      var d1 = Side (n2, v1 - u0);
      var d2 = Side (n2, v2 - u0);
      if (d0 == 0 && d1 == 0 && d2 == 0) return CoplanarTriTriGD (p, ref ta, ref tb);
      else if (d0 == d1 && d1 == d2) return false;

      // --------------------------------------------------------------------------------
      // 2. Check if the vertices of T2 are all on the same side of the plane P1
      var n1 = ta.N; // normal of P1
      var d3 = Side (n1, u0 - v0);
      var d4 = Side (n1, u1 - v0);
      var d5 = Side (n1, u2 - v0);
      if (d3 == 0 && d4 == 0 && d5 == 0) return CoplanarTriTriGD (p, ref ta, ref tb); // is optional?
      if (d3 == d4 && d4 == d5) return false;

      // --------------------------------------------------------------------------------
      // 3. Move the isolated vertex to the first index and ensure that lies in positive half space
      // At this point, triangle T1 (and T2) has exactly one vertex lying alone on one side of P2 (and P1).
      // We choose that one isolated vertex and fix its index (as 0) and lying space (as +ve side). This step
      // ensures that the two edges connected to V0 (or U0) are the ones that intersect the line of intersection L.
      FixIsolatedVertex (ref v0, ref v1, ref v2, d0, d1, d2); // For T1
      FixIsolatedVertex (ref u0, ref u1, ref u2, d3, d4, d5); // For T2
      if (Side (v0, v1, v2, u0) < 0) (v1, v2) = (v2, v1);
      if (Side (u0, u1, u2, v0) < 0) (u1, u2) = (u2, u1);

      // --------------------------------------------------------------------------------
      // 4. Check the intersection of the interval bounds
      // Now the edges (V0, V2) and (V0, V1) of T1, (U0, U1) and (U0, U2) of T2 intersects L.
      // If their bound intersects, we can declare that the triangles T1 and T2 intersects.
      return Side (v0, v1, u0, u1) <= 0 && Side (v0, v2, u2, u0) <= 0;

      // Helper routines -------------------------------------------------
      Point3f FetchVertex (int i) => new (p[i], p[i + 1], p[i + 2]);

      void FixIsolatedVertex (ref Point3f A, ref Point3f B, ref Point3f C, int x, int y, int z) {
         bool left = false, right = false, mid = true, swap = false;
         if (right = (x == y)) swap = z < 0;
         else if (left = (x == z)) swap = y < 0;
         else if (y == z) { mid = false; swap = x < 0; }
         // In case where all 3 vertices lie on differnt space (+, -, 0) i.e when mid = true
         // we choose the one which is not lying in the negative half space.

         // Apply a circular permutation to the vertices so that this isolated vertex is moved to the first position
         if (left || (mid && y > 0)) (A, B, C) = (B, C, A);
         else if (right || (mid && z > 0)) (A, B, C) = (C, A, B);
         // Perform swap to ensure the found vertex lies in the positive open half space of the other plane
         //if (swap) (B, C) = (C, B);
      }
   }

   // Routine used to test the intersection of two co-planar triangles based on the Guigue-Devillers algorithm.
   // In this test, all the checks are based on the 2D predicate. To confirm the intersection a maximum of
   // 10 predicates (2 for finding orientation + 3 for mapping region + 5 for confirmation) will be computed.
   static unsafe bool CoplanarTriTriGD (float* p, ref CTri ta, ref CTri tb) {
      // v0, v1, v2 = Vertices of the first triangle T1 in the plane P1
      var (v0, v1, v2) = (FetchVertex (ta.A), FetchVertex (ta.B), FetchVertex (ta.C));
      // u0, u1, u2 = Vertices of the second triangle T2 in the plane P2
      var (u0, u1, u2) = (FetchVertex (tb.A), FetchVertex (tb.B), FetchVertex (tb.C));
      // --------------------------------------------------------------------------------
      // 1. Make the T1 and T2 CCW
      // This is the preliminary step in which we will the fix the orientation of the two triangles.
      // If somehow the orientation of triangles are known beforehand, we can skip these 2 predicates.
      if (Side (v0, v1, v2) < 0) (v1, v2) = (v2, v1);
      if (Side (u0, u1, u2) < 0) (u1, u2) = (u2, u1);

      // --------------------------------------------------------------------------------
      // 2. Classify the vertex V0 against all the edges of T2
      // During this step we try to map the vertex V0 around the T2. Out of 6 possible regions,
      // we will fix the vertex at 2 specific regions (R1 or R2) by performing circular permutation.
      int s1 = Side (v0, u1, v0);
      int s2 = Side (u1, u2, v0);
      int s3 = Side (u2, u0, v0);

      bool region; // true for R1, false for R2
      switch (s1, s2, s3) {
         case (1, 1, 1): return true; // All 3 positives indicates V0 lies inside T2

         // Any two values are zero indicates V0 lies on one of the T2 vertices.
         case (0, 0, _) or (0, _, 0) or (_, 0, 0): return true;
         case (0, _, _) or (_, 0, _) or (_, _, 0): {
            // As (0, -, -) is impossible to occur, it is either (0, +, +) or (0, -, +)
            if (s1 + s2 + s3 == 2) return true; // (0, +, +) indicates V0 lies on the edge of T2
            // (0, +, -) indicates V0 lies along the infinite line along T2 edge, but outside the triangle. Hence it is R2 region
            else region = false; break;
         }
         // If 2 positives and 1 negative, make it (+, +, -) which maps to R1 region
         case (-1, 1, 1): (u0, u1, u2) = (u2, u0, u1); region = true; break;
         case (1, -1, 1): (u0, u1, u2) = (u1, u2, u0); region = true; break;
         case (1, 1, -1): region = true; break;

         // If 2 negatives and 1 positive, make it (+, -, -) which maps to R2 region
         case (-1, -1, 1): (u0, u1, u2) = (u1, u2, u0); region = false; break;
         case (-1, 1, -1): (u0, u1, u2) = (u2, u0, u1); region = false; break;
         case (1, -1, -1): region = false; break;
         default: region = false; break;
      }

      // --------------------------------------------------------------------------------
      // 3. Perform the final 5 predicates to confirm the intersection
      return region ? R1Test () : R2Test ();

      bool R1Test () {
         if (Side (u2, u0, v1) >= 0 && Side (u2, v0, v1) >= 0) {
            if (Side (v0, u0, v1) >= 0 || (Side (v0, u0, v2) >= 0 && Side (v1, v2, u0) >= 0)) return true;
         } else if (Side (u2, u0, v2) >= 0 && Side (v1, v2, u2) >= 0 && Side (v0, u0, v2) >= 0) return true;
         return false;
      }

      bool R2Test () {
         if (Side (u2, u0, v1) >= 0) {
            if (Side (u1, u2, v1) >= 0 &&
              ((Side (v0, u0, v1) >= 0 && Side (v0, u1, v1) <= 0) ||
               (Side (v0, u0, v2) >= 0 && Side (u2, u0, v2) >= 0))) return true;
            else if (Side (v0, u1, v1) <= 0 && Side (u1, u2, v2) >= 0 && Side (v1, v2, u1) >= 0) return true;
         } else if (Side (u2, u0, v2) >= 0 &&
                  ((Side (v1, v2, u2) >= 0 && Side (v2, v0, u0) >= 0) ||
                   (Side (v1, v2, u1) >= 0 && Side (u1, u2, v2) >= 0))) return true;
         return false;
      }

      Point3f FetchVertex (int i) => new (p[i], p[i + 1], p[i + 2]);
   }

   // This 3D predicate [a, b, c, d] tells (in accordance with right-handed thumb rule),
   // (1) The side of the point d relative to the plane through a, b and c. Above (1), Below (-1), On the plane (0).
   // (2) The twist direction along the ray ab relative to the direction of ray cd. Towards (1), Away (-1), Same direction (0).
   static int Side (Point3f a, Point3f b, Point3f c, Point3f d) {
      var (dx, dy, dz) = (d.X, d.Y, d.Z);
      return Side (new Point3f (a.X - dx, a.Y - dy, a.Z - dz),
                   new Point3f (b.X - dx, b.Y - dy, b.Z - dz),
                   new Point3f (c.X - dx, c.Y - dy, c.Z - dz));
   }

   // This the same 3D predicate as above. Instead of the points a, b and c,
   // here we can pass the normal vector of the plane through the points.
   static int Side (Vector3f n, Vector3f d) {
      var det = n.Dot (d);
      return det.IsZero () ? 0 : det > 0 ? 1 : -1;
   }

   // This 2D predicate [a, b, c] tells (in accordance with right-handed thumb rule),
   // (1) The side of the point c relative to the infinite line through the points a and b. Left (1), Right (-1), On the line (0).
   // (2) The orientation of the triangle abc. CCW (1), CW (-1), Degenerate (0)
   static int Side (Point3f a, Point3f b, Point3f c) {
      var det = a.X * Det (b.Y, b.Z, c.Y, c.Z) - a.Y * Det (b.X, b.Z, c.X, c.Z) + a.Z * Det (b.X, b.Y, c.X, c.Y);
      return det.IsZero () ? 0 : det > 0 ? 1 : -1;

      double Det (double a11, double a12, double a21, double a22) => a11 * a22 - a12 * a21;
   }
}