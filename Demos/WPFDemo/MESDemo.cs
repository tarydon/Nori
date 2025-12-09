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
      Root = VN = new RootVN ();
      Refresh ();
   }

   void Refresh () {
      Random R = new ();
      // Generate random points within a cube.
      int maxpts = 500; double size = Bound.Width / 2;
      List<Point3> pts = [];
      Point3 ptRef = new (R.NextDouble () * size, R.NextDouble () * size, R.NextDouble () * size);
      for (int i = 0; i < maxpts; i++) {
         var vec = new Vector3 (R.NextDouble (), R.NextDouble (), R.NextDouble ()) * size;
         pts.Add (ptRef + vec);
      }

      var s = MinSphere.From (pts.AsSpan ());
      List<Sphere> models = [new (s.Radius, s.Center, Color4.Yellow, true), new (6, s.Center, Color4.Magenta)];
      double rMin = s.Radius - Lib.Epsilon, rMax = s.Radius + Lib.Epsilon;
      for (int i = 0; i < pts.Count; i++) {
         var pt = pts[i]; var d = pt.DistTo (s.Center);
         var (color, r) = d > rMax ? (Color4.Red, 6) : d < rMin ? (Color4.Blue, 4) : (Color4.Green, 6);
         models.Add (new (r, pt, color));
      }
      VN.Set (models);
   }
   RootVN VN;

   struct Sphere (double radius, Point3 center, Color4 color, bool translucent = false) {
      public readonly double Radius = radius;
      public readonly Point3 Center = center;
      public readonly Color4 Color = color;
      public readonly bool Tranclucent = translucent;

      public Mesh3 Mesh => mMesh ??= BuildMesh (Radius, Center, Radius > 10);
      Mesh3? mMesh;

      static Mesh3 BuildMesh (double radius, Point3 center, bool hires) {
         List<Mesh3.Node> nodes = []; List<int> tries = [];
         // Define the latitude and longitude counts for the sphere.
         var (lats, longs) = hires ? (36, 72) : (6, 12);
         // Steps in north and east.
         double nstep = PI / lats, estep = Tau / longs, north = 0;
         int rows = longs + 1;
         for (int i = 0; i <= lats; i++, north += nstep) {
            var theta = i * nstep;
            var dz = radius * Cos (theta);
            var rad = radius * Sin (theta);
            double east = 0;
            for (int j = 0; j <= longs; j++, east += estep) {
               var dx = rad * Cos (east);
               var dy = rad * Sin (east);
               var vec = new Vec3H ((Half)(dx / radius), (Half)(dy / radius), (Half)(dz / radius));
               nodes.Add (new (new (center.X + dx, center.Y + dy, center.Z + dz), vec));
               if (i > 0 && j > 0) {
                  int a = (i - 1) * rows + (j - 1), d = a + 1;
                  int b = i * rows + (j - 1), c = b + 1;
                  tries.AddRange ([a, b, c]);
                  // For the first and the last latitude, only one triangle per segment.
                  if (i > 1 && i < longs)
                     tries.AddRange ([c, d, a]);
               }
            }

         }
         return new Mesh3 ([.. nodes], [.. tries], []);
      }
   }

   class RootVN : VNode {
      public void Set (List<Sphere> models) => mModels = models;

      public override VNode? GetChild (int n) {
         if (mModels == null || n >= mModels.Count) return null;
         return new SphereVN (mModels[n]);
      }

      List<Sphere>? mModels;
   }

   class SphereVN (Sphere s) : VNode {
      public override void SetAttributes () => Lux.Color = S.Color;
      public override void Draw () => Lux.Mesh (S.Mesh, S.Tranclucent ? EShadeMode.Glass : EShadeMode.Phong);

      readonly Sphere S = s;
   }
}
