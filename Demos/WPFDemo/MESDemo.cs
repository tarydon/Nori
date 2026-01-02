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
      Bound = new (0, 0, 0, 800, 800, 800);
      Lib.Tracer = TraceVN.Print; Random R = new ();
      Build ([.. GeneratePoints (R, 10000, Bound.Width * R.Next (35, 65) / 100)]);
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
      Lib.Trace ($"Min-Sphere, Radius: {s.Radius.Round (1)}, Center: ({s.Center.X.Round (1)}, {s.Center.Y.Round (1)}, {s.Center.Z.Round (1)})");
      Lib.Trace ($"Points: {pts.Length}, On Sphere: {ptlie.Count (x => x.N == 0)}, Elapsed: {sw.Elapsed.TotalMicroseconds:F0} us");

      // Compute approximate enclosing sphere by Ritter's algorithm for comparison
      sw.Restart ();
      var s2 = MinSphere.FromQuickApprox (pts);
      sw.Stop ();
      Lib.Trace ($"Approx-Sphere, Radius: {s2.Radius.Round (1)}, Center: ({s2.Center.X.Round (1)}, {s2.Center.Y.Round (1)}, {s2.Center.Z.Round (1)}), Elapsed: {sw.Elapsed.TotalMicroseconds:F0} us");
      Lib.Trace ($"Deviation: {((s2.Radius - s.Radius) / s.Radius):P2}");

      Lib.Trace ("Press 'Min. Sphere' again to regenerate");
      Root = new GroupVN (nodes);
   }
   readonly static (Color4 Clr, float Size)[] Styles = [(Color4.Green, 8), (Color4.White, 3), (Color4.Red, 8)];

   // Generate random points within a cube if size 'size'.
   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      Point3 anchor = P () * size;
      return Enumerable.Range (0, count).Select (_ => anchor + P () * size);
      Point3 P () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
   }

   // Draw axis lines.
   class AxesVN : VNode {
      public override void SetAttributes () => Lux.Color = Color4.White;
      public override void Draw () => Lux.Lines ([Org, new (100, 0, 0), Org, new (0, 100, 0), Org, new (0, 0, 100)]);
      readonly static Vec3F Org = new ();
   }

   // Draw points cloud
   class PointsVN (IEnumerable<Point3> pts, (Color4 Clr, float Size) style) : VNode {
      public override void SetAttributes () => (Lux.Color, Lux.PointSize) = Style;
      public override void Draw () => Lux.Points (Pts);
      readonly Vec3F[] Pts = [.. pts.Select (p => (Vec3F)p)];
      readonly (Color4, float) Style = style;
   }
}