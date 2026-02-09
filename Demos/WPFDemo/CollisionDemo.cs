// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CollisionScene.cs
// ║║║║╬║╔╣║ Demonstrates the collision detections between the primitives
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;
using System.Diagnostics;
using System.Reactive.Linq;

class CollisionScene : Scene3 {
   public CollisionScene () {
      BgrdColor = Color4.Gray (64);
      const int extent = 1500;
      Bound = new Bound3 (0, 0, -extent / 5, extent, extent, extent * 4 / 5).InflatedF (1.6);
      Lib.Tracer = TraceVN.Print;
      Build (250, extent);
   }

   // Demo mode: 0=Box-Box, 1=Tri-Tri, 2=Tri-Tri-2D, 3=Box-Tri
   static int Mode = -1;

   // Build the scene 
   void Build (int shapes, float extent) {
      if (Mode < 0 || !HW.IsShiftDown) Mode = (Mode + 1) % 4;
      TraceVN.It.Clear ();
      Random R = new ();

      var (boxcnt, tricnt, test) = Mode switch {
         0 => (shapes, 0, "Box-Box"),
         1 => (shapes / 2, shapes / 2, "Box-Tri"),
         2 => (0, shapes, "Tri-Tri"),
         _ => (0, shapes, "Tri-Tri-2D"),
      };
      shapes = tricnt + boxcnt;

      var obbs = new OBB[boxcnt];
      for (int i = 0; i < obbs.Length; i++) {
         var vX = V ().Normalized (); var v = V ().Normalized ();
         var vY = (vX * v).Normalized ();
         obbs[i] = new (P () * extent, vX, vY, V () * 100);
      }

      static Point3f X (Point3f p) => new (p.X, p.Y, 300);
      CTri[] tris = new CTri[tricnt];
      List<Point3f> pts = [];
      for (int i = 0; i < tris.Length; i++) {
         var a = P () * extent; var b = a + V () * R.Next (200, 300);
         var c = a + V () * R.Next (200, 300);
         // Planar triangles
         if (Mode == 3) (a, b, c) = (X (a), X (b), X (c));
         int j = pts.Count; pts.AddRange (a, b, c);
         tris[i] = new (pts.AsSpan (), j, j + 1, j + 2);
      }

      var bcolls = new bool[obbs.Length];
      var tcolls = new bool[tris.Length];
      Point3f[] points = [.. pts];
      Stopwatch sw = Stopwatch.StartNew (); sw.Start ();
      switch (Mode) {
         case 0:
            for (int i = 0; i < obbs.Length - 1; i++)
               for (int j = i + 1; j < obbs.Length; j++)
                  if (Collision.Check (obbs[i], obbs[j]))
                     bcolls[i] = bcolls[j] = true;
            break;

         case 1:
            for (int i = 0; i < tris.Length; i++)
               for (int j = 0; j < obbs.Length; j++)
                  if (Collision.Check (points, tris[i], obbs[j]))
                     bcolls[j] = tcolls[i] = true;
            break;

         case 2: case 3:
            for (int i = 0; i < tris.Length - 1; i++)
               for (int j = i + 1; j < tris.Length; j++)
                  if (Collision.Check (points, tris[i], tris[j]))
                     tcolls[i] = tcolls[j] = true;
            break;

      }
      sw.Stop ();

      List<VNode> nodes = [TraceVN.It, new MinSphereScene.AxesVN ()];
      var boxNodes = obbs.Select ((x, n) => new BoxVN (x, bcolls[n]));
      var triNodes = tris.Select ((x, n) => new TriVN (points[x.A], points[x.B], points[x.C], tcolls[n]));
      if (HW.IsCtrlDown) {
         boxNodes = boxNodes.Where (x => x.Collides);
         triNodes = triNodes.Where (x => x.Collides);
      }
      nodes.AddRange (boxNodes); nodes.AddRange (triNodes);
      Root = new GroupVN (nodes);
      Lib.Trace ($"{test} test. Press 'Collision' to rerun ('Ctrl': Only collisions, 'Shift': Repeat '{test}')");
      Lib.Trace ($"Total: {shapes} objects, {bcolls.Count (x => x) + tcolls.Count (x => x)} collide. Elapsed: {S (sw.Elapsed)}");
      Lib.Trace ("");

      Point3f P () => new (Bias () * R.NextDouble (), Bias () * R.NextDouble (), R.NextDouble ());
      Vector3f V () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      double Bias () => R.NextDouble () switch { < 0.25 => -0.5, _ => 1.5 }; // Spreading out a little in X, Y for a better view
   }

   static string S (TimeSpan ts) => $"{ts.TotalMicroseconds:F0} us";

   // Draw OBB.
   class BoxVN (OBB box, bool collides) : VNode {
      public readonly OBB Box = box;
      public bool Collides = collides;
      public Mesh3 Mesh => mMesh ??= BuildMesh ();
      Mesh3? mMesh;
      Vec3F[] Pts = [];
      readonly Matrix3 Xfm = To (box);

      public override void SetAttributes () =>
         (Lux.Color, Lux.Xfm, Lux.LineWidth) = (Collides ? Color4.Magenta : Color4.White, Xfm, 2);

      public override void Draw () {
         if (Mesh == null) BuildMesh ();
         Lux.Lines (Pts);
         if (Mesh != null) Lux.Mesh (Mesh, EShadeMode.Flat);
      }

      Mesh3 BuildMesh () {
         List<Point3f> corners = [];
         var v = Box.Extent;
         for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
               for (int dz = -1; dz <= 1; dz += 2)
                  corners.Add (new (v.X * dx, v.Y * dy, v.Z * dz));

         // Fill box edges with center at the origin
         Pts = [.. Edges.Select (n => (Vec3F)corners[n])];

         // Build box mesh
         List<Mesh3.Node> nodes = []; List<int> tries = [];
         foreach (var f in Faces) {
            int idx = nodes.Count;
            ReadOnlySpan<Point3f> pts = [corners[f[0]], corners[f[1]], corners[f[2]], corners[f[3]]];
            Vec3H n = (Vec3H)((Vector3)(pts[1] - pts[0]) * (Vector3)(pts[2] - pts[0])).Normalized ();
            foreach (var p in pts) nodes.Add (new (p, n));
            tries.AddRange (idx, idx + 1, idx + 2);
            tries.AddRange (idx + 1, idx + 2, idx + 3);
         }
         return new Mesh3 ([.. nodes], [.. tries], []);
      }

      // Computes World-To-OBB transform.
      static Matrix3 To (OBB box) {
         var (x, y, z, t) = (box.X, box.Y, box.Z, box.Center);
         return new (x.X, x.Y, x.Z, y.X, y.Y, y.Z, z.X, z.Y, z.Z, t.X, t.Y, t.Z);
      }

      readonly static int[][] Faces =
         [[0, 1, 2, 3],
          [0, 1, 4, 5],
          [0, 4, 2, 6],
          [4, 6, 5, 7],
          [2, 3, 6, 7],
          [1, 3, 5, 7]];

      readonly static int[] Edges =
         [0,1,  0,2,  0,4,
          1,3,  1,5,
          2,3,  2,6,
          3,7,
          4,5,  4,6,
          5,7,  6,7];
   }

   // Draw Triangle
   class TriVN (Point3f a, Point3f b, Point3f c, bool collides) : VNode {
      public Point3f A = a, B = b, C = c;
      public bool Collides = collides;
      public Mesh3 Mesh => mMesh ??= BuildMesh ();
      private Mesh3? mMesh;

      public override void SetAttributes () =>
         (Lux.Color, Lux.LineWidth) = (Collides ? Color4.Magenta : Color4.Cyan, 2);

      public override void Draw () {
         Lux.Mesh (Mesh, EShadeMode.Flat);
         Lux.Lines ([(Vec3F)A, (Vec3F)B, (Vec3F)B, (Vec3F)C, (Vec3F)C, (Vec3F)A]);
      }

      Mesh3 BuildMesh () {
         List<Mesh3.Node> nodes = [];
         ReadOnlySpan<Point3f> pts = [A, B, C];
         for (int i = 0; i < pts.Length; i++) {
            var n = (Vec3H)((pts[(i + 1) % 3] - pts[i]) * (pts[(i + 2) % 3] - pts[i])).Normalized ();
            nodes.Add (new (pts[i], n));
         }
         return new Mesh3 ([.. nodes], [0, 1, 2], []);
      }
   }
}