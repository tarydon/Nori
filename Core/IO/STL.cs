// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STL.cs
// ║║║║╬║╔╣║ Implements STLReader/writer: Common, very simple (but old) format to exchange meshes.
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Class to read STL files in ASCII or Binary formats</summary>
public class STLReader {
   public STLReader (string file) : this (File.ReadAllBytes (file)) { }
   public STLReader (byte[] data) => mData = data;

   /// <summary>Gets the triangle triplets defined in the STL file</summary>
   public List<Point3> GetTriangles () {
      if (mData.Take (5).SequenceEqual (_HeaderWord)) return ReadASCII ();
      else return ReadBinary ();
   }
   /// <summary>Builds a mesh with smoothened normals using Mesh3Builder</summary>
   /// <returns></returns>
   public Mesh3 BuildMesh () => new Mesh3Builder (GetTriangles ().AsSpan ()).Build ();

   // Implementation -----------------------------------------------------------
   List<Point3> ReadASCII () {
      List<Point3> pts = [];
      UTFReader r = new UTFReader (mData);
      r.SkipToLineEnd (); // Skip the first line which is in the format 'solid xxxx'
      r.SkipSpace ();
      while (r.Peek == 102) { // The next character must be 'f' from "facet normal x y z" phrase. Otherwise, we are done.
         r.SkipToLineEnd (); // Skip the normal "facet normal ..."
         r.SkipTo ('o'); r.SkipToLineEnd (); // Skip the phrase "outer loop"
         for (int i = 0; i < 3; i++) {
            r.SkipTo ('x'); // Skip to the last character 'x' in 'vertex'.
            r.Read (out double x);
            r.Read (out double y);
            r.Read (out double z);
            pts.Add (new Point3 (x, y, z));
         }
         r.SkipTo ('p'); r.SkipToLineEnd (); // skip the phrase "endloop"
         r.SkipTo ('e'); r.SkipToLineEnd (); // skip the phrase "endfacet"
         r.SkipSpace ();
      }
      return pts;
   }

   List<Point3> ReadBinary () {
      /* UINT8[80]    – Header                 - 80 bytes
         UINT32       – Number of triangles    - 04 bytes
         foreach triangle                      - 50 bytes
               REAL32[3] – Normal vector         - 12 bytes
               REAL32[3] – Vertex 1              - 12 bytes
               REAL32[3] – Vertex 2              - 12 bytes
               REAL32[3] – Vertex 3              - 12 bytes
               UINT16    – Attribute byte count  - 02 bytes
         end
       */
      int n = 76; // Skip past the first 80 bytes.
      int cTriangles = BitConverter.ToInt32 (mData, n += 4);
      List<Point3> pts = new List<Point3> (cTriangles * 3);
      for (int i = 0; i < cTriangles; i++) {
         n += 12; // Skip the normal part. We can always build it from the 3 vertices of the triangle
         pts.Add (new Point3 ((double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4)));
         pts.Add (new Point3 ((double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4)));
         pts.Add (new Point3 ((double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4), (double)BitConverter.ToSingle (mData, n += 4)));
         n += 2; // Skip the attribute part.
      }
      return pts;
   }
   
   // Private data ---------------------------------------------------
   readonly byte[] mData;
   static readonly byte[] _HeaderWord = [115, 111, 108, 105, 100]; // word "solid"
}

/// <summary>Class to write meshes to STL files.</summary>
/// STL files can be authored in two formats. ASCII-text or binary.
/// Usage: 
/// using (var stm = File.Create ())
///   STLWriter.WriteASCII (mesh, stm);
public static class STLWriter {
   /// <summary>Writes STL in ASCII (text) format</summary>
   /// <param name="mesh">The mesh that needs to be saved to STL files.</param>
   /// <param name="stm">The stream to which the file needs to be written.</param>
   public static void WriteASCII (Mesh3 mesh, Stream stm) {
      var sw = new StreamWriter (stm);
      const string name = "NoriExport";
      sw.WriteLine ($"solid {name}");
      for (int i = 0; i < mesh.Triangle.Length; i += 3) {
         Point3 p1 = (Point3)mesh.Vertex[mesh.Triangle[i]].Pos, p2 = (Point3)mesh.Vertex[mesh.Triangle[i + 1]].Pos, p3 = (Point3)mesh.Vertex[mesh.Triangle[i + 2]].Pos;
         Vector3 normal = ((p2 - p1) * (p3 - p2)).Normalized ();
         sw.WriteLine ($"   facet normal {normal.X.S6 ()} {normal.Y.S6 ()} {normal.Z.S6 ()}");
         sw.WriteLine ("      outer loop");
         sw.WriteLine ($"         vertex {p1.X.S6 ()} {p1.Y.S6 ()} {p1.Z.S6 ()}");
         sw.WriteLine ($"         vertex {p2.X.S6 ()} {p2.Y.S6 ()} {p2.Z.S6 ()}");
         sw.WriteLine ($"         vertex {p3.X.S6 ()} {p3.Y.S6 ()} {p3.Z.S6 ()}");
         sw.WriteLine ("      endloop");
         sw.WriteLine ("   endfacet");
      }
      sw.WriteLine ($"endsolid {name}");
      sw.Flush ();
   }

   /// <summary>Writes STL in binary format</summary>
   /// <param name="mesh">The mesh that needs to be saved to STL files</param>
   /// <param name="stm">The stream to which the file needs to be written.</param>
   public static void WriteBinary (Mesh3 mesh, Stream stm) {
      var bw = new BinaryWriter (stm);
      // Write the 80 byte header.
      byte[] header = ASCIIEncoding.ASCII.GetBytes ("Exported from Nori");
      bw.Write (header);
      for (int i = header.Length; i < 80; i++)
         bw.Write ((byte)0);
      // Write number of triangles
      bw.Write (mesh.Triangle.Length / 3);
      for (int i = 0; i < mesh.Triangle.Length; i += 3) {
         Point3 p1 = (Point3)mesh.Vertex[mesh.Triangle[i]].Pos, p2 = (Point3)mesh.Vertex[mesh.Triangle[i + 1]].Pos, p3 = (Point3)mesh.Vertex[mesh.Triangle[i + 2]].Pos;
         Vector3 normal = ((p2 - p1) * (p3 - p2)).Normalized ();
         bw.Write ((float)normal.X); bw.Write ((float)normal.Y); bw.Write ((float)normal.Z); // Write the normal
         bw.Write ((float)p1.X); bw.Write ((float)p1.Y); bw.Write ((float)p1.Z);
         bw.Write ((float)p2.X); bw.Write ((float)p2.Y); bw.Write ((float)p2.Z);
         bw.Write ((float)p3.X); bw.Write ((float)p3.Y); bw.Write ((float)p3.Z);
         bw.Write ((short)0);
      }
      bw.Flush ();
   }
}
