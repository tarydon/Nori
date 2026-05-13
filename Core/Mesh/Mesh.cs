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
public partial class Mesh3 {
   // Constructor --------------------------------------------------------------
   /// <summary>Core constructor for a Mesh3</summary>
   public Mesh3 (ImmutableArray<Node> vertex, ImmutableArray<int> tris, ImmutableArray<int> wires) 
      => (Vertex, Triangle, Wire) = (vertex, tris, wires);

   /// <summary>Create a mesh by merging multiple meshes together</summary>
   public Mesh3 (IEnumerable<Mesh3> meshes) {
      List<Node> verts = []; List<int> tris = [], wires = [];
      foreach (var mesh in meshes) {
         int n = verts.Count; verts.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
         wires.AddRange (mesh.Wire.Select (a => a + n));
      }
      Vertex = [.. verts]; Triangle = [.. tris]; Wire = [.. wires];
   }

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

   /// <summary>Loads a Mesh3 from a stream in canonical MSH format</summary>
   public static Mesh3 Load (Stream stm) {
      int sign = stm.ReadInt32 (), version = stm.ReadInt32 ();
      if (sign != SIGN || version != 1) throw new IOException ($"Not a MSH file");
      Node[] verts = new Node[stm.ReadInt32 ()];
      int[] tris = new int[stm.ReadInt32 ()], wires = new int[stm.ReadInt32 ()];
      string? tag = stm.ReadString ();
      stm.ReadExactly (MemoryMarshal.AsBytes (verts.AsSpan ()));
      if (verts.Length < 65535) {
         ushort[] utris = new ushort[tris.Length], uwires = new ushort[wires.Length];
         stm.ReadExactly (MemoryMarshal.AsBytes (utris.AsSpan ()));
         stm.ReadExactly (MemoryMarshal.AsBytes (uwires.AsSpan ()));
         for (int i = 0; i < tris.Length; i++) tris[i] = utris[i];
         for (int i = 0; i < wires.Length; i++) wires[i] = uwires[i];
      } else {
         stm.ReadExactly (MemoryMarshal.AsBytes (tris.AsSpan ()));
         stm.ReadExactly (MemoryMarshal.AsBytes (wires.AsSpan ()));
      }
      return new ([.. verts], [.. tris], [.. wires]) { Tag = tag };
   }
   const int SIGN = 'M' + ('E' << 8) + ('S' << 16) + ('H' << 24);

   /// <summary>Loads a Mesh3 from a file, compressed MSH format</summary>
   /// This is basically a DeflateStream, but contains one signature at the beginning
   public static Mesh3 Load (string file) {
      using var stm = Lib.OpenRead (file);
      if (stm.ReadInt32 () != SIGN) throw new IOException ($"{file} is not a MSH file");
      using var dfs = new DeflateStream (stm, CompressionMode.Decompress);
      return Load (dfs);
   }

   /// <summary>Loads a Mesh3 and a TopoMesh from a MSH2 file</summary>
   /// Typically, these files contain a Mesh3 for rendering, and a TopoMesh to build a 
   /// collision model
   public static (Mesh3 Model, TopoMesh Crash) LoadMSH2 (string file) {
      Mesh3 model; TopoMesh crash;
      using var stm = File.OpenRead (file);
      using var zar = new ZipArchive (stm, ZipArchiveMode.Read);
      using (var stm1 = zar.Open ("model.msh")) model = Load (stm1);
      using (var stm2 = zar.Open ("crash.msht")) crash = TopoMesh.Load (stm2);
      return (model, crash);
   }

   /// <summary>Loads a Mesh3 from a stream in Flux .mesh format</summary>
   /// These files contain multiple meshes packed into one file; this routine merges all those
   /// meshes into a single mesh and returns it
   public static unsafe Mesh3 LoadFluxMesh (Stream rawStm) {
      using DeflateStream stm = new (rawStm, CompressionMode.Decompress, false);
      ByteStm bs = new (stm.ReadBytes (stm.ReadInt32 ()));
      int sign = bs.ReadInt32 (), version = bs.ReadByte ();
      if (sign != 0x1A48534D || version is < 1 or > 2)
         throw new IOException ("Mesh file is damaged");

      string? tag = null;
      if (version >= 2) tag = bs.ReadString ();    // Read the name

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
      return new Mesh3 ([.. vertex], [.. ftris], [.. fwires]) { Tag = tag };
   }

   /// <summary>Loads a Mesh from a Flux .mesh file</summary>
   public static Mesh3 LoadFluxMesh (string file) {
      using var stm = File.OpenRead (file);
      return LoadFluxMesh (stm);
   }

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

   /// <summary>Loads a Mesh from an OBJ file</summary>
   public static Mesh3 LoadObj (string file) => LoadObj (File.ReadAllLines (file));

   /// <summary>Saves a Mesh3 to a binary stream (raw, uncompressed save)</summary>
   public void Save (Stream stm) {
      stm.WriteInt32 (SIGN); stm.WriteInt32 (1);
      stm.WriteInt32 (Vertex.Length).WriteInt32 (Triangle.Length).WriteInt32 (Wire.Length).WriteString (Tag);
      stm.Write (Vertex);
      if (Vertex.Length < 65535) {
         stm.Write (MemoryMarshal.AsBytes (Triangle.Select (a => (ushort)a).ToList ().AsSpan ()));
         stm.Write (MemoryMarshal.AsBytes (Wire.Select (a => (ushort)a).ToList ().AsSpan ()));
      } else
         stm.Write (Triangle).Write (Wire);
   }

   /// <summary>Saves a Mesh3 to a file - the file is compressed, and has a signature at the beginning</summary>
   public void Save (string file) {
      using var stm = File.Create (file);
      stm.WriteInt32 (SIGN);
      using var dfs = new DeflateStream (stm, CompressionLevel.SmallestSize);
      Save (dfs);
   }

   /// <summary>Saves the Mesh3 in TMesh format</summary>
   /// This is a portable and simple Mesh format used mainly for testing / interchange. 
   /// The mesh contains a header, followed by the vertices, triangles and wires. An annotated
   /// file is shown below:
   /// 
   /// TMESH                    <-- First line is always TMESH
   /// 1                        <-- Version number, 1 for now
   /// 4                        <-- Number of vertices
   /// 5,-50,-30,  1,0,0        <-- Each vertex is a POSITION + NORMAL
   /// 5,50,-30,  1,0,0
   /// 5,50,30,  1,0,0
   /// 5,-50,30,  1,0,0
   /// 2                        <-- Number of triangles
   /// 0 1 2                    <-- Each triangle connects 3 vertices
   /// 2 3 0
   /// 4                        <-- Number of stencil wires
   /// 0 1                      <-- Each wire connects two vertices
   /// 1 2
   /// 2 3
   /// 3 0
   public void SaveTMesh (string file) {
      // Version, Vertex count, vertices
      StringBuilder sb = new ($"TMESH\n1\n{Vertex.Length}\n");
      foreach (var (pos0, vec) in Vertex) {
         var pos = ((Point3)pos0).R6 ();
         sb.Append ($"{pos.X},{pos.Y},{pos.Z},  {vec.X},{vec.Y},{vec.Z}\n");
      }

      // Triangle count, triangle indices
      sb.Append ($"{Triangle.Length / 3}\n");
      for (int i = 0; i < Triangle.Length; i += 3)
         sb.Append ($"{Triangle[i]} {Triangle[i + 1]} {Triangle[i + 2]}\n");

      // Stencil count, stencil indices
      sb.Append ($"{Wire.Length / 2}\n");
      for (int i = 0; i < Wire.Length; i += 2)
         sb.Append ($"{Wire[i]} {Wire[i + 1]}\n");
      sb.Append ("EOF\n");
      File.WriteAllText (file, sb.ToString ());
   }

   /// <summary>Converts a Mesh3 to a TopoMesh (drops the normals, de-duplicates the vertices)</summary>
   public TopoMesh ToTopoMesh ()
      => new (Vertex.Select (a => a.Pos).ToList (), Triangle);

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

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the bounding cuboid of this Mesh3</summary>
   public Bound3 Bound { get => Bound3.Cached (ref field, () => new (Vertex.Select (a => a.Pos))); } = new ();
   /// <summary>Is this an empty mesh3 (no triangles)</summary>
   public bool IsEmpty => Triangle.Length == 0;
   /// <summary>A descriptive tag for this Mesh3 (written out into MSH files)</summary>
   public string? Tag;

   /// <summary>These integers, taken 3 at a time, are indices into Vertex[], making up the triangles</summary>
   public readonly ImmutableArray<int> Triangle;
   /// <summary>Set of Vertices of this Mesh3</summary>
   /// Each vertex contains a Point3f (for position) and a Vec3H (for normal vector)
   public readonly ImmutableArray<Node> Vertex;
   /// <summary>These integers, taken 2 at a time, are the stencil lines to be drawn in the mesh</summary>
   public readonly ImmutableArray<int> Wire;

   // Implementation -----------------------------------------------------------
   public override string ToString () 
      => $"Mesh3 {Vertex.Length} verts, {Triangle.Length / 3} tris, {Wire.Length / 2} wires";

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
      public Node (Point3 pos, Vector3 vec) { Pos = (Point3f)pos; Vec = vec; }
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
