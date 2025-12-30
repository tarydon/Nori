// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBB.cs
// ║║║║╬║╔╣║ Implements minimum enclosing 'Orientend Bounding Box' in 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Represents a bounding box oriented along given axes.</summary>
public readonly struct OBB {
   public OBB (CoordSystem cs, Vector3 ext) => (CS, Extent) = (cs, ext);

   // The box center
   public Point3 Center => CS.Org;
   // OBB origin and co-ordinate axes.
   public readonly CoordSystem CS;
   // The 'half extent' along the axes.
   public readonly Vector3 Extent;
   // The box area
   public double Area => 8 * (Extent.X * Extent.Y + Extent.X * Extent.Z + Extent.Y * Extent.Z);
   // The box volume
   public double Volume => 8 * (Extent.X * Extent.Y * Extent.Z);

   public static OBB From (ReadOnlySpan<Point3> pts) {
      ReadOnlySpan<Vector3> axes = Axes;
      OBB best = new (CoordSystem.Nil, Vector3.Zero);
      // The extremal point set
      Span<Point3> set = stackalloc Point3[2 * axes.Length];
      // The extremal extents
      Span<double> min = stackalloc double[axes.Length], max = stackalloc double[axes.Length];
      // 1. Get extremal points wrt the given axes.
      GetExtremalPoints (pts, axes, set, min, max);
      Span<Point3> minPts = set[..axes.Length], maxPts = set[axes.Length..];

      // 2. Make AABB as fallback OBB. (Note that the first three axes in the sample are world X, Y and Z)
      var aabb = best = CreateBox (axes, min, max); 
      var bestArea = Area (best.Extent); var aabbArea = bestArea;

      // 3. Build the irr. ditetrahedron now.
      // 3.a. First find the base triangle (p0, p1, p1)
      var (p0, p1, p2) = (minPts[0], maxPts[0], Point3.Nil);
      double dMax = p0.DistToSq (p1);
      for (int i = 1; i < maxPts.Length; i++) {
         var (a, b) = (minPts[i], maxPts[i]);
         var d = a.DistToSq (b);
         if (d > dMax) (dMax, p0, p1) = (d, a, b);
      }

      dMax = double.MinValue;
      foreach (var p in set) {
         var d = p.DistToLineSq (p0, p1);
         if (d > dMax) (dMax, p2) = (d, p);
      }

      // 3.b. Now we have the base of the triangle as (p0, p1, p2).
      //  Compute the top and bottom tetrahedron apex points (q0, q1).
      var (q0, q1) = GetTetraApexPoints (p0, p1, p2, set);

      // 4. Refine the box for the smallest surface area by locating the best
      //  axes from the di-tetraheron triangles.
      ReadOnlySpan<Point3> tri = [p0, p1, p2, q0, q1];
      for (int i = 0; i < tri.Length - 2; i++) {
         for (int j = i + 1; j < tri.Length - 1; j++) {
            for (int k = j + 1; k < tri.Length; k++) {
               RefineBox (tri[i], tri[j], tri[k], set, ref best, ref bestArea);
            }
         }
      }

      // 5. Final step - Refine the box by doing a final pass through all points along OBB axes
      ReadOnlySpan<Vector3> csa = [best.CS.VecX, best.CS.VecY, best.CS.VecZ];
      ComputeProjections (pts, csa, min, max);
      best = CreateBox (csa, min, max);
      if (aabb.Area < best.Area) best = aabb;
      return best;

      static double Area (Vector3 extent) => extent.X * extent.Y + extent.Y * extent.Z + extent.Z * extent.X;
      static double Volume (Vector3 extent) => extent.X * extent.Y * extent.Z;

      static void ComputeProjections (ReadOnlySpan<Point3> set, ReadOnlySpan<Vector3> axes, Span<double> min, Span<double> max) {
         min[0] = min[1] = min[2] = double.MaxValue;
         max[0] = max[1] = max[2] = double.MinValue;
         for (int i = 0; i < 3; i++) {
            var a = axes[i];
            foreach (var p in set) {
               // Compute projection of point 'p' on axes (basically Dot (p, axes[j])
               double d = p.X * a.X + p.Y * a.Y + p.Z * a.Z;
               if (d < min[i]) min[i] = d;
               if (d > max[i]) max[i] = d;
            }
         }
      }

      static OBB CreateBox (ReadOnlySpan<Vector3> axes, ReadOnlySpan<double> min, ReadOnlySpan<double> max) {
         ReadOnlySpan<double> h = [max[0] - min[0], max[1] - min[1], max[2] - min[2]];
         var center = Point3.Zero;
         for (int i = 0; i < 3; i++) {
            center += axes[i] * ((max[i] + min[i]) * 0.5);
         }
         return new OBB (new (center, axes[0], axes[1]), (new Vector3 (h[0], h[1], h[2]) * 0.5));
      }

      static void GetExtremalPoints (ReadOnlySpan<Point3> pts, ReadOnlySpan<Vector3> axes, Span<Point3> set, Span<double> min, Span<double> max) {
         for (int i = 0; i < min.Length; i++) (min[i], max[i]) = (double.MaxValue, double.MinValue);
         Span<Point3> minPts = set[..axes.Length], maxPts = set[axes.Length..];

         for (int i = 0; i < axes.Length; i++) {
            var a = axes[i];
            foreach (var p in pts) {
               // Compute projection of point 'p' on axes (p.Dot (a))
               double d = p.X * a.X + p.Y * a.Y + p.Z * a.Z;
               if (d < min[i]) (min[i], minPts[i]) = (d, p);
               if (d > max[i]) (max[i], maxPts[i]) = (d, p);
            }
         }
      }

      static (Point3 p, Point3 q) GetTetraApexPoints (Point3 a, Point3 b, Point3 c, ReadOnlySpan<Point3> set) {
         // Now we have the base of the triangle as (a, b, c)
         // Build the irr. ditetrahedron now.
         var u = (b - a).Normalized ();
         var v = (c - a).Normalized ();
         var w = (u * v).Normalized (); // The plane normal
         v = (w * u).Normalized ();
         // Now find min-max along normal 'w'.
         var (min, max) = (double.MaxValue, double.MinValue);
         var (q0, q1) = (Point3.Nil, Point3.Nil);
         foreach (var p in set) {
            // Compute projection of point 'p' on normal (p.Dot (w))
            double d = p.X * w.X + p.Y * w.Y + p.Z * w.Z;
            if (d < min) (min, q0) = (d, p);
            if (d > max) (max, q1) = (d, p);
         }
         return (q0, q1);
      }

      static void RefineBox (Point3 p0, Point3 p1, Point3 p2, ReadOnlySpan<Point3> pts, ref OBB best, ref double bestArea) {
         OBB obb = new (CoordSystem.Nil, Vector3.Zero);
         var u = (p1 - p0).Normalized ();
         var v = (p2 - p0).Normalized ();
         var w = u * v; if (w.IsZero) return;
         w = w.Normalized (); // The plane normal
         v = (w * u).Normalized ();

         ReadOnlySpan<Point3> tri = [p0, p1, p2];
         Span<double> min = stackalloc double[3], max = stackalloc double[3];
         // Iterate over all three triangle sides
         for (int pass = 0; pass < 3; pass++) {
            if (pass > 0) {
               (p0, p1, p2) = (tri[pass], tri[(pass + 1) % 3], tri[(pass + 2) % 3]);
               u = (p1 - p0).Normalized ();
               v = (w * u).Normalized ();
            }
            ReadOnlySpan<Vector3> axes = [u, v, w];
            // Compute OBB from orthogonal axes (u, v, w)
            ComputeProjections (pts, axes, min, max);
            // Half extents along the 3 axes
            var half = new Vector3 (max[0] - min[0], max[1] - min[1], max[2] - min[2]) * 0.5;
            if (Volume (half).IsZero ()) continue;
            var area = Area (half);
            if (area < bestArea) (best, bestArea) = (CreateBox (axes, min, max), area);
         }
      }
   }
   readonly static Vector3[] Axes = [new (1, 0, 0), new (0, 1, 0), new (0, 0, 1), new (1, 1, 1), new (1, 1, -1), new (1, -1, 1), new (1, -1, -1)];

}