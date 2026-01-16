namespace Nori;
using static Math;

public static partial class Tri {
   // MetaCAM's triangle-triangle collision routine. Very robust, but about 3 times slower
   // than the Flux one. Detects coplanar triangle collisions reliably, while Flux routine often 
   // fails when testing a triangle against itself (reports no collision)
   public static bool CollideMCAM (Point3f P1, Point3f P2, Point3f P3, Point3f Q1, Point3f Q2, Point3f Q3) {
      // One triangle is (P1, P2, P3), the other is (Q1, Q2, Q3). 
      // The edges are (E1, E2, E3) and (F1, F2, F3). 
      // Normals are N1 and M1

      // Check if normal of triangle 1 is a separating axis
      Vector3f E1 = P2 - P1, E2 = P3 - P2, E3 = P1 - P3;
      Vector3f N1 = E1 * E2;
      if (SeparatedOn (N1)) return false;       // (1)
      
      // Check with normal of triangle 2
      Vector3f F1 = Q2 - Q1, F2 = Q3 - Q2, F3 = Q1 - Q3;
      Vector3f M1 = F1 * F2;
      if (SeparatedOn (M1)) return false;       // (2)

      // Check with outward edge normals of triangle 1
      if (SeparatedOn (E1 * N1)) return false;
      if (SeparatedOn (E2 * N1)) return false;
      if (SeparatedOn (E3 * N1)) return false;

      // Check with outward edge normals of triangle 2
      if (SeparatedOn (F1 * M1)) return false;
      if (SeparatedOn (F2 * M1)) return false;
      if (SeparatedOn (F3 * M1)) return false;

      // Check with the 9 cross products of edges
      if (SeparatedOn (E1 * F1)) return false;
      if (SeparatedOn (E1 * F2)) return false;
      if (SeparatedOn (E1 * F3)) return false;
      if (SeparatedOn (E2 * F1)) return false;
      if (SeparatedOn (E2 * F2)) return false;
      if (SeparatedOn (E2 * F3)) return false;
      if (SeparatedOn (E3 * F1)) return false;
      if (SeparatedOn (E3 * F2)) return false;
      if (SeparatedOn (E3 * F3)) return false;
      return true; 

      bool SeparatedOn (Vector3f axis) {
         double u1 = Dot (axis, P1), u2 = Dot (axis, P2), u3 = Dot (axis, P3);
         double v1 = Dot (axis, Q1), v2 = Dot (axis, Q2), v3 = Dot (axis, Q3);
         double uMin = Min (u1, Min (u2, u3)), vMax = Max (v1, Max (v2, v3));
         if (uMin > vMax) return true;
         double vMin = Min (v1, Min (v2, v3)), uMax = Max (u1, Max (u2, u3));
         return vMin > uMax;
      }
   }
}
