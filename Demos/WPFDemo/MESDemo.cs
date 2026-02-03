// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MinSphereScene.cs
// ║║║║╬║╔╣║ Demonstrates the minimum enclosing circle/sphere algorithm
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;
using System.Diagnostics;

class MinSphereScene : Scene3 {
   public MinSphereScene () {
      BgrdColor = Color4.Gray (96);
      Bound = new (0, 0, 0, 1000, 1000, 1000);
      Lib.Tracer = TraceVN.Print; Random R = new ();
      Build ([.. GeneratePoints (R, 10000, Bound.Width)]);
   }

   // Build the scene with the minimum enclosing sphere for given points.
   void Build (Point3[] pts) {
      TraceVN.It.Clear ();
      // Compute minimum enclosing sphere
      Stopwatch sw = Stopwatch.StartNew (); sw.Start ();
      var s = MinSphere.From (pts);
      sw.Stop ();
      (Point3 Pt, int N)[] ptlie = [.. pts.Select (pt => (pt, d: pt.DistTo (s.Center))).Select (x => (x.pt, x.d.EQ (s.Radius) ? 0 : x.d < s.Radius ? 1 : 2))];
      MeshVN sphere = new (Mesh3.Sphere (s.Center, s.Radius)) { Shading = EShadeMode.Glass };
      List<VNode> nodes = [new AxesVN (), TraceVN.It, sphere, new PointsVN ([s.Center], (Color4.Magenta, 6))];
      nodes.AddRange (ptlie.GroupBy (x => x.N).Select (g => new PointsVN (g.Select (x => x.Pt), Styles[g.Key])));
      Lib.Trace ($"Min-Sphere, Radius: {S (s.Radius)}, Center: {S (s.Center)}");
      Lib.Trace ($"Points: {pts.Length}, On Sphere: {ptlie.Count (x => x.N == 0)}, Elapsed: {S (sw.Elapsed)}");
      // Compute approximate enclosing sphere by Ritter's algorithm for comparison
      sw.Restart ();
      var s2 = MinSphere.FromQuickApprox (pts);
      sw.Stop ();
      Lib.Trace ($"Approx-Sphere, Radius: {S (s2.Radius)}, Center: {S (s2.Center)}");
      Lib.Trace ($"Deviation: {((s2.Radius - s.Radius) + (s2.Center - s.Center).Length) / s.Radius:P2}, Elapsed: {S (sw.Elapsed)}");
      Lib.Trace ($"");
      // OBB demo
      Point3f[] ptsF = [.. pts.Select (x => (Point3f)x)];
      sw.Restart ();
      var obb = OBB.From (ptsF);
      sw.Stop ();
      nodes.Add (new BoxVN (obb));
      Bound3 aabb = new (pts);
      var vol = obb.Volume;
      var abvol = aabb.Width * aabb.Height * aabb.Depth;
      var svol = 4 / 3 * Lib.PI * Math.Pow (s.Radius, 3);
      Lib.Trace ($"OBB: {S ((Point3)obb.Center)}, Size: {S (obb.Extent.Length)}, Vol.: ({vol / svol:P0} of Sphere, {vol / abvol:P0} of AABB), Elapsed: {S (sw.Elapsed)}");
      Lib.Trace ("Press 'Min. Sphere' again to regenerate");
      Root = new GroupVN (nodes);
   }
   readonly static (Color4 Clr, float Size)[] Styles = [(Color4.Green, 8), (Color4.White, 3), (Color4.Red, 8)];

   // Generate random points within a randomly rotated cuboid of maximum size 'size'.
   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      double half = size * 0.5, fsize = size * 0.01;
      var (w, h, d) = (Span (), Span (), Span ());
      Bound3 bound = new (-w, -h, -d, w, h, d);
      Matrix3 xfm = Matrix3.Rotation (V (), R.NextDouble () * Math.PI); // Apply rotation
      xfm *= Matrix3.Translation (V () * half); // Move to anchor
      int i = 0;
      do {
         var pt = P () * half;
         if (!bound.Contains (pt)) continue;
         i++;
         yield return pt * xfm;
      } while (i < count);
      // Helpers
      Point3 P () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      Vector3 V () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      double Span () => R.Next (5, 95) * fsize; // [5-95] %
   }

   static string S (double f) => $"{f:F1}";
   static string S (Point3 p) => $"({p.X:F1},{p.Y:F1},{p.Z:F1})";
   static string S (Vector3 v) => $"<{v.X:F1},{v.Y:F1},{v.Z:F1}>";
   static string S (TimeSpan ts) => $"{ts.TotalMicroseconds:F0} us";

   // View Nodes
   // Draw axis lines.
   class AxesVN : VNode {
      public override void SetAttributes () => Lux.Color = Color4.White;
      public override void Draw () => Lux.Lines ([Org, new (100, 0, 0), Org, new (0, 100, 0), Org, new (0, 0, 100)]);
      readonly static Vec3F Org = new ();
   }

   // Draw OBB.
   class BoxVN (OBB box) : VNode {
      readonly OBB Box = box;
      Vec3F[] Pts = [];
      readonly Matrix3 Xfm = Matrix3.To (new CoordSystem ((Point3)box.Center, (Vector3)box.X, (Vector3)box.Y));

      public override void SetAttributes () =>
         (Lux.Color, Lux.Xfm, Lux.LineWidth) = (Color4.White, Xfm, 2);

      public override void Draw () {
         if (Pts.Length == 0) {
            List<Vec3F> corners = [];
            var (ex, ey, ez) = (Box.Extent.X, Box.Extent.Y, Box.Extent.Z);
            for (int dx = -1; dx <= 1; dx += 2)
               for (int dy = -1; dy <= 1; dy += 2)
                  for (int dz = -1; dz <= 1; dz += 2)
                     corners.Add (new (ex * dx, ey * dy, ez * dz));

            // Fill box edges with center at the origin
            Pts = [.. Edges.Select (n => corners[n])];
         }
         Lux.Lines (Pts);
      }

      readonly static int[] Edges =
         [0,1,  0,2,  0,4,
          1,3,  1,5,
          2,3,  2,6,
          3,7,
          4,5,  4,6,
          5,7,  6,7];
   }

   // Draw points cloud
   class PointsVN (IEnumerable<Point3> pts, (Color4 Clr, float Size) style) : VNode {
      public override void SetAttributes () => (Lux.Color, Lux.PointSize) = Style;
      public override void Draw () => Lux.Points (Pts);
      readonly Vec3F[] Pts = [.. pts.Select (p => (Vec3F)p)];
      readonly (Color4, float) Style = style;
   }
}