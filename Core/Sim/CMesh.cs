// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CMesh.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class CMesh --------------------------------------------------------------------------------
/// <summary>CMesh represents a collision-mesh</summary>
public class CMesh {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a CMesh</summary>
   internal CMesh (ImmutableArray<Vec3F> pts, ImmutableArray<int> index, Box[] boxes, Matrix3 xfm) {
      Pts = pts; Index = index; Boxes = boxes; Xfm = xfm;
   }

   /// <summary>
   /// Create a copy of the CMesh with a new transform
   /// </summary>
   public CMesh With (Matrix3 xfm) => new (Pts, Index, Boxes, xfm);

   // Properties ---------------------------------------------------------------
   /// <summary>Set of vertices making up the triangles</summary>
   public readonly ImmutableArray<Vec3F> Pts;
   /// <summary>Index values into Pts (taken 3 at a time to define triangles)</summary>
   public readonly ImmutableArray<int> Index;
   /// <summary>Hierarchy of bounding boxes</summary>
   internal readonly Box[] Boxes;
   /// <summary>
   /// Transformation matrix for this CMesh (from World to CMesh local)
   /// </summary>
   internal readonly Matrix3 Xfm;
   /// <summary>
   /// Returns the inverse transform (from CMesh position to World)
   /// </summary>
   public Matrix3 InvXfm => mInvXfm ??= Xfm.GetInverse ();
   Matrix3? mInvXfm;

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Gets the vertices of the nth triangle, untransformed by the transformation matrix
   /// </summary>
   public void GetRawTriangle (int tri, out Point3 a, out Point3 b, out Point3 c) {
      tri *= 3;
      a = (Point3)Pts[Index[tri++]];
      b = (Point3)Pts[Index[tri++]];
      c = (Point3)Pts[Index[tri]];
   }

   // Nested types -------------------------------------------------------------
   // This represents a node in the CMesh tree. 
   // This serves as a bounding box for a subset of triangles from the CMesh.
   // The top-most node (node 1 in the Boxes array) contains the bounding-box for the entire mesh.
   // Then, subsequent nodes partition this set of triangles into smaller and smaller set building up
   // a binary tree. The Left and Right indices are interpreted thus:
   // - If they are 0 or +ve, they are _leaf_ nodes, and they point into the Index array, 
   //   where they point to the start of a set of 3 indices that form a leaf triangle
   // - If they are -ve, they are the negative of the index into the Nodes array, where they
   //   point to a child box
   internal struct Box {
      public Vec3F Center;
      public Vec3F Extent;
      public int Left;
      public int Right;
   }
}
#endregion
