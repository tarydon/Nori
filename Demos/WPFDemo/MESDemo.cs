// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MinSphereScene.cs
// ║║║║╬║╔╣║ Demonstrates the minimum enclosing circle/sphere algorithm
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using static Math;
using Nori;

class MinSphereScene : Scene3 {
   public MinSphereScene () {
      BgrdColor = Color4.Gray (96);
      Bound = new Bound3 (0, 0, 0, 800, 800, 800);
      Lib.Tracer = TraceVN.Print;
      Random R = new ();
      Build ([..GeneratePoints (R, 500, Bound.Width * R.Next (35, 65) / 100)]);
   }

   void Build (ReadOnlySpan<Point3> pts) {
      var s = MinSphere.From (pts);
      List<Sphere> models = [new (s.Radius, s.Center, Color4.Yellow, true), new (6, s.Center, Color4.Magenta)];
      double rMin = s.Radius - Lib.Epsilon, rMax = s.Radius + Lib.Epsilon;
      int nSurfacePts = 0;
      for (int i = 0; i < pts.Length; i++) {
         var pt = pts[i]; var d = pt.DistTo (s.Center);
         var (color, r) = d > rMax ? (Color4.Red, 6) : d < rMin ? (Color4.White, 3) : (Color4.Green, 5);
         // Skip some of the internal points if there are too many of them.
         if (pts.Length > 50000 && d < rMin) continue;
         models.Add (new (r, pt, color));
         if (color.Value == Color4.Green.Value) nSurfacePts++;
      }
      TraceVN.It.Clear ();
      Lib.Trace ($"Sphere, Radius: {s.Radius.Round (1)}, Center: ({s.Center.X.Round (1)}, {s.Center.Y.Round (1)}, {s.Center.Z.Round (1)})");
      Lib.Trace ($"Points: {pts.Length}, On Sphere: {nSurfacePts}");
      Lib.Trace ("Press 'Mininum Sphere' again to regenerate");
      Root = new GroupVN ([new RootVN (models), TraceVN.It]);
   }

   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      // Generate random points within a cube if size 'size'.
      Point3 ptRef = new (R.NextDouble () * size, R.NextDouble () * size, R.NextDouble () * size);
      for (int i = 0; i < count; i++) {
         var vec = new Vector3 (R.NextDouble (), R.NextDouble (), R.NextDouble ()) * size;
         yield return ptRef + vec;
      }
   }

   class RootVN (List<Sphere> models) : VNode {
      public override void SetAttributes () => Lux.Color = Color4.Black;
      public override VNode? GetChild (int n) => n >= Models.Count ? null : new ModelVN (Models[n]);

      public override void Draw () {
         base.Draw (); Vec3F org = new ();
         // Draw axis lines.
         Lux.Lines ([org, new (100, 0, 0), org, new (0, 100, 0), org, new (0, 0, 100)]);
      }

      readonly List<Sphere> Models = models;
   }

   class ModelVN (Sphere s) : VNode {
      public override void SetAttributes () => Lux.Color = S.Color;
      public override void Draw () => Lux.Mesh (S.Mesh, S.Tranclucent ? EShadeMode.Glass : EShadeMode.Phong);
      readonly Sphere S = s;
   }

   struct Sphere (double radius, Point3 center, Color4 color, bool translucent = false) {
      public readonly double Radius = radius;
      public readonly Point3 Center = center;
      public readonly Color4 Color = color;
      public readonly bool Tranclucent = translucent;

      public Mesh3 Mesh => mMesh ??= BuildMesh (Radius, Center, Radius <= 10);
      Mesh3? mMesh;

      static Mesh3 BuildMesh (double radius, Point3 center, bool coarse) {
         List<Mesh3.Node> nodes = []; List<int> tries = [];
         // Define the latitude and longitude counts for the sphere.
         var (lats, longs) = coarse ? (6, 12) : (36, 72);
         // Steps in north and east.
         double nstep = PI / lats, estep = Tau / longs, north = 0;
         int rows = longs + 1;
         for (int i = 0; i <= lats; i++, north += nstep) {
            var (dz, r) = (radius * Cos (north), radius * Sin (north));
            double east = 0;
            for (int j = 0; j <= longs; j++, east += estep) {
               var (dx, dy) = (r * Cos (east), r * Sin (east));
               var vec = new Vec3H ((Half)(dx / radius), (Half)(dy / radius), (Half)(dz / radius));
               // Add mesh node.
               nodes.Add (new (new (center.X + dx, center.Y + dy, center.Z + dz), vec));
               // Add triangles. Each segment between latitudes consists of two triangles.
               // ______
               // |\   |
               // | \  |
               // |  \ |
               // |___\|
               if (i > 0 && j > 0) {
                  int a = (i - 1) * rows + (j - 1), d = a + 1;
                  int b = i * rows + (j - 1), c = b + 1;
                  tries.AddRange ([a, b, c]);
                  // For the first and the last latitude, at poles, only one triangle per segment.
                  if (i > 1 && i < longs)
                     tries.AddRange ([c, d, a]);
               }
            }
         }
         return new ([.. nodes], [.. tries], []);
      }
   }
}
