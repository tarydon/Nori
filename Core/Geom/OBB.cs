// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBB.cs
// ║║║║╬║╔╣║ Implements minimum enclosing 'Orientend Bounding Box' in 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Represents a bounding box oriented along given axes.</summary>
public readonly struct OBB {
   public OBB (CoordSystem cs, Vector3 ext) => (CS, Extent) = (cs, ext);

   // The box center
   public Vector3 Center => (Vector3)CS.Org;
   // OBB origin and co-ordinate axes.
   public readonly CoordSystem CS;
   // The 'half extent' along the axes.
   public readonly Vector3 Extent;
   // The box area
   public double Area => 8 * (Extent.X * Extent.Y + Extent.X * Extent.Z + Extent.Y * Extent.Z);
   // The box volume
   public double Volume => 8 * (Extent.X * Extent.Y * Extent.Z);

   public static OBB Bruteforce (ReadOnlySpan<Point3> pts, ReadOnlySpan<int> triangles) {
      OBB best = new (CoordSystem.Nil, Vector3.Zero);
      double bestArea = double.MaxValue;
      // Iterate over candidate orientations from triangle normals
      for (int i = 0; i < triangles.Length; i += 3) {
         // The triangle space.
         Vector3 u = (pts[triangles[i + 1]] - pts[triangles[i]]).Normalized ();
         Vector3 v = (pts[triangles[i + 2]] - pts[triangles[i]]).Normalized ();
         Vector3 w = (u * v).Normalized ();
         v = w * u;
         ReadOnlySpan<Vector3> axes = [u, v, w];

         // Project points onto triangle axes
         Span<double> min = [double.MaxValue, double.MaxValue, double.MaxValue];
         Span<double> max = [double.MinValue, double.MinValue, double.MinValue];

         foreach (var p in pts) {
            for (int j = 0; j < 3; j++) {
               var a = axes[j];
               // Compute projection of point 'p' on axes (basically Dot (p, axes[j])
               double proj = p.X * a.X + p.Y * a.Y + p.Z * a.Z;
               if (proj < min[j]) min[j] = proj;
               if (proj > max[j]) max[j] = proj;
            }
         }

         // Compute half-lengths
         ReadOnlySpan<double> H = [(max[0] - min[0]) * 0.5, (max[1] - min[1]) * 0.5, (max[2] - min[2]) * 0.5];

         // Compute surface area (in fact 1/8th of the area)
         double area = H[0] * H[1] + H[1] * H[2] + H[2] * H[0];
         if (area < bestArea) {
            bestArea = area;
            Vector3 center = new (0, 0, 0);
            for (int j = 0; j < 3; j++) {
               double offset = (max[j] + min[j]) / 2.0;
               center += axes[j] * offset;
            }
            best = new OBB (new ((Point3)center, u, v), new (H[0], H[1], H[2]));
         }
      }
      return best;
   }
}