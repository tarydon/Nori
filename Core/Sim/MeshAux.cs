// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MeshAux.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public partial class Mesh3 {
   // Methods ------------------------------------------------------------------
   /// <summary>Create a mesh by extruding a Poly</summary>
   public static Mesh3 Extrude (Poly[] poly, double thickness, Matrix3 xfm) {
      List<Point2> pts = [];
      List<int> splits = [0];
      foreach (var p in poly) {
         p.Discretize (pts, Lib.CoarseTess, Lib.CoarseTess);
         splits.Add (pts.Count);
      }
      var tris = Lib.Tessellate (pts, splits);

      // Reserve space for the nodes, and create the bottom and top planes first
      int n = pts.Count;
      List<Point3> raw = [];
      // Add bottom and top plane nodes
      for (int i = 0; i < n; i++) raw.Add ((Point3)pts[i]);   
      for (int i = 0; i < n; i++) raw.Add (((Point3)pts[i]).Moved (0, 0, thickness));  
      // Bottom plane triangles are already added, add the top plane triangles as well
      for (int i = 0, max = tris.Count; i < max; i += 3)
         tris.AddM ([tris[i + 2] + n, tris[i + 1] + n, tris[i] + n]);
      // Now, the sidewalls 
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         tris.AddM ([i, j, j + n, i, j + n, i + n]);
      }

      var vertex = tris.Select (a => raw[a] * xfm).ToArray ();
      return new Mesh3Builder (vertex).Build ();
   }

   /// <summary>Builds a sphere mesh centered at 'center' with the specified 'radius'/>.
   /// The generated sphere mesh consists of triangles of uniform size. The number of output 
   /// triangles, and the accuracy of the mesh relative to the spherical surface, are determined 
   /// by the 'tolerance' parameter, which defines the allowable _relative deviation_ of a 
   /// triangle from the ideal sphere.
   /// <remarks>
   /// This method employs polyhedron-based subdivision to produce equilateral triangles. Each subdivision
   /// step replaces one triangle with four smaller triangles. The number of subdivisions performed is
   /// controlled by the tolerance value.
   /// </remarks>
   /// <param name="center">Center of the sphere.</param>
   /// <param name="radius">Radius of the sphere.</param>
   /// <param name="tolerance">Percentage mismatch (default is 0.1%)</param>
   public static Mesh3 Sphere (Point3 center, double radius, double tolerance = 0.001) {
      // All computations are done on 'unit sphere' of radius = 1 with center (0, 0)
      List<Vector3> pts = []; Dictionary<Vector3, int> dict = [];
      var (V, T, citer) = PickSeedData (tolerance); V.ForEach (v => Add (v));
      List<int> tries = [.. T], buf = [];

      // Subdivide triangles without 'inflating' the nodes to preserve the `equilaterality`
      // of the sub-triangles. We will loft them after the recursive subdivision.
      for (int iter = 0; iter < citer; iter++) {
         buf.Clear ();
         for (int i = 0; i < tries.Count; i += 3) Subdivide (i);
         // Swap buffer with the main triangle list
         (buf, tries) = (tries, buf);
      }

      // Inflate the nodes to the 'unit sphere' surface
      var sphere = pts.Select (p => p.Normalized ());
      // Compose the Mesh.
      return new ([.. sphere.Select (Node)], [.. tries], []);

      // Create a mesh node from a position-vector on the unit sphere
      Mesh3.Node Node (Vector3 v) => new (center + v * radius, v);

      // Add a position-vector to the node list if not already present, and return its index.
      int Add (Vector3 pos) {
         if (dict.TryGetValue (pos, out int n)) return n;
         n = pts.Count; pts.Add (pos); dict[pos] = n;
         return n;
      }

      // Picks the optimal approximation beetween icosahedron and octahedron for the
      // given tolerance. It also estiamtes the number of required subdivisions.
      // It compares the number of generated triangles and picks the one which can match
      // the tolerance with fewer triangles. While making the mesh generation faster,
      // it also helps smoothen the subdivision-to-tolerance-range map.
      static (ImmutableArray<Vector3> V, ImmutableArray<int> T, int Subs) PickSeedData (double tol) {
         int idx = -1, subs = 0, cfaces = int.MaxValue;
         for (int i = 0; i < _SphereData.Length; i++) {
            var (V, T) = _SphereData[i];
            // Number of subdivisions to achieve the tolerance.
            int s = ComputeSubdivisions (V[T[0]], V[T[1]], tol);
            // Triangles count for 's'
            int c = (int)(T.Length / 3 * Math.Pow (4, s));
            if (c < cfaces) (idx, subs, cfaces) = (i, s, c);
         }
         return (_SphereData[idx].Vertices, _SphereData[idx].Faces, subs);

         // Given the tolerance value and a 'unit sphere' chord, compute the number of subdivision levels.
         static int ComputeSubdivisions (Vector3 a, Vector3 b, double tol) {
            if (tol < Lib.Epsilon) tol = Lib.Epsilon;
            // This is how close to the sphere radius we want to get.
            var minLen = 1 - tol;
            // Limit subdivision count 's' to 10. That is aleady too much with
            // N0 * power(4, '10') triangles for s = '10'. Where N0 is the
            // initial faces (8 for octahedron and 20 for icosahedron).
            for (int s = 0; s < 10; s++) {
               var mid = (a + b) * 0.5; var len = mid.Length;
               if (len >= minLen) return s;
               // Snap to sphere
               b = mid / len;
            }
            return 10;
         }
      }

      // Divide equilateral triangle 'ABC' into four smaller equilateral triangles.
      //        A
      //       / \
      //      /   \
      //    P/_____\R
      //    /\    / \
      //   /  \  /   \  
      //  /____\/_____\
      // B     Q       C
      void Subdivide (int i) {
         var (A, B, C) = (tries[i], tries[i + 1], tries[i + 2]);
         // Position vectors
         Vector3 a = pts[A], b = pts[B], c = pts[C];
         // Mid points on the triangle sides
         Vector3 ab = (a + b) * 0.5, ac = (a + c) * 0.5, bc = (b + c) * 0.5;
         // Register nodes and make new triangles.
         var (P, Q, R) = (Add (ab), Add (bc), Add (ac));
         buf.AddRange (A, P, R, P, Q, R, P, B, Q, Q, C, R);
      }
   }

   // Private data -------------------------------------------------------------
   // The icosahedron is constructed from three mutually perpendicular golden rectangles.
   // See https://en.wikipedia.org/wiki/Regular_icosahedron#Construction and
   // https://en.wikipedia.org/wiki/Golden_rectangle for more.
   // ________________________(a,b,0)
   // |          |           | One of the three golden rectangles.
   // |          b           | Corners of the rectangles are the 
   // |          |_____a_____| icosahedron vertices.
   // |        (0,0,0)       |
   // |                      |
   // |______________________|(a,-b,0)
   // For 'unit sphere':
   //  Sqr (a) + Sqr (b) = 1 
   //  a = b * (golden ratio)
   //  Golden ratio = (1 + Math.Sqrt (5)) / 2 
   readonly static double _GR = (1 + Math.Sqrt (5)) * 0.5;
   readonly static double _B = Math.Sqrt (1 / (1 + _GR * _GR));
   readonly static double _A = _B * _GR;
   // The 'known' sphere approximations with equilaterial triangles.
   readonly static (ImmutableArray<Vector3> Vertices, ImmutableArray<int> Faces)[] _SphereData = [
      // Octahedron vertices and triangles (6 and 8)
      ([Vector3.ZAxis, -Vector3.ZAxis, Vector3.XAxis, -Vector3.XAxis, Vector3.YAxis, -Vector3.YAxis],
      [0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,2,5, 1,5,3, 1,3,4]), 

      // Icosahedron vertices and triangles (12 and 20)
      ([new (-_B,0,_A), new (_B,0,_A),  new (-_B,0,-_A), new (_B,0,-_A),
        new (0,_A,_B),  new (0,_A,-_B), new (0,-_A,_B),  new (0,-_A,-_B),
        new (_A,_B,0),  new (-_A,_B,0), new (_A,-_B,0),  new (-_A,-_B, 0)],
      [0,4,1,  0,9,4,  9,5,4,  4,5,8,  4,8,1,
       8,10,1, 8,3,10, 5,3,8,  5,2,3,  2,7,3,
       7,10,3, 7,6,10, 7,11,6, 11,0,6, 0,1,6,
       6,1,10, 9,0,11, 9,11,2, 9,2,5,  7,2,11])
   ];
}
