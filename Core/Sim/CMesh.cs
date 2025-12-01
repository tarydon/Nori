// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CMesh.cs
// ║║║║╬║╔╣║ Represents a collision BVH using AABBs as the bounding primitive
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
namespace Nori;

#region class CMesh --------------------------------------------------------------------------------
/// <summary>CMesh represents a BVH where each level is represented by an AABB</summary>
/// The BVH is structured like a binary tree, where each AABB (axis-aligned bounding box) has 
/// two children that  are smaller AABBs (split along the major axis). At the bottom leaf 
/// level of boxes, the left and right children are directly triangles (that is, we don't 
/// create AABBs that enclose just a single triangle).
/// Since a CMesh itself is immutable, it is constructed using a CMesh.Builder. A CMesh
/// also holds onto a transformation matrix and can easily return transformed copies of
/// itself (sharing the same AABB tree)
public class CMesh {
   // Constructor --------------------------------------------------------------
   // Construct a CMesh
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
   /// <summary>Enumerate through the boxes (each box is returned with the 'level')</summary>
   /// Useful mostly for debugging
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

   public IEnumerable<Bound3> EnumBoxes (int maxLevel) {
      Queue<(int NBox, int Level)> todo = [];
      todo.Enqueue ((1, 0));
      while (todo.TryDequeue (out var tup)) {
         var b = Boxes[tup.NBox];
         if (tup.Level <= maxLevel) {
            // Output boxes at maxLevel. Also output leaf boxes from 
            // earlier levels (ones that don't have two boxes as children)
            if (tup.Level == maxLevel || b.Left >= 0 || b.Right >= 0) {
               Point3f min = b.Center - b.Extent, max = b.Center + b.Extent;
               yield return new Bound3 ([min, max]);
            }
            if (b.Left < 0) todo.Enqueue ((-b.Left, tup.Level + 1));
            if (b.Right < 0) todo.Enqueue ((-b.Right, tup.Level + 1));
         }
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
   // a binary tree. 
   internal struct Box {
      // Center of this bounding box
      public Point3f Center;
      // Half-extents of this bounding box in X, Y, Z. So the min and max opposite 
      // diagonal corners can be expressed as Center-Extent, Center+Extent
      public Vector3f Extent;
      // Pointer to the left child in the hierarchy. If this is a negative value -N
      // then N is an index into the Boxes array of the left child (which is also a Box).
      // If this is 0 or a +ve value M, then the value is a triangle number, and we
      // can get the actual points of the triangle like:
      //   int n = M * 3;
      //   Pts[Index[n]], Pts[Index[n+1]], Pts[Index[n+2]]
      public int Left;
      // Pointer to the right child in the hierarchy. See the Left member above for
      // how to interpret this
      public int Right;
   }

   #region class CMesh.Builder ---------------------------------------------------------------------
   /// <summary>Builds a</summary>
   public class Builder {
      // Interface -------------------------------------------------------------
      /// <summary>Build a collision mesh from a Mesh3</summary>
      public static CMesh Build (Mesh3 mesh) {
         var builder = It;
         builder.Reset (); builder.Add (mesh);
         return builder.Build ();
      }

      // Implementation --------------------------------------------------------
      void Add (Mesh3 mesh) {
         int n = mPts.Count;
         mPts.AddRange (mesh.Vertex.Select (a => a.Pos));
         mIndex.AddRange (mesh.Triangle.Select (a => a + n));
      }

      CMesh Build () {
         // This is the number of triangles
         int tris = mIndex.Count / 3;
         // Allocate space for the boxes - box 0 is not going to be used, and we are going to
         // use Boxes[1] as the root 
         mBoxes = new Box[tris + 1];

         // Generate a basic permutation of the triangles, which is simply in the order 
         // (0, 1, 2, 3 ...). Later, as we build the tree, we will shuffle this around to
         // match the tree
         for (int i = 0; i < tris; i++) mPermute.Add (i);

         // Compute the triangle bounds and centers 
         Bound3 bound = new ();
         for (int i = 0; i < mIndex.Count; i++) {
            bound += mPts[mIndex[i]];
            if (i % 3 == 2) {
               mTriCen.Add ((Point3f)bound.Midpoint);
               mTriBound.Add (bound);
               bound = new ();
            }
         }

         // Build the topmost bounding box with all the triangles. Note that this will
         // get stored in mBoxes[1] (we don't use mBoxes[0]) so we have no confusion about
         // whether an Index of 0 refers to box 0 or triangle 0 (since there is no box 0). 
         BuildBox (0, tris);

         // Clean up the temporary stuff we needed only during building. Upto this point, the
         // Box.Left and Box.Right indices had values like this:
         // Box.Left, Box.Right >= 0 : These are indirect indices into the list of triangles
         //                            via the mTriIdx array (which contains the pemutation)
         // Box.Left, Box.Right < 0  : These are the negative of the indices of sub-nodes (in
         //                            the Nodes array. This is why we don't use Nodes[0]
         // Now, we are removing the layer of indirection for the indices that are pointing
         // to the triangle array. So, instead of a triangle being picked up as
         // Tris[mTriIdx[Node.Left]], we will simply set Node.Left = mTriIdx[Node.Left]
         // so that we later pick up triangles by saying Tri[Node.Left].
         for (int i = 1; i < mBoxes.Length; i++) {
            int left = mBoxes[i].Left;
            if (left >= 0) mBoxes[i].Left = mPermute[left];
            int right = mBoxes[i].Right;
            if (right >= 0) mBoxes[i].Right = mPermute[right];
         }
         return new CMesh ([.. mPts], [.. mIndex], [.. mBoxes], Matrix3.Identity);
      }

      // This makes a new box using the triangles from the interval [start .. end).
      //    start = The starting index from the mTriIdx array (this is inclusive)
      //    end = The ending index in the mTriIdx array (exclusive)
      // Returns the index of the newly created node
      int BuildBox (int start, int end) {
         if (end <= start) return 0;
         // Compute the bound of the triangles contained in this box, so we can then 
         // get the Center and Extent
         Bound3 bound = new ();
         for (int i = start; i < end; i++)
            bound += mTriBound[mPermute[i]];
         int nodeIdx = ++mBoxesUsed;
         var box = new Box {
            Center = (Point3f)bound.Midpoint,
            Extent = new (bound.X.Length / 2, bound.Y.Length / 2, bound.Z.Length / 2),
         };

         // Now, if this box has 2 or more triangles, we have to create a split by 
         // partitioning this set of triangles into two
         if (end > start + 2) {
            Point3f mean = new (0, 0, 0);
            for (int i = start; i < end; i++) mean += (Vector3f)mTriCen[mPermute[i]];
            int count = end - start;
            mean = new (mean.X / count, mean.Y / count, mean.Z / count);

            // Now, compute the variances on the different axes
            Vector3f vari = new (0, 0, 0);
            for (int i = start; i < end; i++) {
               Vector3f v = mTriCen[mPermute[i]] - mean;
               vari += new Vector3f (v.X * v.X, v.Y * v.Y, v.Z * v.Z);
            }

            // We are going to try splitting by the largest variance axis first, then
            // the other ones. For this, we get an ordered list of axes by which to attempt the
            // split. This might return a list like 213, which means the order of the axes from
            // worst to best is Y,X,Z (the units place contains the best axis for attempting a split).
            bool iOK = false;
            int order = GetAxesSorted (vari), split = 0;
            for (int i = 0; i < 3; i++) {
               // Try splitting with the axis in units place (the best one). If that fails, we
               // divide the split order by 10, which will bring the next possible axis into units
               // place. So, in this 213 example above, we would attempt the split by Z, then by X
               // and finally by Y. If any of these splits go well (the array gets divided into two pieces)
               // the Split routine returns true and we stop.
               split = Partition (start, end, mean, order % 10);
               if (split > start && split < end) { iOK = true; break; }
               order /= 10;
            }
            if (!iOK) split = (start + end) / 2;

            // OK, we got a possible split point. Let's create new nodes for each of the
            // subtrees. If split == start + 1, then we have a single triangle (so we store
            // a +ve triangle index as Left), otherwise we have multiple triangles, so we need
            // a box with those (so we store a -ve index there)
            if (split > start + 1) box.Left = -BuildBox (start, split);
            else box.Left = start;
            // Likewise if there are multiple triangles on the right, build a box around them
            // otherwise, set Right to just point to the single triangle on the right
            if (end > split + 1) box.Right = -BuildBox (split, end);
            else box.Right = split;
         } else {
            // We are at a leaf box with just 2 triangles so no need to partition it
            box.Left = start; box.Right = end - 1;
         }

         // Store the box and return the index at which we've stored it
         mBoxes[nodeIdx] = box;
         return nodeIdx;
      }

      // This returns an integer which orders the axes from smallest to largest.
      // Suppose this returns 312: this means the Z component is the largest, followed
      // by the X axis and finally the Y axis (note that this is not using the 
      // absolute values of these components, but the actual value). This is meant to
      // be used with vectors where none of the components are negative
      static int GetAxesSorted (Vector3f vec) {
         if (vec.X > vec.Y) {
            if (vec.Z < vec.Y) return 321;         // ZYX
            if (vec.Z > vec.X) return 213;         // YXZ
            return 231;                            // YZX
         } else {
            if (vec.Z < vec.X) return 312;         // ZXY
            if (vec.Z > vec.Y) return 123;         // XYZ
            return 132;                            // XZY
         }
      }

      void Reset () {
         mPts.Clear (); mIndex.Clear (); mPermute.Clear ();
         mTriCen.Clear (); mTriBound.Clear ();
         mBoxesUsed = 0;
      }

      // Given a range of triangles, this partitions them by a given axis.
      //    start = The starting index of the triangles (in the mTriIdx array)
      //    end = The ending index (+1) of the triangles in the mTriIdx array
      //    mean = The mean point about which the split should take place
      //    axis = The axis along which the split takes place (1/2/3 = X/Y/Z)
      // This returns the partition index within the interval start..end. If the returned value
      // is equal to start or end, then this was not a successful split (meaning one of the
      // two partitions was empty)
      int Partition (int start, int end, Point3f mean, int axis) {
         // After this process, everything at the index split and beyond
         // is in the 'right half' of the tree, and everything below split is in the
         // 'left half' of the tree.
         int split = start;
         bool less = false;
         for (int i = start; i < end; i++) {
            Point3f pt = mTriCen[mPermute[i]];
            less = axis switch { 1 => pt.X < mean.X, 2 => pt.Y < mean.Y, 3 => pt.Z < mean.Z, _ => less };
            if (less) {
               // The triangle at index i is larger than the mean, so we should move it
               // to the right-side.                
               (mPermute[split], mPermute[i]) = (mPermute[i], mPermute[split]);
               split++;
            }
         }
         return split;
      }

      static Builder It => mIt ??= new ();
      [ThreadStatic] static Builder? mIt;

      // Private data ----------------------------------------------------------
      // List of vertices
      List<Point3f> mPts = [];         // List of vertices
      List<int> mIndex = [];           // List of indices, taken 3 at a time, making the triangles
      List<Point3f> mTriCen = [];      // Center points of the N triangles
      List<Bound3> mTriBound = [];     // Bounds of the N triangles
      Box[] mBoxes = [];               // List of boxes forming the BVH tree
      int mBoxesUsed;                  // How many of those slots have we used
      // mPermute contains a permutation of all the triangles. Initially, this is 0...N where
      // and as we create the BVH, we rearrange here the triangle. Suppose, for example, we do
      // the first split along X at some particular Xmid. Then, all the triangles with centroids
      // less than Xmid will go into the first half of this mPermute array, while the rest
      // are in the second half. This is used during the building of the BVH.
      List<int> mPermute = [];         
   }
   #endregion
}
#endregion
