// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CMeshBuilder.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class CMeshBuilder {
   // Interface ----------------------------------------------------------------
   /// <summary>Build a collision mesh from a Mesh3</summary>
   public static CMesh Build (Mesh3 mesh) {
      var builder = It;
      builder.Reset (); builder.Add (mesh);
      return builder.Build ();
   }

   // Implementation -----------------------------------------------------------
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
      mBoxes = new CMesh.Box[tris + 1];
      
      // Generate a basic permutation of the triangles, which is simply in the order 
      // (0, 1, 2, 3 ...). Later, as we build the tree, we will shuffle this around to
      // match the tree
      for (int i = 0; i < tris; i++) mTriIdx.Add (i);

      // Compute the triangle bounds and centers 
      Bound3 bound = new ();
      for (int i = 0; i < mIndex.Count; i++) {
         bound += mPts[mIndex[i]];
         if (i % 3 == 2) {
            mTriCen.Add ((Vec3F)bound.Midpoint);
            mTriBound.Add (bound);
            bound = new ();
         }
      }
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
         if (left >= 0) mBoxes[i].Left = mTriIdx[left];
         int right = mBoxes[i].Right; 
         if (right >= 0) mBoxes[i].Right = mTriIdx[right];
      }
      return new CMesh ([.. mPts], [.. mIndex], [.. mBoxes], Matrix3.Identity);
   }

   // This makes a new box using the triangles from the interval [start .. end).
   //    start = The starting index from the mTriIdx array (this is inclusive)
   //    end = The ending index in the mTriIdx array (exclusive)
   // Returns the index of the newly created node
   int BuildBox (int start, int end) {
      if (end <= start) return 0;
      Bound3 bound = new ();
      for (int i = start; i < end; i++) 
         bound += mTriBound[mTriIdx[i]];

      int nodeIdx = ++mBoxesUsed;
      var box = new CMesh.Box {
         Center = (Vec3F)bound.Midpoint,
         Extent = new (bound.X.Length / 2, bound.Y.Length / 2, bound.Z.Length / 2),
      };

      // Now, if this node has 2 or more triangles, we have to create a split by 
      // partitioning this set of triangles into wo
      if (end > start + 2) {
         Vec3F mean = new (0, 0, 0);
         for (int i = start; i < end; i++) mean += mTriCen[mTriIdx[i]];
         mean /= (end - start);

         // Now, compute the variances on the different axes
         Vec3F vari = Vec3F.Zero;
         for (int i = start; i < end; i++) {
            Vec3F v = mTriCen[mTriIdx[i]] - mean;
            vari += new Vec3F (v.X * v.X, v.Y * v.Y, v.Z + v.Z);
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

      // Store the box and return it
      mBoxes[nodeIdx] = box;
      return nodeIdx;
   }

   // This returns an integer which orders the axes from smallest to largest.
   // Suppose this returns 312: this means the Z component is the largest, followed
   // by the X axis and finally the Y axis (note that this is not using the 
   // absolute values of these components, but the actual value). This is meant to
   // be used with vectors where none of the components are negative
   static int GetAxesSorted (Vec3F vec) {
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
      mPts.Clear (); mIndex.Clear (); mTriIdx.Clear (); 
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
   int Partition (int start, int end, Vec3F mean, int axis) {
      // After this process, everything at the index split and beyond
      // is in the 'right half' of the tree, and everything below split is in the
      // 'left half' of the tree.
      int split = start;
      bool less = false;
      for (int i = start; i < end; i++) {
         Vec3F pt = mTriCen[mTriIdx[i]];
         less = axis switch { 1 => pt.X < mean.X, 2 => pt.Y < mean.Y, 3 => pt.Z < mean.Z, _ => less };
         if (less) {
            // The triangle at index i is larger than the mean, so we should move it
            // to the right-side.                
            (mTriIdx[split], mTriIdx[i]) = (mTriIdx[i], mTriIdx[split]);
            split++;
         }
      }
      return split;
   }

   static CMeshBuilder It => mIt ??= new ();
   [ThreadStatic] static CMeshBuilder? mIt;

   // Private data -------------------------------------------------------------
   List<Vec3F> mPts = [], mTriCen = [];
   List<Bound3> mTriBound = [];
   List<int> mIndex = [], mTriIdx = [];
   CMesh.Box[] mBoxes = [];
   int mBoxesUsed;
}
