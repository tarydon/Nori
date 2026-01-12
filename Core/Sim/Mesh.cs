// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Mesh.cs
// ║║║║╬║╔╣║ Implements the Mesh3 class, a simple mesh format for rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO.Compression;
namespace Nori;

#region class Mesh3 --------------------------------------------------------------------------------
/// <summary>This implements a super-simple mesh format for rendering</summary>
/// This format is very close to the way the data has to be presented for rendering,
/// and is designed to be very simple to read / understand.
/// - The Vertex array contains a set of nodes, each defined with a position
///   (Point3f) and normal (Vec3H)
/// - The Triangle array (taken 3 at a time) defines the triangles as indices into
///   the Vertex array
/// - The Wires array (taken 2 at a time) defines the stencil lines to be drawn.
///   Each endpoint of the wire is again an index into the Vertex array (and to draw the
///   wires, we use only the position, not the normal)
public class Mesh3 {
   // Constructor --------------------------------------------------------------
   /// <summary>Core constructor for a Mesh3</summary>
   public Mesh3 (ImmutableArray<Node> vertex, ImmutableArray<int> tris, ImmutableArray<int> wire) {
      Vertex = vertex; Triangle = tris; Wire = wire;
   }

   /// <summary>Loads a Mesh from a Flux .mesh file</summary>
   public static unsafe Mesh3 LoadFluxMesh (string file) {
      using var stm = File.OpenRead (file);
      using DeflateStream dfs = new (stm, CompressionMode.Decompress, false);
      ByteStm bs = new (dfs.ReadBytes (dfs.ReadInt32 ()));
      int sign = bs.ReadInt32 (), version = bs.ReadByte ();
      if (sign != 0x1A48534D || version is < 1 or > 2)
         throw new IOException ("Mesh file is damaged");
      if (version >= 2) bs.ReadString ();    // Read and discard the name

      Mesh3[] meshes = new Mesh3[bs.ReadInt32 ()];
      for (int i = 0; i < meshes.Length; i++) {
         // Read the marker to indicate if this is a 'small' mesh, and skip past the
         // 'mesh format'
         bool small = bs.ReadByte () == 2; bs.ReadByte ();
         var nodes = new Node[bs.ReadInt32 ()];
         int tris = bs.ReadInt32 (), all = bs.ReadInt32 (), wires = all - tris;
         if (nodes.Length > 0)
            fixed (Node* pnode = &nodes[0])
               for (int j = 0; j < nodes.Length; j++) bs.Read (pnode + j, 18);

         int[] tIdx; List<int> wIdx = [];
         if (small) {
            ushort[] stIdx = new ushort[tris], swIdx = new ushort[all - tris];
            fixed (void* p = stIdx) bs.Read (p, tris * 2);
            fixed (void* p = swIdx) bs.Read (p, wires * 2);
            tIdx = [.. stIdx.Select (a => (int)a)];
            ushort prev = 0xffff;
            foreach (var n in swIdx) {
               if (prev != 0xffff && n != 0xffff) { wIdx.Add (prev); wIdx.Add (n); }
               prev = n;
            }
         } else {
            tIdx = new int[tris]; uint[] wIdx0 = new uint[wires];
            fixed (void* p = tIdx) bs.Read (p, tris * 4);
            fixed (void* p = wIdx0) bs.Read (p, wires * 4);
            uint prev = 0xffffffff;
            for (int j = 0; j < wIdx0.Length; i++) {
               uint n = wIdx0[j];
               if (prev != 0xffffffff && n != 0xffffffff) { wIdx.Add ((int)prev); wIdx.Add ((int)n); }
               prev = n;
            }
         }
         meshes[i] = new Mesh3 ([.. nodes], [.. tIdx], [.. wIdx]);
      }
      List<Node> vertex = [];
      List<int> ftris = [], fwires = [];
      foreach (var mesh in meshes) {
         int nBase = vertex.Count;
         vertex.AddRange (mesh.Vertex);
         ftris.AddRange (mesh.Triangle.Select (a => a + nBase));
         fwires.AddRange (mesh.Wire.Select (a => a + nBase));
      }
      return new Mesh3 ([.. vertex], [.. ftris], [.. fwires]);
   }

   /// <summary>Loads a Mesh from an OBJ file</summary>
   public static Mesh3 LoadObj (string file) => LoadObj (File.ReadAllLines (file));

   /// <summary>Loads a Mesh from OBJ file data (presented as strings)</summary>
   public static Mesh3 LoadObj (IList<string> lines) {
      List<Point3> raw = [], pts = [];
      foreach (var line in lines) {
         if (line.StartsWith ('v')) {
            var v = line[2..].Split (' ').Select (double.Parse).ToList ();
            if (v.Count >= 3) raw.Add (new (v[0], v[1], v[2]));
         } else if (line.StartsWith ('f')) {
            var v = line[2..].Split (' ').Select (int.Parse).ToList ();
            if (v.Count >= 3)
               for (int i = 0; i < 3; i++) pts.Add (raw[v[i] - 1]);
         }
      }
      return new Mesh3Builder (pts.AsSpan ()).Build ();
   }

   /// <summary>Loads data from a TMesh file</summary>
   public static Mesh3 LoadTMesh (string filename) {
      int n = 0;
      var lines = File.ReadAllLines (filename);
      // Parse the file header
      if (Line () != "TMESH" || Line () != "1") Fatal ();

      // Load the array of PointVec
      var nodes = new Node[int.Parse (Line ())];
      const StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
      for (int i = 0; i < nodes.Length; i++) {
         double[] a = [.. Line ().Split (',', options).Select (double.Parse)];
         nodes[i] = new Node (new (a[0], a[1], a[2]), new Vec3H ((Half)a[3], (Half)a[4], (Half)a[5]));
      }

      // Load the array of triangles
      int[] triangle = new int[3 * int.Parse (Line ())];
      for (int i = 0; i < triangle.Length; i += 3) {
         int[] a = [.. Line ().Split (' ', options).Select (int.Parse)];
         for (int j = 0; j < 3; j++) triangle[i + j] = a[j];
      }

      // Load the array of wires
      int[] wire = new int[2 * int.Parse (Line ())];
      for (int i = 0; i < wire.Length; i += 2) {
         int[] a = [.. Line ().Split (' ', options).Select (int.Parse)];
         for (int j = 0; j < 2; j++) wire[i + j] = a[j];
      }
      if (Line () != "EOF") Fatal ();
      return new (nodes.AsIArray (), triangle.AsIArray (), wire.AsIArray ());

      // Helpers ...........................................
      string Line () => lines[n++];
      static void Fatal () => throw new Exception ("Invalid TMESH file");
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

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the bounding cuboid of this Mesh3</summary>
   public Bound3 Bound => Bound3.Cached (ref _bound, () => new (Vertex.Select (a => a.Pos)));
   Bound3 _bound = new ();

   /// <summary>Is this an empty mesh3 (no triangles)</summary>
   public bool IsEmpty => Triangle.Length == 0;
   /// <summary>These integers, taken 3 at a time, are indices into Vertex[], making up the triangles</summary>
   public readonly ImmutableArray<int> Triangle;
   /// <summary>Set of Vertices of this Mesh3</summary>
   /// Each vertex contains a Point3f (for position) and a Vec3H (for normal vector)
   public readonly ImmutableArray<Node> Vertex;
   /// <summary>These integers, taken 2 at a time, are the stencil lines to be drawn in the mesh</summary>
   public readonly ImmutableArray<int> Wire;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the surface area of this mesh (sum of areas of all triangles)</summary>
   public double GetArea () {
      double total = 0;
      for (int i = 0; i < Triangle.Length; i += 3) {
         Point3 pa = (Point3)Vertex[Triangle[i]].Pos,
                pb = (Point3)Vertex[Triangle[i + 1]].Pos,
                pc = (Point3)Vertex[Triangle[i + 2]].Pos;
         total += ((pb - pa) * (pc - pb)).Length;
      }
      return total / 2;
   }

   /// <summary>Returns the Bound of this Mesh3, transformed by the given matrix</summary>
   public Bound3 GetBound (Matrix3 xfm) => new (Vertex.Select (a => (Point3)a.Pos * xfm));

   /// <summary>Returns a copy of this mesh with full stencil lines</summary>
   public Mesh3 Wireframed () {
      List<int> wires = [];
      HashSet<(int A, int B)> done = [];
      for (int i = 0; i < Triangle.Length; i += 3) {
         int a = Triangle[i], b = Triangle[i + 1], c = Triangle[i + 2];
         Add (a, b); Add (b, c); Add (c, a);

         void Add (int t1, int t2) {
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (done.Add ((t1, t2))) { wires.Add (t1); wires.Add (t2); }
         }
      }
      return new (Vertex, Triangle, [.. wires]);
   }

   /// <summary>Saves the mesh to a TMesh file.</summary>
   /// The TMESH file has following sections:
   /// 1. HEADER: The header section contains file format signature and file version.
   /// 2. VERTICES: This section contains the vertex table where a vertex node is defined
   ///    by a vertex position and the normal.
   /// 3. TRIANGLES: These are the triangles table where a row contains three vertex indices
   ///    for a triangle corner each.
   /// 4. STENCILS: This section contains the stencil lines defined with a pair of vertex indeces.
   /// 5. The file is terminated with an EOF (end-of-file) marker.
   ///
   /// The data sections (2, 3 and 4) start with the count followed by the one-per-line entries.
   /// A sample mesh file with two triangles forming a rectangular plane and a border:
   /// <code>
   /// <![CDATA[
   /// TMESH
   /// 1
   /// 4
   /// 5,-50,-30,  1,0,0
   /// 5,50,-30,  1,0,0
   /// 5,50,30,  1,0,0
   /// 5,-50,30,  1,0,0
   /// 2
   /// 0 1 2
   /// 2 3 0
   /// 4
   /// 0 1
   /// 1 2
   /// 2 3
   /// 3 0
   /// EOF
   /// ]]>
   /// </code>
   public string ToTMesh () {
      // Version, Vertex count, vertices
      StringBuilder sb = new ($"TMESH\n1\n{Vertex.Length}\n");
      foreach (var (pos, vec) in Vertex)
         sb.Append ($"{pos.X},{pos.Y},{pos.Z},  {vec.X},{vec.Y},{vec.Z}\n");

      // Triangle count, triangle indices
      sb.Append ($"{Triangle.Length / 3}\n");
      for (int i = 0; i < Triangle.Length; i += 3)
         sb.Append ($"{Triangle[i]} {Triangle[i + 1]} {Triangle[i + 2]}\n");

      // Stencil count, stencil indices
      sb.Append ($"{Wire.Length / 2}\n");
      for (int i = 0; i < Wire.Length; i += 2)
         sb.Append ($"{Wire[i]} {Wire[i + 1]}\n");
      sb.Append ("EOF\n");
      return sb.ToString ();
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Multiply a Mesh3 by a given transform</summary>
   public static Mesh3 operator * (Mesh3 mesh, Matrix3 xfm) {
      if (xfm.IsIdentity) return mesh;
      ImmutableArray<Node> nodes = [.. mesh.Vertex.Select (a => a * xfm)];
      return new (nodes, mesh.Triangle, mesh.Wire);
   }

   // Nested types -------------------------------------------------------------
   /// <summary>Represents a vertex in a Mesh3 (with a position, and a normal)</summary>
   [StructLayout (LayoutKind.Sequential, Pack = 2, Size = 20)]
   public readonly struct Node {
      // Constructors ----------------------------------------------------------
      /// <summary>Basic constructor for Node</summary>
      public Node (Point3f pos, Vec3H vec) { Pos = pos; Vec = vec; }
      /// <summary>Construct a Node given a Point3 and a Vector3</summary>
      public Node (Point3 pos, Vector3 vec) { Pos = (Point3f)pos; Vec = (Vec3H)vec; }
      /// <summary>Construct a Node3 given 3 components for position and 3 components for normal</summary>
      public Node (double x, double y, double z, double dx, double dy, double dz) {
         Pos = new Point3f (x, y, z); Vec = new Vec3H ((Half)dx, (Half)dy, (Half)dz);
      }

      // Properties ------------------------------------------------------------
      /// <summary>The Position of the Node</summary>
      public readonly Point3f Pos;
      /// <summary>The Normal Vector of the node</summary>
      public readonly Vec3H Vec;

      // Implementation --------------------------------------------------------
      public void Deconstruct (out Point3f p, out Vec3H v) => (p, v) = (Pos, Vec);
      public override string ToString () => $"{Pos} {Vec}";

      // Operators -------------------------------------------------------------
      /// <summary>Multiply a node by a given translation matrix</summary>
      /// Optimizes some special cases:
      /// - If this is a pure translation matrix, the Vector does not change
      /// - If there is no scaling, the new Vector does not have be normalized
      public static Node operator * (Node node, Matrix3 xfm) {
         var pos = node.Pos * xfm;
         var vec = node.Vec;
         if (!xfm.IsTranslation) {
            Vector3 v = new ((double)vec.X, (double)vec.Y, (double)vec.Z);
            v *= xfm;
            if (xfm.HasScaling) v = v.Normalized ();
            vec = new ((Half)v.X, (Half)v.Y, (Half)v.Z);
         }
         return new (pos, vec);
      }
   }
}
#endregion Mesh3
