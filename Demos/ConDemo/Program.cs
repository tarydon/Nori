namespace ConDemo;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;

class Program {
   static void Main () {
      var summary = BenchmarkRunner.Run<TestIntersections> ();
      Console.WriteLine (summary);
   }
}

[MemoryDiagnoser (displayGenColumns:false)]
public class TestIntersections {
   public TestIntersections () {
      Lib.Init ();
      Lib.Tracer = Console.Write;

      // First, create a full mesh by combining all the meshes from all the surfaces
      Model3 model = new T3XReader ("N:/Demos/Data/5x-024-blank.t3x").Load ();
      List<Mesh3.Node> nodes = [];
      List<int> tris = [];
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         var mesh = ent.Mesh;
         int n = nodes.Count;
         nodes.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
      }
      mMesh = new ([.. nodes], [.. tris], []);
      var bound = mMesh.Bound;
   }
   int mStep = 1;
   Mesh3 mMesh;

   [Benchmark (Baseline = true)]
   public void PlaneMeshIntersector () {
      PlaneMeshIntersector pmi = new (mMesh);
      var bound = mMesh.Bound;
      for (int i = mStep; i < 1000; i += mStep) {
         double x = (i / 1000.0).Along (bound.X.Min, bound.X.Max);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var lines = pmi.Compute (pdef);

         double y = (i / 1000.0).Along (bound.Y.Min, bound.Y.Max);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         lines = pmi.Compute (pdef);
      }
   }

   [Benchmark]
   public void MeshSlicer () {
      MeshSlicer pmi = new (mMesh);
      var bound = mMesh.Bound;
      for (int i = mStep; i < 1000; i += mStep) {
         double x = (i / 1000.0).Along (bound.X.Min, bound.X.Max);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var lines = pmi.Slice (pdef);

         double y = (i / 1000.0).Along (bound.Y.Min, bound.Y.Max);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         lines = pmi.Slice (pdef);
      }
   }
} 