// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Mesh.cs
// ║║║║╬║╔╣║ Implements the CMesh class, a simple mesh format for rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class CMesh --------------------------------------------------------------------------------
/// <summary>This implements a super-simple mesh format for rendering</summary>
/// This format is very close to the way the data has to be presented for rendering, 
/// and is designed to be very simple to read / understand. 
/// - The Vertex array contains a set of nodes, each defined with a position 
///   (Vec3F) and normal (Vec3H)
/// - The Triangle array (taken 3 at a time) defines the triangles as indices into
///   the Vertex array
/// - The Wires array (taken 2 at a time) defines the stencil lines to be drawn.
///   Each endpoint of the wire is again an index into the Vertex array (and to draw the
///   wires, we use only the position, not the normal)
public class CMesh (ImmutableArray<CMesh.Node> vertex, ImmutableArray<int> triangle, ImmutableArray<int> wire) {
   public readonly ImmutableArray<Node> Vertex = vertex;
   public readonly ImmutableArray<int> Triangle = triangle;
   public readonly ImmutableArray<int> Wire = wire;

   public Bound3 Bound {
      get {
         if (mBound.IsEmpty) mBound = new Bound3 (Vertex.Select (a => a.Pos));
         return mBound;
      }
   }
   Bound3 mBound = new ();

   [StructLayout (LayoutKind.Sequential, Pack = 2, Size = 18)]
   public readonly struct Node {
      public Node (Vec3F pos, Vec3H vec) => (mPos, mVec) = (pos, vec);
      public override string ToString () => $"{mPos}, {mVec}";

      public Vec3F Pos => mPos;
      public Vec3H Vec => mVec;

      readonly Vec3F mPos;
      readonly Vec3H mVec;
   }

   /// <summary>Loads data from a TMesh file</summary>
   public static CMesh LoadTMesh (string filename) {
      int n = 0;
      var lines = File.ReadAllLines (filename);
      // Parse the file header
      if (Line () != "TMESH" || Line () != "1") Fatal ();

      // Load the array of PointVec
      var nodes = new Node[int.Parse (Line ())];
      var options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
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

      void Fatal () => throw new Exception ("Invalid TMESH file");
      string Line () => lines[n++];
   }
}
#endregion
