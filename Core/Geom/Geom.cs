using static System.Math;
namespace Nori;

#region class Geom ---------------------------------------------------------------------------------
public readonly struct Line2 (Point2 a, Point2 b, bool finite) {
   public readonly Point2 A = a;
   public readonly Point2 B = b;
   public readonly bool Finite = finite;
}

public readonly struct Circle2 (Point2 c, double r) {
   public readonly Point2 Center = c;
   public readonly double Radius = r;
}

public readonly struct Arc2 (Point2 c, double r, double sa, double ea) {
   public readonly Point2 Center = c;
   public readonly double Radius = r;
   public readonly double SAngle = sa;
   public readonly double EAngle = ea;
}

/// <summary>
/// The Geom class contains a number of core geometry functions
/// </summary>
public static class Geom {
   /// <summary>Return the intersection Point2 of two lines p1-p2 and p3-p4</summary>
   /// <param name="A">First Point2 on line 1</param>
   /// <param name="B">Second Point2 on line 1</param>
   /// <param name="C">First Point2 on line 2</param>
   /// <param name="D">Second Point2 on line 2</param>
   /// This treats the lines A-B and C-D as infinite lines, not as finite segments.
   /// If the lines are parallel (do not intersect), this returns Point2.Nil. 
   public static Point2 LineXLine (Point2 A, Point2 B, Point2 C, Point2 D) {
      // Line AB represented as a1x + b1y = c1
      double a1 = B.Y - A.Y, b1 = A.X - B.X, c1 = a1 * A.X + b1 * A.Y;
      // Line CD represented as a2x + b2y = c2
      double a2 = D.Y - C.Y, b2 = C.X - D.X, c2 = a2 * C.X + b2 * C.Y;

      // Use determinant to figure out if the lines are parallel, and return Nil if so
      double determinant = a1 * b2 - a2 * b1;
      if (Abs (determinant) < 1e-12) return Point2.Nil;    
      return new ((b2 * c1 - b1 * c2) / determinant, (a1 * c2 - a2 * c1) / determinant);
   }
}
#endregion
