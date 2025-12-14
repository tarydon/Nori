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
      Build ([..GeneratePoints (R, 50000, Bound.Width * R.Next (35, 65) / 100)]);
   }

   void Build (ReadOnlySpan<Point3> pts) {
      var s = MinSphere.From (pts);
      List<Sphere> models = [new (s.Radius, s.Center, Color4.Yellow, true), new (6, s.Center, Color4.Magenta)];
      double rMin = s.Radius - Lib.Epsilon, rMax = s.Radius + Lib.Epsilon;
      int nSurfacePts = 0;
      for (int i = 0; i < pts.Length; i++) {
         var pt = pts[i]; var d = pt.DistTo (s.Center);
         var (color, r) = d > rMax ? (Color4.Red, 6) : d < rMin ? (Color4.White, 3) : (Color4.Green, 5);
         models.Add (new (r, pt, color));
         if (color.Value == Color4.Green.Value) nSurfacePts++;
      }
      TraceVN.It.Clear ();
      Lib.Trace ($"Sphere, Radius: {s.Radius.Round (1)}, Center: ({s.Center.X.Round (1)}, {s.Center.Y.Round (1)}, {s.Center.Z.Round (1)})");
      Lib.Trace ($"Points: {pts.Length}, On Sphere: {nSurfacePts}");
      Lib.Trace ("Press 'Mininum Sphere' again to regenerate");

      List<VNode> nodes = [new AxesVN (), TraceVN.It];
      //// Approach 1: Add an individual node for each model.
      //nodes.AddRange (models.Select (s => new ModelVN (s, Matrix3.Identity)));

      //// Approach 2: Use the first node as reference.
      //foreach (var grp in models.GroupBy (m => (m.Radius, m.Color, m.Tranclucent))) {
      //   var first = grp.First (); var vn = new ModelVN (first, Matrix3.Identity); nodes.Add (vn);
      //   nodes.AddRange (grp.Skip (1).Select (s => new XfmVN (Matrix3.Translation (s.Center - first.Center), vn)));
      //}

      // Approach 3: Use the first model as reference.
      foreach (var grp in models.GroupBy (m => (m.Radius, m.Color, m.Tranclucent))) {
         var first = grp.First (); nodes.Add (new ModelVN (first, Matrix3.Identity));
         nodes.AddRange (grp.Skip (1).Select (s => new ModelVN (first, Matrix3.Translation (s.Center - first.Center))));
      }
      Root = new GroupVN (nodes);
   }

   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      // Generate random points within a cube if size 'size'.
      Point3 ptRef = new (R.NextDouble () * size, R.NextDouble () * size, R.NextDouble () * size);
      for (int i = 0; i < count; i++) {
         var vec = new Vector3 (R.NextDouble (), R.NextDouble (), R.NextDouble ()) * size;
         yield return ptRef + vec;
      }
   }

   // Draw axis lines.
   class AxesVN : VNode {
      public override void SetAttributes () => Lux.Color = Color4.Black;
      public override void Draw () => Lux.Lines ([Zero, new (100, 0, 0), Zero, new (0, 100, 0), Zero, new (0, 0, 100)]);
      readonly Vec3F Zero = new ();
   }

   class ModelVN (Sphere s, Matrix3 xfm) : VNode {
      public override void SetAttributes () {
         Lux.Color = S.Color;
         Lux.Xfm = Xfm;
      }
      public override void Draw () => Lux.Mesh (S.Mesh, S.Tranclucent ? EShadeMode.Glass : EShadeMode.Phong);
      readonly Sphere S = s;
      readonly Matrix3 Xfm = xfm;
   }

   class Sphere (double radius, Point3 center, Color4 color, bool translucent = false) {
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
               // ___a__d___
               // |\ |\ |\ |
               // |_\|_\|_\|
               //    b  c
               if (i > 0 && j > 0) {
                  int a = (i - 1) * rows + (j - 1), d = a + 1;
                  int b = i * rows + (j - 1), c = b + 1;
                  // For the first and the last latitude, at poles, only one triangle per segment.
                  if (i < lats) tries.AddRange ([a, b, c]);
                  if (i > 1) tries.AddRange ([c, d, a]);
               }
            }
         }
         return new ([.. nodes], [.. tries], []);
      }
   }
}
