// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collision.cs
// ║║║║╬║╔╣║ Primitive collision detection methods
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.MathF;
namespace Nori;


/// <summary>Provides methods for collision detection between various geometric primitives.</summary>
public static class Collision {
   /// <summary>Checks if 'a' Bound3 intersects with another Bound3 'b'</summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (in Bound3 a, in Bound3 b) => Check (in a.X, in b.X) && Check (in a.Y, in b.Y) && Check (in a.Z, in b.Z);

   /// <summary>Checks if 'a' Bound2 intersects with another Bound2 'b'</summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (in Bound2 a, in Bound2 b) => Check (in a.X, in b.X) && Check (in a.Y, in b.Y);

   /// <summary>Checks if 'a' Bound1 intersects with another Bound1 'b'</summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (in Bound1 a, in Bound1 b) => a.Min <= b.Max && a.Max >= b.Min;

   /// <summary>Checks if 'a' sphere intersects with another sphere 'b'</summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (in MinSphere A, in MinSphere B) {
      var r = A.Radius + B.Radius;
      return (A.Center - B.Center).LengthSq <= r * r;
   }

   /// <summary>Checks if two OBBs intersect.</summary>
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (in OBB a, in OBB b) =>
      BoxBox (in a.Center, in a.X, in a.Y, in a.Extent, in b.Center, in b.X, in b.Y, in b.Extent);

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (ReadOnlySpan<Point3f> pts, in CTri a, in OBB b) =>
      BoxTri (in b.Center, in b.X, in b.Y, in b.Extent, in pts[a.A], in pts[a.B], in pts[a.C]);

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool Check (ReadOnlySpan<Point3f> pts, in CTri a, in CTri b) => TriTri (pts, in a, in b);

   /// <summary>Checks if two OBBs intersect using the Separating Axis Theorem (SAT)</summary>
   /// The algorithm tests 15 potential separating axes, and if no separating axis is found, the 
   /// OBBs are intersecting. The 15 axes are the 3 axes of each OBB (face-face) and the 9 axes 
   /// formed by the cross products of each pair of axes from the two OBBs (edge-edge).
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   public static bool BoxBox (in Point3f aC, in Vector3f aX, in Vector3f aY, in Vector3f aR, in Point3f bC, in Vector3f bX, in Vector3f bY, in Vector3f bR) {
      Vector3f aZ = aX * aY, bZ = bX * bY;
      float a0 = aR.X, a1 = aR.Y, a2 = aR.Z, b0 = bR.X, b1 = bR.Y, b2 = bR.Z;

      // Check 1. Test A axes: aX, aY, aZ
      // The translation vector T <t0, t1, t2> from a to b (in a's coordinate system)      
      var tmp = bC - aC; float t0 = Dot (in tmp, in aX);
      // The rotation matrix R (R00, R01 ... R33) represents 'b' in a's coordinate system.
      // Since the absolute values of R (denoted AR = |R|) are repeatedly used in all tests,
      // we precompute them once for efficiency. A small epsilon is added to handle
      // numerical errors that occur when two edges are nearly parallel, causing their
      // cross product to approach zero.
      // T, R, and AR are initialized in an interleaved manner to avoid redundant computations,
      // with early termination triggered on detection of a separating axis.
      float R00 = Dot (in aX, in bX), R01 = Dot (in aX, in bY), R02 = Dot (in aX, in bZ);
      float AR00 = Abs (R00) + E, AR01 = Abs (R01) + E, AR02 = Abs (R02) + E;
      if (Abs (t0) > a0 + b0 * AR00 + b1 * AR01 + b2 * AR02) return false;

      float t1 = Dot (in tmp, in aY);
      float R10 = Dot (in aY, in bX), R11 = Dot (in aY, in bY), R12 = Dot (in aY, in bZ);
      float AR10 = Abs (R10) + E, AR11 = Abs (R11) + E, AR12 = Abs (R12) + E;
      if (Abs (t1) > a1 + b0 * AR10 + b1 * AR11 + b2 * AR12) return false;

      float t2 = Dot (in tmp, in aZ);
      float R20 = Dot (in aZ, in bX), R21 = Dot (in aZ, in bY), R22 = Dot (in aZ, in bZ);
      float AR20 = Abs (R20) + E, AR21 = Abs (R21) + E, AR22 = Abs (R22) + E;
      if (Abs (t2) > a2 + b0 * AR20 + b1 * AR21 + b2 * AR22) return false;

      // Check 2. Test B axes: bX, bY, bZ
      if (Abs (t0 * R00 + t1 * R10 + t2 * R20) > b0 + a0 * AR00 + a1 * AR10 + a2 * AR20) return false;
      if (Abs (t0 * R01 + t1 * R11 + t2 * R21) > b1 + a0 * AR01 + a1 * AR11 + a2 * AR21) return false;
      if (Abs (t0 * R02 + t1 * R12 + t2 * R22) > b2 + a0 * AR02 + a1 * AR12 + a2 * AR22) return false;

      // Check 3. Test 9 (Ai x Bj) axis pairs
      if (Abs (t2 * R10 - t1 * R20) > (a1 * AR20 + a2 * AR10 + b1 * AR02 + b2 * AR01)) return false;  // aX x bX
      if (Abs (t2 * R11 - t1 * R21) > (a1 * AR21 + a2 * AR11 + b2 * AR00 + b0 * AR02)) return false;  // aX x bY
      if (Abs (t2 * R12 - t1 * R22) > (a1 * AR22 + a2 * AR12 + b0 * AR01 + b1 * AR00)) return false;  // aX x bZ

      if (Abs (t0 * R20 - t2 * R00) > (a2 * AR00 + a0 * AR20 + b1 * AR12 + b2 * AR11)) return false;  // aY x bX
      if (Abs (t0 * R21 - t2 * R01) > (a2 * AR01 + a0 * AR21 + b2 * AR10 + b0 * AR12)) return false;  // aY x bY
      if (Abs (t0 * R22 - t2 * R02) > (a2 * AR02 + a0 * AR22 + b0 * AR11 + b1 * AR10)) return false;  // aY x bZ

      if (Abs (t1 * R00 - t0 * R10) > (a0 * AR10 + a1 * AR00 + b1 * AR22 + b2 * AR21)) return false;  // aZ x bX
      if (Abs (t1 * R01 - t0 * R11) > (a0 * AR11 + a1 * AR01 + b2 * AR20 + b0 * AR22)) return false;  // aZ x bY
      if (Abs (t1 * R02 - t0 * R12) > (a0 * AR12 + a1 * AR02 + b0 * AR21 + b1 * AR20)) return false;  // aZ x bZ

      // No separating axis found, the OBBs are intersecting
      return true;
   }

   /// <summary>Checks if an oriented box (OBB) intersects with a triangle defined by points p1, p2, and p3.</summary>
   /// It uses the Separating Axis Theorem (SAT) to determine if there is a separating axis between the box and the triangle.
   /// It uses 13 potential separating axes: the three face normals of the box, the triangle's face normal, and the nine 
   /// cross products of the box edges and triangle edges. If no separating axis is found, the box and triangle are intersecting.
   /// <param name="bC">Box center.</param>
   /// <param name="bX">Box X-axis</param>
   /// <param name="bY">Box Y-axis</param>
   /// <param name="bH">Box half extents</param>
   /// <param name="p0">Triangle vertex 1</param>
   /// <param name="p1">Triangle vertex 2</param>
   /// <param name="p2">Triangle vertex 3</param>
   /// <returns>True, if there is a collision. False otherwise.</returns>
   public static bool BoxTri (in Point3f bC, in Vector3f bX, in Vector3f bY, in Vector3f bH, in Point3f p0, in Point3f p1, in Point3f p2) {
      // Transform triangle vertices (p0, p1, p2) into box's local space as (a, b, c)
      var bZ = bX * bY;
      Vector3f v0 = p0 - bC, v1 = p1 - bC, v2 = p2 - bC;
      var a = new Point3f (Dot (in v0, in bX), Dot (in v0, in bY), Dot (in v0, in bZ));
      var b = new Point3f (Dot (in v1, in bX), Dot (in v1, in bY), Dot (in v1, in bZ));
      var c = new Point3f (Dot (in v2, in bX), Dot (in v2, in bY), Dot (in v2, in bZ));

      // Check 1. Nine edge cross products
      // Following two optimizations are applied to improve performance:
      // 1. Optimize the cross product calculations for the axes by directly using the components of the triangle edges.
      //  a x b = (ax, ay, az) x (bx, by, bz) = (ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx)
      //  a x xAxis = (ax, ay, az) x (1, 0, 0) = (0, az, -ay)
      //  a x yAxis = (ax, ay, az) x (0, 1, 0) = (-az, 0, ax)
      //  a x zAxis = (ax, ay, az) x (0, 0, 1) = (ay, -ax, 0)
      //
      // 2. Ignore zero components in the cross products since they do not contribute to the projection.
      //  Axis 'n' = e0 x xAxis = (0, e0.Z, -e0.Y) => r = bH.Y * |ny| + bH.Z * |nz|, d0 = ny * a.Y + nz * a.Z, etc.

      // Edge 0
      var e0 = b - a;
      if (!TestAxis (e0.Z, -e0.Y, bH.Y, bH.Z, a.Y, a.Z, b.Y, b.Z, c.Y, c.Z)) return false;
      if (!TestAxis (-e0.Z, e0.X, bH.X, bH.Z, a.X, a.Z, b.X, b.Z, c.X, c.Z)) return false;
      if (!TestAxis (e0.Y, -e0.X, bH.X, bH.Y, a.X, a.Y, b.X, b.Y, c.X, c.Y)) return false;

      // Edge 1
      var e1 = c - b;
      if (!TestAxis (e1.Z, -e1.Y, bH.Y, bH.Z, a.Y, a.Z, b.Y, b.Z, c.Y, c.Z)) return false;
      if (!TestAxis (-e1.Z, e1.X, bH.X, bH.Z, a.X, a.Z, b.X, b.Z, c.X, c.Z)) return false;
      if (!TestAxis (e1.Y, -e1.X, bH.X, bH.Y, a.X, a.Y, b.X, b.Y, c.X, c.Y)) return false;

      // Edge 2
      var e2 = a - c;
      if (!TestAxis (e2.Z, -e2.Y, bH.Y, bH.Z, a.Y, a.Z, b.Y, b.Z, c.Y, c.Z)) return false;
      if (!TestAxis (-e2.Z, e2.X, bH.X, bH.Z, a.X, a.Z, b.X, b.Z, c.X, c.Z)) return false;
      if (!TestAxis (e2.Y, -e2.X, bH.X, bH.Y, a.X, a.Y, b.X, b.Y, c.X, c.Y)) return false;

      // Check 2. Check Box's AABB vs Triangle's AABB (three face normals of the Box)
      Bound3 b1 = new (-bH.X, -bH.Y, -bH.Z, bH.X, bH.Y, bH.Z), b2 = new (a, b, c);
      if (!Check (in b1, in b2)) return false;

      // Check 3. Triangle's face normal (basically box to triangle plane)
      var n = ((b - a) * (c - a)).Normalized (); // Triangle normal
      // The radius of the box projected onto the triangle normal
      var r = bH.X * Abs (n.X) + bH.Y * Abs (n.Y) + bH.Z * Abs (n.Z);
      // If the distance from the box center to the triangle plane is
      // greater than the projected radius, there is a separating axis.
      if (Abs (Dot (in n, new (a.X, a.Y, a.Z))) > r) return false;

      // No separating axis found. The OBB and triangle are intersecting.
      return true;

      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      static bool TestAxis (float n1, float n2, float h1, float h2, float a1, float a2, float b1, float b2, float c1, float c2) {
         // Components. Axis: n1, n2, Box half extents: h1, h2, Triangle vertices: a1,a2; b1,b2; c1,c2
         // The radius of the box projected onto the axis
         var r = h1 * Abs (n1) + h2 * Abs (n2);

         // Project triangle onto axis to find min and max
         float d0 = n1 * a1 + n2 * a2, d1 = n1 * b1 + n2 * b2, d2 = n1 * c1 + n2 * c2;
         var (min, max) = (d0, d0);
         if (d1 < min) min = d1; else if (d1 > max) max = d1;
         if (d2 < min) min = d2; else if (d2 > max) max = d2;

         // If the distance from the box center to the triangle projection
         // is greater than the projected radius, there is a separating axis.
         return !(min > r || max < -r);
      }
   }

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

   /// <summary>Given the vertices of two triangles (a1, b1, c1) and (a2, b2, c2), checks if two triangles intersect in the 3D space.</summary>
   /// See TriTri (pts, triangleA, triangleB) for detailed comments.
   /// <remarks>This variant is about 25% slower than the first variant.</remarks>
   public static bool TriTri (in Point3f a1, in Point3f b1, in Point3f c1, in Point3f a2, in Point3f b2, in Point3f c2) {
      unsafe {
         Point3f* pts = stackalloc Point3f[] { a1, b1, c1, a2, b2, c2 };
         return TriTri (pts, 0, 1, 2, (b1 - a1) * (c1 - a1), 3, 4, 5, (b2 - a2) * (c2 - a2));
      }
   }

   /// <summary>Checks if two triangles intersect in 3D space.</summary>
   unsafe static bool TriTri (Point3f* p, int a1, int b1, int c1, in Vector3f n1, int a2, int b2, int c2, in Vector3f n2) {
      // Step 1. Plane-side tests
      // 1a. Check if triangle 1 is completely on one side of triangle 2's plane
      var pa2 = p[a2]; var pa1 = p[a1];
      var sa1 = Sign (Dot (pa1 - pa2, in n2));
      var sb1 = Sign (Dot (p[b1] - pa2, in n2));
      var sc1 = Sign (Dot (p[c1] - pa2, in n2));
      if (SameSign (sa1, sb1, sc1)) return false;

      // Step 2. Check for coplanar case. 
      if (sa1 == sb1 && sb1 == sc1 && sa1 == 0) {
         // Now perform 2D triangle overlap tests by projecting 3D triangle points into 2D space.
         // Instead of mapping points directly onto the triangle’s plane, we project them onto
         // a principal plane that best aligns with the triangle. This approach simplifies
         // the transformation and avoids several costly arithmetic operations.
         float nx = Abs (n2.X), ny = Abs (n2.Y), nz = Abs (n2.Z);
         Point2* pt2 = stackalloc Point2[6];
         Point3f pb1 = p[b1], pc1 = p[c1], pb2 = p[b2], pc2 = p[c2];
         if (nz > nx && nz > ny) {
            // Project to XY plane
            pt2[0] = new (pa1.X, pa1.Y); pt2[1] = new (pb1.X, pb1.Y); pt2[2] = new (pc1.X, pc1.Y);
            pt2[3] = new (pa2.X, pa2.Y); pt2[4] = new (pb2.X, pb2.Y); pt2[5] = new (pc2.X, pc2.Y);
         } else if (ny > nx && ny > nz) {
            // Project to ZX plane
            pt2[0] = new (pa1.X, pa1.Z); pt2[1] = new (pb1.X, pb1.Z); pt2[2] = new (pc1.X, pc1.Z);
            pt2[3] = new (pa2.X, pa2.Z); pt2[4] = new (pb2.X, pb2.Z); pt2[5] = new (pc2.X, pc2.Z);
         } else {
            // Project to YZ plane
            pt2[0] = new (pa1.Y, pa1.Z); pt2[1] = new (pb1.Y, pb1.Z); pt2[2] = new (pc1.Y, pc1.Z);
            pt2[3] = new (pa2.Y, pa2.Z); pt2[4] = new (pb2.Y, pb2.Z); pt2[5] = new (pc2.Y, pc2.Z);
         }
         return TriTri (pt2, 0, 1, 2, 3, 4, 5);
      }

      // 1b. Check if triangle 2 is completely on one side of triangle 1's plane
      var sa2 = Sign (Dot (pa2 - pa1, in n1));
      var sb2 = Sign (Dot (p[b2] - pa1, in n1));
      var sc2 = Sign (Dot (p[c2] - pa1, in n1));
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
      pa1 = p[a1]; pa2 = p[a2]; 
      return Side (in pa1, in p[b1], in pa2, in p[b2]) <= 0 && Side (in pa1, in p[c1], in p[c2], in pa2) <= 0;

      // Gets the orientation of point 'd' with respect to the plane defined by triangle 'abc'
      // +1 = d is on the positive side of the plane, -1 = d is on the negative side, 0 = d is on the plane
      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      static float Side (in Point3f a, in Point3f b, in Point3f c, in Point3f d) => Dot (d - a, (b - a) * (c - a));

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
   /// Primarily it performs following two tests to check the intersection:
   /// 1. If vertex of one triangle is inside or on the boundary of the other
   /// 2. If edge of first triangle crosses edge of the other
   /// Like the 3D triangle intersection check, the planar variant also uses orientation
   /// tests to determine the intersections. 
   public static bool TriTri (in Point2 a1, in Point2 b1, in Point2 c1, in Point2 a2, in Point2 b2, in Point2 c2) {
      unsafe {
         Point2 * pts = stackalloc Point2[] { a1, b1, c1, a2, b2, c2 };
         return TriTri (pts, 0, 1, 2, 3, 4, 5);
      }
   }

   /// <summary>Tests if two coplanar triangles intersect with each other.</summary>
   unsafe static bool TriTri (Point2* p, int a1, int b1, int c1, int a2, int b2, int c2) {
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
         var pa = p[a]; var pb = p[b]; var pc = p[c];
         return Sign ((pa.X - pc.X) * (pb.Y - pc.Y) - (pa.Y - pc.Y) * (pb.X - pc.X));
      }
   }

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static float Dot (in Vector3f a, in Vector3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static int Sign (float d) => d switch { < -ESq => -1, > ESq => 1, _ => 0 };
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   static int Sign (double d) => d switch { < -Lib.EpsilonSq => -1, > Lib.EpsilonSq => 1, _ => 0 };

   const float E = (float)Lib.Epsilon;
   const float ESq = (float)Lib.EpsilonSq;
}
