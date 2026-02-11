// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TriDevillers.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public static partial class Tri {
   /// <summary>Checks if two triangles intersect in 3D space.</summary>
   /// To test if two triangles in 3D intersect, it checks:
   /// 1. If each triangle intersects the plane of the 'other' triangle.
   /// 2. If the triangle intersection segments overlap on the intersection line (1D overlap check)
   /// The algorithm relies on orientation tests to detect the overlap, making it relatively
   /// robust against numerical precision errors.
   // It implements the Devilliers & Guigue algorithm for triangle-triangle intersection.
   // Reference: https://inria.hal.science/inria-00072100/file/RR-4488.pdf
   public static bool TriTri (ReadOnlySpan<Point3f> pts, in CTri a, in CTri b) {
      unsafe {
         fixed (Point3f* p = pts) return TriTri (p, a.A, a.B, a.C, a.N, b.A, b.B, b.C, b.N);
      }
   }

   /// <summary>Checks if two triangles intersect in 3D space.</summary>
   public unsafe static bool TriTri (Point3f* p, int a1, int b1, int c1, in Vector3f n1, int a2, int b2, int c2, in Vector3f n2) {
      // Step 1. Plane-side tests
      // 1a. Check if triangle 1 is completely on one side of triangle 2's plane
      ref var pa2 = ref p[a2]; ref var pa1 = ref p[a1];
      var sa1 = Sign (Dot (pa1 - pa2, n2));
      var sb1 = Sign (Dot (p[b1] - pa2, n2));
      var sc1 = Sign (Dot (p[c1] - pa2, n2));
      if (SameSign (sa1, sb1, sc1)) return false;

      // Step 2. Check   for coplanar case. 
      if (sa1 == sb1 && sb1 == sc1 && sa1 == 0) {
         // Now perform 2D triangle overlap tests by projecting 3D triangle points into 2D space.
         // Instead of mapping points directly onto the triangle’s plane, we project them onto
         // a principal plane that best aligns with the triangle. This approach simplifies
         // the transformation and avoids several costly arithmetic operations.
         var (nx, ny, nz) = (MathF.Abs (n2.X), MathF.Abs (n2.Y), MathF.Abs (n2.Z));
         var P2 = XY;
         if (nx > ny && nx > nz) P2 = YZ;
         else if (ny > nx && ny > nz) P2 = ZX;
         Point2* pt2 = stackalloc Point2[] { P2 (p[a1]), P2 (p[b1]), P2 (p[c1]), P2 (p[a2]), P2 (p[b2]), P2 (p[c2]) };
         return TriTri2D (pt2, 0, 1, 2, 3, 4, 5);

         static Point2 XY (in Point3f p) => new (p.X, p.Y); // Project to XY plane
         static Point2 YZ (in Point3f p) => new (p.Y, p.Z); // Project to YZ plane
         static Point2 ZX (in Point3f p) => new (p.X, p.Z); // Project to ZX plane
      }

      // 1b. Check if triangle 2 is completely on one side of triangle 1's plane
      var sa2 = Sign (Dot (pa2 - pa1, n1));
      var sb2 = Sign (Dot (p[b2] - pa1, n1));
      var sc2 = Sign (Dot (p[c2] - pa1, n1));
      if (SameSign (sa2, sb2, sc2)) return false;

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

      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      static bool SameSign (int a, int b, int c) => a == b && b == c && a != 0;
   }

   /// <summary>Tests if two coplanar triangles intersect with each other.</summary>
   unsafe static bool TriTri2D (Point2* p, int a1, int b1, int c1, int a2, int b2, int c2) {
      // Step 0: Ensure both triangles are ccw
      if (Side (a1, b1, c1) < 0) (b1, c1) = (c1, b1);
      if (Side (a2, b2, c2) < 0) (b2, c2) = (c2, b2);

      // Step 1 & 2: Classify point 'a1' wrt the second traingle's edge and test for cases:
      // a. vertex is fully contained within other triangle
      // b. vertex lies in region R1 (+ + -) of the triangle plane
      // c. vertex lies in region R2 (+ - -) of the triangle plane
      // Permute other cases by aligning them to b or c.
      return (Side (a2, b2, a1) >= 0, Side (b2, c2, a1) >= 0, Side (c2, a2, a1) >= 0) switch {
         (true, true, true) => true,      // (+ + +) => vertex 'a1' is inside or on the boundary of the second triangle
         (true, true, false) => R1 (),    // (+ + -) => vertex 'a1' is in R1 region of the second triangle
         (true, false, false) => R2 (),   // (+ - -) => vertex 'a1' is in R2 region of the second triangle
         (true, false, true) => R1 (1),   // (+ - +) => vertex 'a1' is in R1 region of the second triangle after rotating it to the right
         (false, true, true) => R1 (-1),  // (- + +) => vertex 'a1' is in R1 region of the second triangle after rotating it to the left
         (false, true, false) => R2 (-1), // (- + -) => vertex 'a1' is in R2 region of the second triangle after rotating it to the left
         (false, false, true) => R2 (1),  // (- - +) => vertex 'a1' is in R2 region of the second triangle after rotating it to the right
         _ => false
      };

      // Step 3: Orientation based decision tree for (+ + -) and (+ - -).
      // Implements decision tree for configuration (+ + -) from Fig 9.
      // Depending on 'rotate' flag value -1/+1, it reorders the 'second' triangle
      // by rotating it to the left/right to bring it to the canonical form.
      bool R1 (int rotate = 0) {
         // Rotate second triangle to the left (rotate < 0) or right (rotate > 0)
         if (rotate != 0) (a2, b2, c2) = rotate < 0 ? (b2, c2, a2) : (c2, a2, b2);
         if (Side (c2, a2, b1) >= 0) { // I
            if (Side (c2, a1, b1) >= 0) { // II.a
               if (Side (a1, a2, b1) >= 0) { // III.a
                  return true;   // b1 in R13 (Fig 7)
               } else if (Side (a1, a2, c1) >= 0 && Side (b1, c1, a2) >= 0) { // IV.a
                  return true;  // V
               }
            }
         } else if (Side (c2, a2, c1) >= 0 && Side (b1, c1, c2) >= 0 && Side (a1, a2, c1) >= 0) { // II.b, III.b
            return true; // IV.b  
         }
         return false;
      }

      // Implements decision tree for configuration (+ - -) from Fig 10
      bool R2 (int rotate = 0) {
         // Rotate second triangle to the left (rotate < 0) or right (rotate > 0)
         if (rotate != 0) (a2, b2, c2) = rotate < 0 ? (b2, c2, a2) : (c2, a2, b2);
         if (Side (c2, a2, b1) >= 0) { // I
            if (Side (c2, b2, b1) <= 0) { // II.a
               if (Side (a1, a2, b1) > 0) { // III.a
                  if (Side (a1, b2, b1) <= 0) // IV.a
                     return true;
               } else if (Side (a1, a2, c1) >= 0 && Side (b1, c1, a2) >= 0) // IV.b
                  return true; // V.a
            } else if (Side (a1, b2, b1) <= 0 && Side (c2, b2, c1) <= 0 && Side (b1, c1, b2) >= 0) // III.b, IV.c
               return true; // V.b
         } else if (Side (c2, a2, c1) >= 0) { // II.b
            if (Side (b1, c1, c2) >= 0) { // III.c
               if (Side (a1, a2, c1) >= 0) // IV.d
                  return true;
            } else if (Side (b1, c1, b2) >= 0 && Side (b2, c2, c1) >= 0) // IV.e
               return true; // V.c
         }

         return false;
      }

      // Gets the orientation of point 'c' with respect to the line defined by points 'a' and 'b'
      // +1 = c is on the left side of the line, -1 = c is on the right side, 0 = c is on the line
      int Side (int a, int b, int c) {
         ref var pa = ref p[a]; ref var pb = ref p[b]; ref var pc = ref p[c];
         return Sign ((pa.X - pc.X) * (pb.Y - pc.Y) - (pa.Y - pc.Y) * (pb.X - pc.X));
      }
   }

   /// <summary>Tests if two coplanar triangles intersect with each other.</summary>
   unsafe static bool TriTri2Df (Point2f* p) {
      // Step 0: Ensure both triangles are ccw
      int a1 = 0, b1, c1, a2 = 3, b2, c2;
      if (Side (0, 1, 2) < 0) (b1, c1) = (2, 1); else (b1, c1) = (1, 2);
      if (Side (3, 4, 5) < 0) (b2, c2) = (5, 4); else (b2, c2) = (4, 5);

      // Step 1 & 2: Classify point 'a1' wrt the second traingle's edge and test for cases:
      // a. vertex is fully contained within other triangle
      // b. vertex lies in region R1 (+ + -) of the triangle plane
      // c. vertex lies in region R2 (+ - -) of the triangle plane
      // Permute other cases by aligning them to b or c.
      return (Side (a2, b2, a1) >= 0, Side (b2, c2, a1) >= 0, Side (c2, a2, a1) >= 0) switch {
         (true, true, true) => true,      // (+ + +) => vertex 'a1' is inside or on the boundary of the second triangle
         (true, true, false) => R1 (),    // (+ + -) => vertex 'a1' is in R1 region of the second triangle
         (true, false, false) => R2 (),   // (+ - -) => vertex 'a1' is in R2 region of the second triangle
         (true, false, true) => R1 (1),   // (+ - +) => vertex 'a1' is in R1 region of the second triangle after rotating it to the right
         (false, true, true) => R1 (-1),  // (- + +) => vertex 'a1' is in R1 region of the second triangle after rotating it to the left
         (false, true, false) => R2 (-1), // (- + -) => vertex 'a1' is in R2 region of the second triangle after rotating it to the left
         (false, false, true) => R2 (1),  // (- - +) => vertex 'a1' is in R2 region of the second triangle after rotating it to the right
         _ => false
      };

      // Step 3: Orientation based decision tree for (+ + -) and (+ - -).
      // Implements decision tree for configuration (+ + -) from Fig 9.
      // Depending on 'rotate' flag value -1/+1, it reorders the 'second' triangle
      // by rotating it to the left/right to bring it to the canonical form.
      bool R1 (int rotate = 0) {
         // Rotate second triangle to the left (rotate < 0) or right (rotate > 0)
         if (rotate != 0) (a2, b2, c2) = rotate < 0 ? (b2, c2, a2) : (c2, a2, b2);
         if (Side (c2, a2, b1) >= 0) { // I
            if (Side (c2, a1, b1) >= 0) { // II.a
               if (Side (a1, a2, b1) >= 0) { // III.a
                  return true;   // b1 in R13 (Fig 7)
               } else if (Side (a1, a2, c1) >= 0 && Side (b1, c1, a2) >= 0) { // IV.a
                  return true;  // V
               }
            }
         } else if (Side (c2, a2, c1) >= 0 && Side (b1, c1, c2) >= 0 && Side (a1, a2, c1) >= 0) { // II.b, III.b
            return true; // IV.b  
         }
         return false;
      }

      // Implements decision tree for configuration (+ - -) from Fig 10
      bool R2 (int rotate = 0) {
         // Rotate second triangle to the left (rotate < 0) or right (rotate > 0)
         if (rotate != 0) (a2, b2, c2) = rotate < 0 ? (b2, c2, a2) : (c2, a2, b2);
         if (Side (c2, a2, b1) >= 0) { // I
            if (Side (c2, b2, b1) <= 0) { // II.a
               if (Side (a1, a2, b1) > 0) { // III.a
                  if (Side (a1, b2, b1) <= 0) // IV.a
                     return true;
               } else if (Side (a1, a2, c1) >= 0 && Side (b1, c1, a2) >= 0) // IV.b
                  return true; // V.a
            } else if (Side (a1, b2, b1) <= 0 && Side (c2, b2, c1) <= 0 && Side (b1, c1, b2) >= 0) // III.b, IV.c
               return true; // V.b
         } else if (Side (c2, a2, c1) >= 0) { // II.b
            if (Side (b1, c1, c2) >= 0) { // III.c
               if (Side (a1, a2, c1) >= 0) // IV.d
                  return true;
            } else if (Side (b1, c1, b2) >= 0 && Side (b2, c2, c1) >= 0) // IV.e
               return true; // V.c
         }

         return false;
      }

      // Gets the orientation of point 'c' with respect to the line defined by points 'a' and 'b'
      // +1 = c is on the left side of the line, -1 = c is on the right side, 0 = c is on the line
      int Side (int a, int b, int c) {
         ref var pa = ref p[a]; ref var pb = ref p[b]; ref var pc = ref p[c];
         return Sign ((pa.X - pc.X) * (pb.Y - pc.Y) - (pa.Y - pc.Y) * (pb.X - pc.X));
      }
   }

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static float Dot (in Vector3f a, in Vector3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static float Dot (in Point3f a, in Vector3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static int Sign (float d) => d switch { < -ESq => -1, > ESq => 1, _ => 0 };
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static int Sign (double d) => d switch { < -Lib.EpsilonSq => -1, > Lib.EpsilonSq => 1, _ => 0 };

   const float E = (float)Lib.Epsilon;
   const float ESq = (float)Lib.EpsilonSq;
}
