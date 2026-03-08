// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MeshScene.cs
// ║║║║╬║╔╣║ Demo that uses Lux.Mesh to draw a 3D mesh, with stencil lines
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;

using System.Diagnostics;
using Nori;

class MeshScene : Scene3 {
   public MeshScene (bool tessdemo = false) {
      TessDemo = tessdemo;
      Lib.Tracer = TraceVN.Print;
      TraceVN.HoldTime = 20;
      Root = new GroupVN ([new MeshVN (mMesh = MakeMesh ()), TraceVN.It]);
      BgrdColor = Color4.Gray (96);
      Bound = mMesh.Bound;
   }
   readonly bool TessDemo = false;

   Mesh3 MakeMesh () {
      if (!TessDemo) return Mesh3.LoadFluxMesh ($"{Lib.DevRoot}/Wad/FanucX/Model/R.mesh")!;

      // Tessellation demo makes a 'thick plane' from a Poly with holes.
      const double thk = 10;     // Plane thickness
      var dwg = DXFReader.Load ("N:/TData/Geom/Tess/J.dxf");
      var polys = dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly).ToList ();
      int outer = polys.MaxIndexBy (a => a.GetBound ().Area);

      List<int> splits = [0];
      List<Point2> tmp = [];
      using var td = Triangulator.Borrow (out var tess, ETolerance.Fine);
      Random r = new ();
      for (int i = 0; i < polys.Count; i++) {
         tmp.Clear (); 
         polys[i].Discretize (tmp, Lib.FineTess, Lib.FineTessAngle);
         for (int j = 0; j < tmp.Count; j++) 
            tmp[j] = tmp[j].Moved (r.NextDouble () * 1e-5, r.NextDouble () * 1e-5);
         polys[i] = Poly.Lines (tmp, true);
         tess.AddPoly (polys[i], i != outer);
         splits.Add (tess.Pts.Count);
      }
      var sw = Stopwatch.StartNew ();
      tess.Process ();
      sw.Stop ();
      Lib.Trace ($"{tess.Tris.Count / 3} triangles, {sw.Elapsed.TotalMilliseconds} ms");

      // Create bottom plane
      var pts = tess.Pts; var tris = tess.Tris;
      var nodes = tris.Select (n => (Point3)pts[n]).ToList ();

      // Add the top plane
      nodes.AddRange ([.. nodes.Select (x => x.WithZ (thk))]);
      // Make sidewalls from the polygon contours
      var span = pts.AsSpan ();
      for (int i = 1; i < splits.Count; i++) {
         var span2 = span[splits[i - 1]..splits[i]];
         for (int j = 1; j <= span2.Length; j++) {
            Point3 a = (Point3)span2[j - 1], b = (Point3)span2[j % span2.Length];
            Point3 c = a.WithZ (thk), d = b.WithZ (thk);
            nodes.AddRange (a, b, d, d, c, a);
         }
      }

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
