namespace Nori;
using static Math;

public static partial class Tri {
   public static bool CollideHeld (Point3f V0, Point3f V1, Point3f V2, Point3f U0, Point3f U1, Point3f U2) {
      // Plane equation of triangle V
      Vector3f E1 = V1 - V0, E2 = V2 - V0, N1 = E1 * E2;
      double d1 = -Dot (N1, V0);

      // Signed distances of U vertices to the plane
      const double EPSILON = 0.000001;
      double du0 = N1.X * U0.X + N1.Y * U0.Y + N1.Z * U0.Z + d1;
      double du1 = N1.X * U1.X + N1.Y * U1.Y + N1.Z * U1.Z + d1;
      double du2 = N1.X * U2.X + N1.Y * U2.Y + N1.Z * U2.Z + d1;
      if (Abs (du0) < EPSILON) du0 = 0.0;
      if (Abs (du1) < EPSILON) du1 = 0.0;
      if (Abs (du2) < EPSILON) du2 = 0.0;
      double du0du1 = du0 * du1, du0du2 = du0 * du2;
      if (du0du1 > 0.0 && du0du2 > 0.0) return false;

      // Find the line of projection of the triangle U on the plane of V0
      Point3f a = new (), b = Point3f.Nil;
      if (du1 * du2 <= 0) a = (du1 / (du1 - du2)).Along (U1, U2);
      if (du0du1 <= 0) { b = a; a = (du0 / (du0 - du1)).Along (U0, U1); };
      if (du0du2 <= 0) {
         if (!b.IsNil) return CollideMCAM (V0, V1, V2, U0, U1, U2);
         b = a; a = (du0 / (du0 - du2)).Along (U0, U2);
      }

      // Figure out a 2D plane on which to project the triangle V and the line of 
      // intersection ab. Then, we reduce this to a 2D problem of checking whether
      // the line pa-pb intersects the triangle t1,t2,t3
      Point2 pa, pb, t1, t2, t3;
      if (N1.Z >= N1.X && N1.Z >= N1.Y) {          // Use XY plane
         pa = new (a.X, a.Y); pb = new (b.X, b.Y);
         t1 = new (V0.X, V0.Y); t2 = new (V1.X, V1.Y); t3 = new (V2.X, V2.Y);
      } else if (N1.Y >= N1.X && N1.Y >= N1.Z) {   // Use XZ plane
         pa = new (a.X, a.Z); pb = new (b.X, b.Z);
         t1 = new (V0.X, V0.Z); t2 = new (V1.X, V1.Z); t3 = new (V2.X, V2.Z);
      } else {                                     // Use YZ plane
         pa = new (a.Y, a.Z); pb = new (b.Y, b.Z);
         t1 = new (V0.Y, V0.Z); t2 = new (V1.Y, V1.Z); t3 = new (V2.Y, V2.Z);
      }

      // Get the orientation of each of the triangle vertices relative to the line
      // pa..pb. If all are nonzero and have the same sign, the line misses the
      // triangle completely
      int ta1 = t1.Side (pa, pb), ta2 = t2.Side (pa, pb), ta3 = t3.Side (pa, pb);
      if (ta1 * ta2 > 0 && ta2 * ta3 > 0) return false;

      // Check if either of the endpoints of the line is in the triangle. 
      // This is checked by seeing if all the point is on the same 'side' (LEFT/RIGHT) 
      // of all the 3 segments. 
      int sa1 = pa.Side (t1, t2), sa2 = pa.Side (t2, t3), sa3 = pa.Side (t3, t1);
      // if (sa1 * sa2 >= 0 && sa2 * sa3 >= 0) return true;
      int sb1 = pb.Side (t1, t2), sb2 = pb.Side (t2, t3), sb3 = pb.Side (t3, t1);
      if (sb1 * sb2 >= 0 && sb2 * sb3 >= 0) return true;

      // Check if any of the 3 edges of the triangle intersect the line a..b
      // The following checks that a and b are on opposite sides of the edge t1..t2,
      // and t1 and t2 are on opposite sides of the edge pa..pb
      if (sa1 * sb1 <= 0 && ta1 * ta2 <= 0) return true;
      // Likewise for the other two edge of the triangle:
      if (sa2 * sb2 <= 0 && ta2 * ta3 <= 0) return true;
      if (sa3 * sb3 <= 0 && ta3 * ta1 <= 0) return true;

      return false;
   }

   static double Dot (Vector3f a, Point3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

}