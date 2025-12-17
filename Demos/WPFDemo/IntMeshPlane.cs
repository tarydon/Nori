using Nori;
namespace WPFDemo;

class IntMeshPlaneScene : Scene3 {
   public IntMeshPlaneScene () {
      Model3 model = new T3XReader ("N:/Demos/Data/5x-024-blank.t3x").Load ();
      List<Mesh3.Node> nodes = [];
      List<int> tris = [], wires = [];
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         var mesh = ent.Mesh;
         int n = nodes.Count; 
         nodes.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
         wires.AddRange (mesh.Wire.Select (a => a + n));
      }
      wires.Clear ();

      Lib.Tracer = TraceVN.Print;      
      Mesh3 fullmesh = new ([.. nodes], [.. tris], [.. wires]);
      List<VNode> vnodes = [new MeshVN (fullmesh) { Color = Color4.White, Shading = EShadeMode.Glass }, TraceVN.It];
      Bound = fullmesh.Bound;

      int slices = 20;
      // AddIntersections (fullmesh, vnodes, slices);
      AddIntersections2 (fullmesh, vnodes, slices);
      BgrdColor = new Color4 (32, 64, 96);
      Root = new GroupVN (vnodes);
   }

   void AddIntersections2 (Mesh3 mesh, List<VNode> nodes, int step) {
      using var bt = new BlockTimer ("Slice Mesh");
      MeshSlicer pmi = new (mesh);
      var bound = mesh.Bound;
      for (int i = step; i <= 100; i += step) {
         double x = (i / 100.0).Along (bound.X.Min, bound.X.Max);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var pts = pmi.Slice (pdef);
         nodes.Add (new LinesVN ([.. pts.Select (a => (Vec3F)a)]));

         double y = (i / 100.0).Along (Bound.Y.Min, Bound.Y.Max);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         pts = pmi.Slice (pdef);
         nodes.Add (new LinesVN ([.. pts.Select (a => (Vec3F)a)]));
      }
   }

   void AddIntersections (Mesh3 mesh, List<VNode> curves, int step) {
      using var bt = new BlockTimer ("Compute Intersections");
      PlaneMeshIntersector pmi = new (mesh);
      var bound = mesh.Bound;
      for (int i = step; i <= 100; i += step) {
         double x = (i / 100.0).Along (bound.X.Min, bound.X.Max);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         foreach (var lines in pmi.Compute (pdef))
            curves.Add (new Curve3VN (new Polyline3 (0, lines)));

         double y = (i / 100.0).Along (Bound.Y.Min, Bound.Y.Max);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         foreach (var lines in pmi.Compute (pdef))
            curves.Add (new Curve3VN (new Polyline3 (0, lines)));
      }
   }
}

class LinesVN (List<Vec3F> pts) : VNode (pts) {
   public override void Draw () => Lux.Lines (pts.AsSpan ());
}