// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MeshScene.cs
// ║║║║╬║╔╣║ Demo that uses Lux.Mesh to draw a 3D mesh, with stencil lines
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;

class MeshScene : Scene3 {
   public MeshScene (bool tessdemo = false) {
      TessDemo = tessdemo;
      Root = new MeshVN (mMesh = MakeMesh ());
      BgrdColor = Color4.Gray (96);
      Bound = mMesh.Bound;
   }
   readonly bool TessDemo = false;

   Mesh3 MakeMesh () {
      if (!TessDemo) return Mesh3.LoadFluxMesh ($"{Lib.DevRoot}/Wad/FanucX/Model/R.mesh")!;
      // Tessellation demo makes a 'thick plane' from a Poly with holes.
      const double thk = 10;     // Plane thickness
      // 1. Create a flat with an outer contour and inner holes.
      List<Poly> polys = [
         // 1.a. Make outer contour
         Poly.Parse ("M0,0H500V200Q400,300,-1H100Q0,200,1Z"),
         // 1.b. Add inner contours
         Poly.Circle ((80, 80), 60),
         Poly.Circle ((450, 70), 20),
         Poly.Circle ((250, 120), 20),
         Poly.Rectangle (160, 160, 180, 180),
         Poly.Rectangle (170, 20, 280, 40),
         Poly.Polygon ((350, 150), 30, 6),
         Poly.Polygon ((350, 50), 20, 5),
         Poly.Polygon ((50, 250), 20, 3),
         Poly.Circle ((250, 250), 20),
      ];

      // 2. Make tessellation inputs
      List<Point2> pts = []; List<int> splits = [0];
      foreach (var poly in polys) {
         poly.Discretize (pts, 0.1, 0.5411);
         splits.Add (pts.Count);
      }

      // 3. Tessellate the polygon into triangles
      var tries = Tess2D.Process (pts, splits);

      // 4. Create a thick plane from triangles and contours
      // 4.a. Make the bottom plane from triangles at Z=0
      var nodes = tries.Select (n => (Point3)pts[n]).ToList ();
      // 4.b. Shift the bottom plane by thickness in Z to make the top plane
      nodes.AddRange ([.. nodes.Select (x => x.WithZ (thk))]);
      // 4.c. Make side walls from the polygon contours
      var span = pts.AsSpan ();
      for (int i = 1; i < splits.Count; i++) {
         var span2 = span[splits[i - 1]..splits[i]];
         for (int j = 1; j <= span2.Length; j++) {
            Point3 a = (Point3)span2[j - 1], b = (Point3)span2[j % span2.Length];
            Point3 c = a.WithZ (thk), d = b.WithZ (thk);
            nodes.AddRange (a, b, d, d, c, a);
         }
      }

      // 5. Now build the mesh.
      return new Mesh3Builder (nodes.AsSpan ()).Build ();
   }
   Mesh3 mMesh;
}

class MeshVN (Mesh3 mesh) : VNode {
   public override void SetAttributes () { Lux.Color = Color; Lux.LineWidth = 2f; }
   public override void Draw () => Lux.Mesh (mesh, Shading);

   public Color4 Color = new (255, 255, 128);
   public EShadeMode Shading = EShadeMode.Phong;
}
