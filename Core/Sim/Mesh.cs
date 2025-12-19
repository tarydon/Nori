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
      return new ([..nodes], Triangle, Wire);
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

         int[] tIdx;  List<int> wIdx = [];
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
      return new Mesh3 ([..vertex], [..ftris], [..fwires]);
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

   /// <summary>Builds a sphere mesh centered at 'center' with the specified 'radius'/>.
   /// The generated sphere mesh consists of triangles of uniform size. The number of output 
   /// triangles, and the accuracy of the mesh relative to the spherical surface, are determined 
   /// by the 'tolerance' parameter, which defines the allowable _relative deviation_ of a 
   /// triangle from the ideal sphere.
   /// <remarks>
   /// This method employs octahedron-based subdivision to produce equilateral triangles. Each subdivision
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
      List<int> tries = [..T], buf = [];

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
         buf.AddRange ([A, P, R, P, Q, R, P, B, Q, Q, C, R]);
      }
   }

   // The icosahedron is constructed from three mutually perpendicular golden rectangles.
   // See https://en.wikipedia.org/wiki/Regular_icosahedron#Construction and
   // https://en.wikipedia.org/wiki/Golden_rectangle for more.
   // Sqr (a) + Sqr (b) = 1 
   // a = b * (golden ratio)
   // golden ratio = (1 + Math.Sqrt (5)) / 2 
   readonly static double _GR = (1 + Math.Sqrt (5)) * 0.5;
   readonly static double _B = Math.Sqrt (1 / (1 + _GR * _GR));
   readonly static double _A = _B * _GR;

   // The 'known' sphere approximations with equilaterial triangles.
   readonly static (ImmutableArray<Vector3> Vertices, ImmutableArray<int> Faces)[] _SphereData = [
      // Octahedron vertices and triangles (6 and 8)
      ([Vector3.ZAxis, -Vector3.ZAxis, Vector3.XAxis, -Vector3.XAxis, Vector3.YAxis, -Vector3.YAxis],
      [0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4]), 

      // Icosahedron vertices and triangles (12 and 20)
      ([new (-_B,0,_A), new (_B,0,_A), new (-_B,0,-_A), new (_B,0,-_A),
        new (0,_A,_B), new (0,_A,-_B), new (0,-_A,_B), new (0,-_A,-_B),
        new (_A,_B,0), new (-_A,_B,0), new (_A,-_B,0), new (-_A,-_B, 0)],
      [0,4,1,0,9,4,9,5,4,4,5,8,4,8,1,
       8,10,1,8,3,10,5,3,8,5,2,3,2,7,3,
       7,10,3,7,6,10,7,11,6,11,0,6,0,1,6,
       6,1,10,9,0,11,9,11,2,9,2,5,7,2,11])
   ];
}
#endregion

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

      return new Mesh3 ([..nodes.AsSpan (0, cNodes)], [.. tries], [.. wires]);

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
