// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CollisionScene.cs
// ║║║║╬║╔╣║ Demonstrates the collision detections between the primitives
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;
using System.Diagnostics;

class CollisionScene : Scene3 {
   public CollisionScene () {
      BgrdColor = Color4.Gray (64);
      const int extent = 1500;
      Bound = (new Bound3 (0, 0, -extent / 5, extent, extent, extent * 4 / 5)).InflatedF (1.5);
      Lib.Tracer = TraceVN.Print;
      Build (150, extent);
   }

   // Build the scene 
   void Build (int shapes, double extent) {
      TraceVN.It.Clear ();
      Random R = new ();
      var obbs = new OBB[shapes];
      for (int i = 0; i < shapes; i++) {
         var vX = V ().Normalized (); var v = V ().Normalized ();
         var vY = vX * v;
         obbs[i] = new (new (P () * extent, vX, vY), V () * 100);
      }
      
      var collisions = new bool[shapes];
      Stopwatch sw = Stopwatch.StartNew (); sw.Start ();
      for (int i = 0; i < obbs.Length - 1; i++)
         for (int j = i + 1; j < obbs.Length; j++)
            //if (Collision.Check (obbs[i].Bound, obbs[j].Bound))
            if (Collision.Check (obbs[i], obbs[j]))
               collisions[i] = collisions[j] = true;
      sw.Stop ();

      Lib.Trace ($"Total: {shapes} objects, {collisions.Count (x => x)} collide. Elapsed: {S (sw.Elapsed)}");
      Lib.Trace ("Press 'Collision' button to rerun");

      List<VNode> nodes = [TraceVN.It];
      nodes.AddRange (obbs.Select ((x, n) => new BoxVN (x, collisions[n])));
      Root = new GroupVN (nodes);

      Point3 P () => new (Bias () * R.NextDouble (), Bias () * R.NextDouble (), R.NextDouble ());
      Vector3 V () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      double Bias () => R.NextDouble () switch { < 0.25 => -0.5, _ => 1.5 }; // Spreading out a little in X, Y for a better view
   }

   static string S (TimeSpan ts) => $"{ts.TotalMicroseconds:F0} us";

   // Draw OBB.
   class BoxVN (OBB box, bool collides) : VNode {
      readonly OBB Box = box;
      public bool Collides = collides;
      Vec3F[] Pts = [];
      Mesh3? Mesh;
      readonly Matrix3 Xfm = Matrix3.To (box.CS);

      public override void SetAttributes () =>
         (Lux.Color, Lux.Xfm, Lux.LineWidth) = (Collides ? Color4.Red : Color4.White, Xfm, 2);

      public override void Draw () {
         if (Mesh == null) BuildMesh ();
         Lux.Lines (Pts);
         if (Mesh != null) Lux.Mesh (Mesh, EShadeMode.Phong);
      }

      void BuildMesh () {
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
         Mesh = new Mesh3 ([.. nodes], [.. tries], []);
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
}