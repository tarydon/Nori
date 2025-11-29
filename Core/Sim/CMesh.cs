// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CMesh.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
namespace Nori;

#region class CMesh --------------------------------------------------------------------------------
/// <summary>CMesh represents a collision-mesh</summary>
public class CMesh {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a CMesh</summary>
   internal CMesh (ImmutableArray<Point3f> pts, ImmutableArray<int> index, Box[] boxes, Matrix3 xfm) {
      Pts = pts; Index = index; Boxes = boxes; Xfm = xfm;
   }

   /// <summary>Create a copy of the CMesh with a new transform</summary>
   public CMesh With (Matrix3 xfm) => new (Pts, Index, Boxes, xfm);

   // Properties ---------------------------------------------------------------
   /// <summary>Set of vertices making up the triangles</summary>
   public readonly ImmutableArray<Point3f> Pts;
   /// <summary>Index values into Pts (taken 3 at a time to define triangles)</summary>
   public readonly ImmutableArray<int> Index;
   /// <summary>Hierarchy of bounding boxes</summary>
   internal readonly Box[] Boxes;
   /// <summary>Transformation matrix for this CMesh (from World to CMesh local)</summary>
   internal readonly Matrix3 Xfm;
   /// <summary>Returns the inverse transform (from CMesh position to World)</summary>
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   public Matrix3 InvXfm => mInvXfm ??= Xfm.GetInverse ();
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   Matrix3? mInvXfm;

   // Methods ------------------------------------------------------------------
   public IEnumerable<(Bound3 Box, int Level)> EnumBoxes () {
      Queue<(int NBox, int Level)> todo = [];
      todo.Enqueue ((1, 0));
      while (todo.TryDequeue (out var tup)) {
         var b = Boxes[tup.NBox];
         if (b.Left < 0) todo.Enqueue ((-b.Left, tup.Level + 1));
         if (b.Right < 0) todo.Enqueue ((-b.Right, tup.Level + 1));
         Point3f min = b.Center - b.Extent, max = b.Center + b.Extent;
         yield return (new Bound3 ([min, max]), tup.Level);
      }
   }

   /// <summary>Gets the vertices of the nth triangle, untransformed by the transformation matrix</summary>
   public void GetRawTriangle (int tri, out Point3f a, out Point3f b, out Point3f c) {
      tri *= 3;
      a = Pts[Index[tri++]]; b = Pts[Index[tri++]]; c = Pts[Index[tri]];
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
      public Point3f Center;
      public Vector3f Extent;
      public int Left;
      public int Right;
   }
}
#endregion
