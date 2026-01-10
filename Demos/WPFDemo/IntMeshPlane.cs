using Nori;
namespace WPFDemo;

class IntMeshPlaneScene : Scene3 {
   public IntMeshPlaneScene () {
      Model3 model = new T3XReader ("N:/Demos/Data/5x-024-blank.t3x").Load ();

      List<Mesh3> meshes = [];
      List<Mesh3.Node> nodes = [];
      List<int> tris = [], wires = [];
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         if (ent.Id == 1) ent.IsSelected = true;
         var mesh = ent.Mesh;
         meshes.Add (mesh);
         int n = nodes.Count; 
         nodes.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
         wires.AddRange (mesh.Wire.Select (a => a + n));
      }
      wires.Clear ();

      Lib.Tracer = TraceVN.Print;      
      Mesh3 fullmesh = new ([.. nodes], [.. tris], [.. wires]);
      List<VNode> vnodes = [
         new Model3VN (model),
         // new MeshVN (fullmesh) { Color = Color4.White, Shading = EShadeMode.Phong }, 
         TraceVN.It
      ];
      Bound = fullmesh.Bound;

      // AddIntersections ([fullmesh], Bound, vnodes, 50);
      AddIntersections (meshes, Bound, vnodes, 25);
      BgrdColor = new Color4 (32, 64, 96);
      Root = new GroupVN (vnodes);
   }

   void AddIntersections (IList<Mesh3> meshes, Bound3 bound, List<VNode> vnodes, int step) {
      using var bt = new BlockTimer ("Compute Intersections");
      PlaneMeshIntersector pmi = new (meshes);
      List<Vec3F> ends = [];
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (bound.X.Min, bound.X.Max);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         foreach (var poly in pmi.Compute (pdef)) {
            vnodes.Add (new Curve3VN (poly));
            ends.Add ((Vec3F)poly.Start); ends.Add ((Vec3F)poly.End);
         }
         break;

         double y = (i / 100.0).Along (Bound.Y.Min, Bound.Y.Max);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         foreach (var poly in pmi.Compute (pdef)) {
            vnodes.Add (new Curve3VN (poly));
            ends.Add ((Vec3F)poly.Start); ends.Add ((Vec3F)poly.End);
         }
      }
      vnodes.Add (new SimpleVN (
         () => { Lux.PointSize = 7f; Lux.Color = Color4.Yellow; },
         () => Lux.Points (ends.AsSpan ())
      ));
   }
}
