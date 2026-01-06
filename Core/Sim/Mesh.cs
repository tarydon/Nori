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
   public Mesh3 (ImmutableArray<Node> vertex, ImmutableArray<int> tris, ImmutableArray<int> wire) {
      Vertex = vertex; Triangle = tris; Wire = wire;
   }

   public bool Opposing () {
      for (int i = 0; i < Triangle.Length; i += 3) {
         Node na = Vertex[Triangle[i]], nb = Vertex[Triangle[i + 1]], nc = Vertex[Triangle[i + 2]];
         Point3 pa = (Point3)na.Pos, pb = (Point3)nb.Pos, pc = (Point3)nc.Pos;
         Vector3 va = ((pb - pa) * (pc - pb)).Normalized ();
         Vector3 vb = ((Vector3)na.Vec + (Vector3)nb.Vec + (Vector3)nc.Vec).Normalized ();
         if (va.Opposing (vb)) return true;
      }
      return false;
   }

   public readonly ImmutableArray<Node> Vertex;
   public readonly ImmutableArray<int> Triangle;
   public readonly ImmutableArray<int> Wire;

   public Bound3 GetBound (Matrix3 xfm) => new (Vertex.Select (a => (Point3)a.Pos * xfm));

   public Bound3 Bound {
      get {
         if (_bound.IsEmpty) _bound = new Bound3 (Vertex.Select (a => a.Pos));
         return _bound;
      }
   }
   Bound3 _bound = new ();

   public static Mesh3 operator * (Mesh3 mesh, Matrix3 xfm) {
      if (xfm.IsIdentity) return mesh;
      ImmutableArray<Node> nodes = [.. mesh.Vertex.Select (a => a * xfm)];
      return new (nodes, mesh.Triangle, mesh.Wire);
   }

   [StructLayout (LayoutKind.Sequential, Pack = 2, Size = 20)]
   public readonly struct Node (Point3f pos, Vec3H vec) {
      public Node (Point3 pos, Vector3 vec) : this ((Point3f)pos, (Vec3H)vec) { }

      public Point3f Pos => pos;
      public Vec3H Vec => vec;

      public void Deconstruct (out Point3f p, out Vec3H v) => (p, v) = (Pos, Vec);
      public override string ToString () => $"{pos}, {vec}";
      public static Node operator * (Node node, Matrix3 xfm) {
         var pos = node.Pos * xfm;
         var vec = node.Vec;
         if (!xfm.IsTranslation) {
            Vector3 v = new ((double)vec.X, (double)vec.Y, (double)vec.Z);
            v *= xfm;
            vec = new ((Half)v.X, (Half)v.Y, (Half)v.Z);
         }
         return new (pos, vec);
      }
   }

   /// <summary>Returns a copy of the mesh shifted by the given vector</summary>
   public Mesh3 Translated (Vector3 vec) {
      var nodes = new Node[Vertex.Length];
      for (int i = 0; i < Vertex.Length; i++) {
         var node = Vertex[i];
         nodes[i] = new Node ((Point3f)((Point3)node.Pos + vec), node.Vec);
      }
      return new ([.. nodes], Triangle, Wire);
   }

   public static Mesh3 Load (string file) {
      using var stm = File.OpenRead (file);
      return Load (stm);
   }

   /// <summary>Loads a Mesh from a .mesh file</summary>
   public static unsafe Mesh3 Load (Stream stm) {
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

   public static Mesh3 LoadObj (IEnumerable<string> lines) {
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

   public static Mesh3 LoadObj (string filename)
      => LoadObj (File.ReadAllLines (filename));

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
         double[] a = Line ().Split (',', options).Select (double.Parse).ToArray ();
         nodes[i] = new Node (new (a[0], a[1], a[2]), new Vec3H ((Half)a[3], (Half)a[4], (Half)a[5]));
      }

      // Load the array of triangles
      int[] triangle = new int[3 * int.Parse (Line ())];
      for (int i = 0; i < triangle.Length; i += 3) {
         int[] a = Line ().Split (' ', options).Select (int.Parse).ToArray ();
         for (int j = 0; j < 3; j++) triangle[i + j] = a[j];
      }

      // Load the array of wires
      int[] wire = new int[2 * int.Parse (Line ())];
      for (int i = 0; i < wire.Length; i += 2) {
         int[] a = Line ().Split (' ', options).Select (int.Parse).ToArray ();
         for (int j = 0; j < 2; j++) wire[i + j] = a[j];
      }
      if (Line () != "EOF") Fatal ();
      return new (nodes.AsIArray (), triangle.AsIArray (), wire.AsIArray ());

      // Helpers ...........................................
      string Line () => lines[n++];
      static void Fatal () => throw new Exception ("Invalid TMESH file");
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

   /// <summary>Computes polygonal intersection loops between a mesh and a plane.</summary>
   public List<Polyline3> ComputePlaneIntersection (PlaneDef plane)
      => new PlaneMeshIntersector ([this]).Compute (plane);

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
}
#endregion Mesh3

#region class PlaneMeshIntersector -----------------------------------------------------------------
/// <summary>Provides plane/mesh intersection functionality for a specific mesh.</summary>
/// This class computes polygonal intersection loops between a mesh and a plane.
/// Create an instance with a mesh collection and call `Compute` repeatedly to intersect
/// multiple planes against the same mesh set.
public class PlaneMeshIntersector (IEnumerable<Mesh3> meshes) {
   /// <summary>Computes polygonal intersection loops between the stored meshes and a plane.</summary>
   /// - Quickly rejects meshes whose Bound lies entirely on one side of the plane.
   /// - For remaining meshes:
   ///   - Computes signed distances of all mesh vertices to the plane (used to detect edge crossings).
   ///   - Walks triangles; for each crossing edge, creates (or reuses) an interpolated intersection point.
   ///   - Links triangle intersection points into a small adjacency graph (each point has up to two neighbours).
   ///   - Builds an endpoint map to identify/open chains and reconnect matching endpoints when needed.
   ///   - Traverses the adjacency graph to extract chains/loops and converts them into returned Polyline3 objects.
   public List<Polyline3> Compute (PlaneDef plane) {
      Vector3 n = plane.Normal; double d = plane.D;
      mOutChains.Clear ();

      foreach (var mesh in meshes) {
         if (!Intersects (mesh.Bound, n, d)) continue;

         PrepareMesh (mesh);
         ComputeDistances (n, d);

         ResetWorkBuffers ();
         BuildAdjacency ();
         BuildEndMap ();
         PrepareVisited (mRaw.Count);
         WalkAllChains ();
      }

      return MergePolylines ();
   }

   List<Polyline3> MergePolylines () {
      const float tol = 1e-3f;
      List<Polyline3> polylines = [];

      // Remove all closed loops in outChains first
      for (int i = mOutChains.Count - 1; i >= 0; i--) {
         var chain = mOutChains[i];
         if (chain[0].EQ (chain[^1], tol)) {
            BuildPolyline (chain, polylines);
            mOutChains.RemoveAt (i);
         }
      }

      PrepareVisited (mOutChains.Count);

      void AddToPtMap (Point3f pt, (int idx, bool isEnd) val) {
         if (!ptMap.TryGetValue (pt, out List<(int idx, bool isEnd)>? lst))
            ptMap[pt] = [val];
         else lst.Add (val);
      }

      for (int i = 0; i < mOutChains.Count; i++) {
         var chain = mOutChains[i];
         AddToPtMap (chain[0], (i, false));
         AddToPtMap (chain[^1], (i, true));
      }

      for (int i = 0; i < mOutChains.Count; i++) {
         if (mVisited[i]) continue;

         mVisited[i] = true; var chain = mOutChains[i];

         TraverseAndMerge (chain, false);
         TraverseAndMerge (chain, true);

         BuildPolyline (chain, polylines);
      }

      return polylines;
   }

   void TraverseAndMerge (List<Point3f> chain, bool fromHead) {
      Point3f pt = fromHead ? chain[0] : chain[^1];

      while (ptMap.TryGetValue (pt, out var lst)) {
         if (lst.Count > 2) break;

         (int nIdx, bool isEnd) = lst.Find (a => !mVisited[a.idx]);
         if (nIdx == 0) break;

         var nChain = mOutChains[nIdx]; int nCount = nChain.Count; mVisited[nIdx] = true;

         if (fromHead) {
            // Backward direction: attach at chain start
            if (isEnd) for (int j = nCount - 2; j >= 0; j--) chain.Insert (0, nChain[j]);
            else for (int j = 1; j < nCount; j++) chain.Insert (0, nChain[j]);
            pt = chain[0];
         } else {
            // Forward direction: attach at chain end
            if (isEnd) for (int j = nCount - 2; j >= 0; j--) chain.Add (nChain[j]);
            else for (int j = 1; j < nCount; j++) chain.Add (nChain[j]);
            pt = chain[^1];
         }
      }
   }

   // Quick plane-vs-mesh test; returns false if the bound lies strictly on one side.
   bool Intersects (Bound3 bound, Vector3 n, double d) {
      if (bound.IsEmpty) return false;

      // Center (Mid) and half extents (Length/2) per axis.
      double cx = bound.X.Mid, cy = bound.Y.Mid, cz = bound.Z.Mid;
      double ex = bound.X.Length * 0.5, ey = bound.Y.Length * 0.5, ez = bound.Z.Length * 0.5;

      // Signed distance from bound center to plane: dot(n, c) + d.
      double dist = n.X * cx + n.Y * cy + n.Z * cz + d;

      // Project the bound extents onto the plane normal to get the max reach.
      double r = Math.Abs (n.X) * ex + Math.Abs (n.Y) * ey + Math.Abs (n.Z) * ez;

      // If center distance exceeds projected radius, plane cannot hit the mesh.
      return Math.Abs (dist) <= r;
   }

   // Loads/caches the current mesh's positions and triangles into reusable buffers.
   void PrepareMesh (Mesh3 mesh) {
      if (mPts.Length != mesh.Vertex.Length)
         mPts = new Point3f[mesh.Vertex.Length];

      for (int i = 0; i < mPts.Length; i++)
         mPts[i] = mesh.Vertex[i].Pos;

      mTri = mesh.Triangle;
      if (mDist.Length != mPts.Length)
         mDist = new double[mPts.Length];
   }

   // Computes signed distances of all mesh vertices to the plane.
   void ComputeDistances (Vector3 n, double dBias) {
      for (int i = 0; i < mPts.Length; i++) {
         // Signed distance to plane: dot(n, p) + d.
         var p = mPts[i];
         double v = n.X * p.X + n.Y * p.Y + n.Z * p.Z + dBias;
         // A tiny bias to avoid ambiguous "on plane" classification
         if (Math.Abs (v) < 1e-10) v += 1e-8; mDist[i] = v;
      }
   }

   // Clears all per-call working collections.
   void ResetWorkBuffers () {
      mRaw.Clear (); mNbr1.Clear (); mNbr2.Clear ();
      mEdgeMap.Clear (); mEnds.Clear ();
   }

   // Builds the adjacency lists between plane-edge intersection points produced from triangles.
   void BuildAdjacency () {
      for (int t = 0; t < mTri.Length; t += 3) {
         int a = mTri[t], b = mTri[t + 1], c = mTri[t + 2];
         double da = mDist[a], db = mDist[b], dc = mDist[c];

         int p1 = -1, p2 = -1;

         // For each triangle edge, a sign change means the edge crosses the plane and yields one
         // intersection point (created once per mesh edge via mEdgeMap).
         if (da * db < 0) p1 = GetOrAddEdgePoint (a, b, da, db);
         if (db * dc < 0) { int p = GetOrAddEdgePoint (b, c, db, dc); if (p1 == -1) p1 = p; else p2 = p; }
         if (dc * da < 0) { int p = GetOrAddEdgePoint (c, a, dc, da); if (p1 == -1) p1 = p; else p2 = p; }

         // A plane cutting a triangle produces exactly 2 points.
         // When we have 2, add those as neighbours to each other.
         if (p1 != -1 && p2 != -1) Link (p1, p2);
      }
   }

   // Adds a bidirectional link between two intersection points in the per-point adjacency lists.
   void Link (int a, int b) {
      if (mNbr1[a] == -1) mNbr1[a] = b; else mNbr2[a] = b;
      if (mNbr1[b] == -1) mNbr1[b] = a; else mNbr2[b] = a;
   }

   // Builds a dictionary mapping open-chain endpoints to their associated intersection point indices.
   void BuildEndMap () {
      for (int i = 0; i < mRaw.Count; i++) {
         // EndPoints of a chain have only one neighbour
         if (mNbr2[i] != -1) continue; // Skip non-endpoints

         // This allows reconnecting open ends that are geometrically equal 
         // (within the Point3fComparer tolerance) but produced via different tris.
         var key = mRaw[i];
         if (mEnds.TryGetValue (key, out var v)) mEnds[key] = (v.A, i);
         else mEnds.Add (key, (i, -1));
      }
   }

   // Ensures the visited array is large enough and clears it for the current intersection run.
   void PrepareVisited (int count) {
      if (mVisited.Length < count) mVisited = new bool[count];
      else Array.Clear (mVisited, 0, count);
   }

   // Walks all unvisited intersection points to extract all polylines/chains for this plane cut.
   void WalkAllChains () {
      for (int i = 0; i < mRaw.Count; i++) {
         if (mVisited[i]) continue;
         mVisited[i] = true;

         // Pooled list reduces per-chain allocations when computing many planes.
         List<Point3f> pts = [mRaw[i]];

         // Walk forward and backward to collect all points in the chain.
         Traverse (i, mNbr1[i], false, pts);
         Traverse (i, mNbr2[i], true, pts);

         mOutChains.Add (pts);
      }
   }

   // Converts extracted chains into Polyline3 and returns pooled lists back to the pool.
   void BuildPolyline (List<Point3f> pts, List<Polyline3> polylines) {
      int nPts = pts.Count;

      // return empty polyline if no points
      if (nPts < 2) return;

      // check if zero length polyline (all points equal)
      var prev = pts[0]; bool hasLen = false;
      for (int j = 1; j < nPts; j++) {
         var p = pts[j];
         if (!p.EQ (prev, 1e-3f)) { hasLen = true; break; }
         prev = p;
      }

      if (!hasLen) return;

      var dst = new Point3[nPts];
      for (int j = 0; j < nPts; j++) dst[j] = (Point3)pts[j];

      // Return list to pool for reuse in subsequent Compute() calls
      pts.Clear (); mTmpPool.Push (pts);
      polylines.Add (new Polyline3 (0, ImmutableArray.Create (dst)));
   }

   // Traverses a polyline in one direction from a start point, optionally prepending points to preserve order.
   void Traverse (int from, int nextIdx, bool prepend, List<Point3f> pts) {
      int prev = from, curr = nextIdx;

      while (curr != -1 && !mVisited[curr]) {
         // Follow links until the chain ends (-1) or point visited.
         while (curr != -1 && !mVisited[curr]) {
            if (prepend) pts.Insert (0, mRaw[curr]);
            else pts.Add (mRaw[curr]);
            mVisited[curr] = true;

            // Pick the neighbour that is not the node we came from.
            int n1 = mNbr1[curr], n2 = mNbr2[curr];
            int next = n1 != prev ? n1 : n2;
            prev = curr; curr = next;
         }

         // If curr != -1 we hit a visited node,
         // meaning this direction is complete (loop or overlap).
         // Close the loop by adding the visited point to pts.
         if (curr != -1) { pts.Add (mRaw[curr]); return; }

         // We hit an open end (curr == -1). Try to continue
         // by jumping to the matching endpoint (same geometric position)
         // if the intersection produced discontinuous chains due to tolerance.
         if (!mEnds.TryGetValue (mRaw[prev], out var val)) return;
         mEnds.Remove (mRaw[prev]);

         // If we have only one side recorded, there is nothing to jump to.
         if (val.A == -1 || val.B == -1) return;

         // Jump to the other stored index at the same end point.
         int idx = (val.A == prev) ? val.B : val.A;
         if (idx == -1 || mVisited[idx]) return;

         // Resume traversal from the jumped point.
         // We mark it visited immediately to prevent cycles.
         prev = idx; curr = mNbr1[idx];
         mVisited[idx] = true;
      }
   }

   // Gets an existing intersection point or creates it.
   int GetOrAddEdgePoint (int a, int b, double da, double db) {
      // Normalize edge key so (a,b) and (b,a) map to same point.
      var key = a < b ? (a, b) : (b, a);
      if (mEdgeMap.TryGetValue (key, out int idx)) return idx;

      // Interpolate along the edge using signed distances: t = da / (da - db).
      idx = mRaw.Count;
      mRaw.Add ((da / (da - db)).Along (mPts[a], mPts[b]));

      // Start with no neighbours; BuildAdjacency/Link will fill these.
      mNbr1.Add (-1); mNbr2.Add (-1); mEdgeMap[key] = idx;
      return idx;
   }

   // Input mesh positions and triangles (set per mesh during Compute)
   Point3f[] mPts = [];
   ImmutableArray<int> mTri = [];

   // Per-instance working storage reused between Compute calls
   double[] mDist = [];
   readonly Dictionary<(int, int), int> mEdgeMap = [];
   readonly Dictionary<Point3f, (int A, int B)> mEnds = new (Point3fComparer.Delta);
   readonly Dictionary<Point3f, List<(int idx, bool isEnd)>> ptMap = new (Point3fComparer.Delta);
   readonly List<int> mNbr1 = [], mNbr2 = [];   // Neighbours of intersection points at each index
   readonly List<Point3f> mRaw = [];            // interpolated intersection points (Point3f)
   bool[] mVisited = [];

   // Reuse temporary lists to reduce GC pressure
   readonly List<List<Point3f>> mOutChains = [];
   readonly Stack<List<Point3f>> mTmpPool = new ();
}
#endregion PlaneMeshIntersector

#region class Mesh3Builder -------------------------------------------------------------------------
/// <summary>The Mesh3Builder class builds meshes with auto-smoothing and marking of sharp creases</summary>
/// To construct a mesh, all this needs is a triangle mesh and with consistent winding.
/// This finds all the shared edges between faces and if the edge angle is
/// more than a given threshold, it marks the edge as sharp. The SmoothMeshBuilder works with a given mesh
/// as the target and adds triangles into that mesh. Note that you never need to supply normals to this.
/// It computes normals based on which parts should be 'smooth' and which ones should be 'sharp'.
public class Mesh3Builder {
   /// <summary>Initialize a Mesh3Builder with a set of points </summary>
   /// These points, taken 3 at a time, define a set of triangles.
   /// <param name="pts">Triangle points.</param>
   public Mesh3Builder (ReadOnlySpan<Point3> pts) {
      Dictionary<Point3, int> verts = [];
      foreach (var pt in pts) {
         if (!verts.TryGetValue (pt, out int id)) {
            id = verts.Count;
            verts.Add (pt, id);
            Add (ref mVertex, ref mcVertex, new (pt));
         }
         mIdx.Add (id);
      }
   }

   /// <summary>Constructs a Mesh3 object from the given set of 'smoothed' triangles.</summary>
   public Mesh3 Build () {
      for (int i = 0; i < mIdx.Count; i += 3) {
         int A = mIdx[i], B = mIdx[i + 1], C = mIdx[i + 2];
         Point3 a = mVertex[A].Pos, b = mVertex[B].Pos, c = mVertex[C].Pos;
         var norm = (b - a) * (c - a);
         Face f = new (A, B, C, norm / norm.Length, norm.Length);
         AssignFace (f.A, mcFace); AssignFace (f.B, mcFace); AssignFace (f.C, mcFace);
         Add (ref mFace, ref mcFace, f);
      }

      Mesh3.Node[] nodes = new Mesh3.Node[mcVertex]; int cNodes = 0;
      List<int> wires = [], tries = [];
      HashSet<(Point3, Point3)> lines = [];

      // First go through the faces touching each corner and gather them into smoothing-groups
      for (int i = 0; i < mcVertex; i++) GroupFaces (ref mVertex[i], ref nodes, ref cNodes);

      // Now we have enough information to try and create the faces
      for (int i = 0; i < mcFace; i++) AddTriangle (i);

      return new Mesh3 ([.. nodes.AsSpan (0, cNodes)], [.. tries], [.. wires]);

      // Assigns a reference to the given face to a given vertex
      // This tells the vertex that the face nFace references this vertex
      void AssignFace (int nVert, int nFace) =>
         mChains.Add (ref mVertex[nVert].FaceChain, new FaceData (nFace));

      // Records a mesh triangle and the stencil line.
      void AddTriangle (int nFace) {
         Face f = mFace[nFace];
         Vertex v1 = mVertex[f.A], v2 = mVertex[f.B], v3 = mVertex[f.C];
         int n1 = GetVID (v1, nFace), n2 = GetVID (v2, nFace), n3 = GetVID (v3, nFace);

         tries.Add (n1); tries.Add (n2); tries.Add (n3);
         if (IsStencil (v1, f.B)) AddLine (n1, n2, v1, v2);
         if (IsStencil (v2, f.C)) AddLine (n2, n3, v2, v3);
         if (IsStencil (v3, f.A)) AddLine (n3, n1, v3, v1);
      }

      // Adds an entry to the stencil array
      void AddLine (int n1, int n2, in Vertex v1, in Vertex v2) {
         // Skip duplicate lines.
         if (lines.Contains ((v1.Pos, v2.Pos)) || lines.Contains ((v2.Pos, v1.Pos))) return;
         lines.Add ((v1.Pos, v2.Pos));
         wires.Add (n1); wires.Add (n2);
      }

      // Returns the vertex ID of a corner, as it appears in face nFace
      int GetVID (Vertex v, int nFace) {
         foreach (var fd in mChains.Enum (v.FaceChain))
            if (fd.NFace == nFace) return fd.NGroup;
         throw new NotImplementedException ();
      }
   }

   /// <summary>Given a Vertex c, this assigns group codes to each face referencing this vertex</summary>
   /// Note that these group codes are local to this vertex - they are not a property of the face.
   /// That is, AT this vertex, we set up group codes for all the faces touching it. For example,
   /// it's possible that 6 faces might touch a vertex. Looking top down on the vertex, let's say
   /// they form a hexagonal web at the vertex, and the faces are A, B, C, D, E, F (in that order).
   /// Suppose also that A,B,C have a group code 0 (at this vertex) and D,E,F have a group code 1.
   /// This means that the normals of A,B,C will be averaged to provide the normal on 'that side'
   /// of the sharp edge, while the normals of D,E,F will be averaged to provide the other normal.
   /// The sharp edges here are the edges between C-D and the edge between F-A.
   ///
   /// The GroupFaces method builds all the necessary data required to provide this averaged normal
   /// and the 'sharp-edge' flag at the next stage.
   void GroupFaces (ref Vertex c, ref Mesh3.Node[] nodes, ref int cNodes) {
      int max = 0;
      // First, assign group codes to each face - any face that forms a small enough angle to any
      // previous face uses that face's group code. Otherwise, it begins a new group.
      mChains.GatherRawIndices (c.FaceChain, mVF);
      for (int i = 0; i < mVF.Count; i++) {
         ref FaceData fd1 = ref mChains.Data[mVF[i]];
         Vector3 vec = mFace[fd1.NFace].Vec;
         for (int j = 0; j < i; j++) {
            ref FaceData fd2 = ref mChains.Data[mVF[j]];
            if (mFace[fd2.NFace].Vec.CosineToAlreadyNormalized (vec) > mCos) fd1.NGroup = fd2.NGroup;
         }
         if (fd1.NGroup == -1) fd1.NGroup = max++;
      }

      // At this Point, max is the number of groups at this vertex (this is usually a small number,
      // less than 6 to 10). We have to now compute the normal vectors for each of the groups, by
      // averaging the normals of all faces with that group code. We weight this average by the face
      // area.
      while (mAvgs.Count < max) { mAvgs.Add (Vector3.Zero); mVIDs.Add (0); }
      for (int i = 0; i < max; i++) mAvgs[i] = Vector3.Zero;

      foreach (int n in mVF) {
         FaceData fd = mChains.Data[n];
         Face f = mFace[fd.NFace];
         mAvgs[fd.NGroup] += f.Vec * f.Area;
      }

      // We now have the weighted average of the normal from each face-group for this corner. Add
      // multiple vertices into the mesh. If there are 3 groups, then we can now add 3 'mesh-vertex'
      // objects (each of which consists of a position+normal). The position is same for all of these,
      // but the normals are based on the group-averaged normals. As we do this, we get the vertex-ids
      // of these from the mesh and add them into the mVIDs temporary array.
      for (int i = 0; i < max; i++) {
         var norm = mAvgs[i].Normalized ();
         int vid = cNodes;
         Mesh3.Node node = new ((Point3f)c.Pos, new ((Half)norm.X, (Half)norm.Y, (Half)norm.Z));
         Add (ref nodes, ref cNodes, node);
         mVIDs[i] = vid;
      }
      // Now update the facedata with the VIDs. After this, the NGroup values in the FaceData contain
      // actually vertex IDs within the mesh. This is critical: up to this point, the NGroup values contained
      // the group codes for the faces (small numbers like 0,1,2). At this point, we replace those with
      // vertex-IDs within the mesh (so that these numbers can directly be used later to compose triangles).
      foreach (int n in mVF)
         mChains.Data[n].NGroup = mVIDs[mChains.Data[n].NGroup];
   }

   /// <summary>This tells us if the edge from this vertex (c) to the next vertex (next) is 'sharp'</summary>
   bool IsStencil (Vertex c, int next) {
      if (Wireframe) return true;
      // We have to find two faces that have 'next' in their corner list. Then, if they
      // both have the same group code, there is no stencil. If we find only one face, or if their
      // group codes are different, there is a stencil.
      int cFound = 0, nGroup = 0;
      foreach (var fd in mChains.Enum (c.FaceChain)) {
         if (!mFace[fd.NFace].Contains (next)) continue;
         if (++cFound == 1) nGroup = fd.NGroup;    // Found 1 face
         else return nGroup != fd.NGroup;          // Found 2nd face, we can now figure out if this is a sharp edge
      }
      // Note that we will not find 3 faces in any well-formed mesh.
      return true;
   }

   public bool Wireframe = false;

   // This is the list of faces at this vertex (a temporary used only by GroupFaces)
   readonly List<int> mVF = [];
   // The list of group-wise averages (a temporary used only by GroupFaces)
   readonly List<Vector3> mAvgs = [];
   // The list of vertex-IDs for these groups (a temporary used only by GroupFaces)
   readonly List<int> mVIDs = [];

   // If two faces have a cosine less than this between them, it's a sharp edge
   const double mCos = 0.51;

   // This is the list of vertices
   readonly Vertex[] mVertex = [];
   // How many of these vertices are used?
   readonly int mcVertex;

   // This is the list of all the faces (each has 3 vertices, area and normal)
   Face[] mFace = [];
   // How many of the elements from this array are used?
   int mcFace;

   // These contain the chains of face-data stored with each vertex
   readonly Chains<FaceData> mChains = new ();
   // This is the temporary array into which indices are gathered (they are used only when Build() is called)
   readonly List<int> mIdx = [];

   // Add an element into an array, growing the array as needed
   static void Add<T> ([NotNull] ref T[]? data, ref int cUsed, T value) {
      int n = data?.Length ?? 0;
      if (cUsed >= n || data == null) { n = Math.Max (8, n * 2); Array.Resize (ref data, n); }
      data[cUsed++] = value;
   }

   /// <summary>This holds the data about a Face (the 3 vertices it is made of, the normal, the area)</summary>
   /// <param name="A">The first Face corner</param>
   /// <param name="B">The second Face corner</param>
   /// <param name="C">The third Face corner</param>
   /// <param name="Vec">The face normal</param>
   /// <param name="Area">Area of 'this' face (used when computing an average normal at a corner)</param>
   readonly record struct Face (int A, int B, int C, Vector3 Vec, double Area) {
      /// <summary>Returns true if the given vertex belongs to this face</summary>
      public bool Contains (int n) => A == n || B == n || C == n;
   }

   /// <summary>This structure holds the reference of a face within a vertex</summary>
   /// It is primarily used to assign group numbers to these faces such that all faces
   /// sharing a group number are part of the same 'smoothing group'.
   struct FaceData (int nFace) {
      /// <summary>The face reference.</summary>
      public readonly int NFace = nFace;
      /// <summary>Faces with the same group code are in the same smoothing-group</summary>
      public int NGroup = -1;
   }

   /// <summary>This maintains the data for a given vertex</summary>
   /// This holds the list of faces this vertex is referenced by, and the actual
   /// geometric position of the vertex
   struct Vertex (in Point3 pos) {
      /// <summary>Position of 'this' vertex.</summary>
      public readonly Point3 Pos = pos;
      /// <summary>This is the chain of faces connected to this corner</summary>
      public int FaceChain;
   }
}
#endregion

class Point3fComparer (float threshold) : IEqualityComparer<Point3f> {
   public bool Equals (Point3f a, Point3f b) => a.EQ (b, threshold);

   public int GetHashCode (Point3f a)
      => HashCode.Combine (a.X.Round (threshold), a.Y.Round (threshold), a.Z.Round (threshold));

   /// <summary>A Point3f comparer that compares points with a threshold of 1e-3</summary>
   public static readonly Point3fComparer Delta = new (1e-3f);
   /// <summary>A Point3f comparer that compares points with a threshold of 1e-6</summary>
   public static readonly Point3fComparer Epsilon = new (1e-6f);
}