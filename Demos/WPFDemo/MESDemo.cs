// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MinSphereScene.cs
// ║║║║╬║╔╣║ Demonstrates the minimum enclosing circle/sphere algorithm
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using static Math;
using Nori;
using System.Diagnostics;

class MinSphereScene : Scene3 {
   public MinSphereScene () {
      BgrdColor = Color4.Gray (96);
      Bound = new (0, 0, 0, 800, 800, 800);
      Lib.Tracer = TraceVN.Print; Random R = new ();
      Build ([.. GeneratePoints (R, 10000, Bound.Width * R.Next (35, 65) / 100)]);
   }

   // Build the scene with the minimum enclosing sphere for given points.
   void Build (Point3[] pts) {
      Stopwatch sw = Stopwatch.StartNew (); sw.Start ();
      var s = MinSphere.From (pts); // Compute minimum enclosing sphere
      sw.Stop ();
      MeshVN sphere = new (BuildMesh (s.Radius, s.Center)) { Color = Color4.Yellow, Shading = EShadeMode.Glass };
      List<VNode> nodes = [new AxesVN (), TraceVN.It, sphere, new PointsVN ([s.Center], Color4.Magenta, 6)];
      (Color4 Clr, float Size)[] props = [(Color4.Green, 8), (Color4.White, 3), (Color4.Red, 8)];
      (Point3 Pt, int N)[] ptlie = [.. pts.Select (pt => (pt, d: pt.DistTo (s.Center))).Select (x => (x.pt, x.d.EQ (s.Radius) ? 0 : x.d < s.Radius ? 1 : 2))];
      nodes.AddRange (ptlie.GroupBy (x => x.N).Select (g => new PointsVN (g.Select (x => x.Pt), props[g.Key].Clr, props[g.Key].Size)));
      TraceVN.It.Clear ();
      Lib.Trace ($"Sphere, Radius: {s.Radius.Round (1)}, Center: ({s.Center.X.Round (1)}, {s.Center.Y.Round (1)}, {s.Center.Z.Round (1)})");
      Lib.Trace ($"Points: {pts.Length}, On Sphere: {ptlie.Count (x => x.N == 0)}, Elapsed: {sw.ElapsedMilliseconds} ms");
      Lib.Trace ("Press 'Min. Sphere' again to regenerate");
      Root = new GroupVN (nodes);
   }

   // Generate random points within a cube if size 'size'.
   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      Point3 anchor = P () * size;
      return Enumerable.Range (0, count).Select (_ => anchor + P () * size);
      Point3 P () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
   }

   // Build a sphere mesh with given radius and center.
   static Mesh3 BuildMesh (double radius, Point3 center) {
      List<Mesh3.Node> nodes = []; List<int> tries = [];
      // Define the latitude and longitude counts for the sphere.
      var (lats, longs) = (36, 72); int rows = longs + 1;
      // Steps in north and east.
      double nstep = PI / lats, estep = Tau / longs, north = 0;
      for (int i = 0; i <= lats; i++, north += nstep) {
         var (dz, r, east) = (radius * Cos (north), radius * Sin (north), 0.0);
         for (int j = 0; j <= longs; j++, east += estep) {
            // Add mesh node.
            Vector3 vec = new (r * Cos (east), r * Sin (east), dz);
            nodes.Add (new (center + vec, vec.Normalized ()));
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

   // Draw axis lines.
   class AxesVN : VNode {
      public override void SetAttributes () => Lux.Color = Color4.White;
      public override void Draw () => Lux.Lines ([Org, new (100, 0, 0), Org, new (0, 100, 0), Org, new (0, 0, 100)]);
      readonly static Vec3F Org = new ();
   }

   // Draw points cloud
   class PointsVN (IEnumerable<Point3> pts, Color4 clr, float size) : VNode {
      public override void SetAttributes () => (Lux.Color, Lux.PointSize) = (Color, Size);
      public override void Draw () => Lux.Points (Pts);
      readonly Vec3F[] Pts = [.. pts.Select (p => (Vec3F)p)];
      readonly Color4 Color = clr;
      readonly float Size = size;
   }
}