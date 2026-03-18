namespace Nori.Alt;

class OBBTree {
   /// <summary>
   /// Internal constructor used to initialize an OBBTree
   /// </summary>
   internal OBBTree (Point3f[] pts, CTri[] tris, OBB[] obbs)
      => (Pts, Tris, OBBs) = ([.. pts], [.. tris], [.. obbs]);

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// List of points, referenced by triangle indices
   /// </summary>
   /// These are typically obtained from a mesh, but are de-duplicated with a resolution 1e-3
   public readonly Point3f[] Pts;
   /// <summary>
   /// Set of triangles in the OBB
   /// </summary>
   /// Each triangle points to 3 indices from the Pts array defining the endpoints, and
   /// also stores some cached values like the normal vector, predominant projection direction etc.
   /// Tris[0] is not used, since the OBBs use negative indices to point to triangles (while using
   /// positive indices to point to sub-OBBs), and we don't want any confusion about the index 0
   public readonly CTri[] Tris;
   /// <summary>
   /// The hierarchy of oriented bounding boxes
   /// </summary>
   /// OBBs[1] is the root OBB of the entire mesh and will contain all the N 
   /// triangles in the mesh. The left and right children will contain a (close to equal) partition 
   /// of these children with A and B triangles such that A+B = N. The binary tree keeps going 
   /// down until we finally reach individual triangles. At that point, we don't actually build 
   /// OBBs surronding single triangles, but switch to storing a pointer to the leaf triangle 
   /// directly in Left/Right (these are stored as negative values)
   public readonly OBB[] OBBs;
}

public class OBBTreeBuilder : IBorrowable<OBBTreeBuilder> {
   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Borrow a Builder from the pool of builders
   /// </summary>
   public static OBBTreeBuilder Borrow () {
      var builder = BorrowPool<OBBTreeBuilder>.Borrow ();
      builder.Reset ();
      return builder;
   }

   public void AddMesh (Mesh3 mesh) {
      var (v, t) = (mesh.Vertex, mesh.Triangle);
      Lib.Grow (ref mP, mPN, v.Length); Lib.Grow (ref mVertexMap, 0, v.Length);
      Lib.Grow (ref mT, mTN, t.Length); Lib.Grow (ref mO, mON, t.Length);

      // First add all the unique points from this mesh's vertex set into mP, and build a 
      // mVertexMap that maps those indices to indices into mP
      for (int i = 0; i < v.Length; i++) {
         var pt = v[i].Pos;
         if (!mPtMap.TryGetValue (pt, out int n)) { 
            mP[mPN] = pt; mPtMap.Add (pt, mPN); n = mPN;
            mPN++; 
         }
         mVertexMap[i] = n;
      }
      // Now, we can build the triangles
      for (int i = 0; i < t.Length; i += 3) {
         int a = t[i], b = t[i + 1], c = t[i + 2];
         Point3f pa = v[a].Pos, pb = v[b].Pos, pc = v[c].Pos;
         double area = ((pb - pa) * (pc - pa)).LengthSq; if (area < 1e-8) continue;
         mT[mTN++] = new CTri (mP, mVertexMap[a], mVertexMap[b], mVertexMap[c]);
      }
   }

   public void Build () {
      mO[0] = OBB.Build (mP.AsSpan (0, mPN));

      // Generate a basic permutation of the triangles, which is just (0,1,2 .. mTN-1). 
      // As we build the tree, we will shuffle sections of this so that each OBB node in the
      // tree can refer to a consecutive set of triangles
      Lib.Grow (ref mPermute, 0, mTN);
      for (int i = 0; i < mTN; i++) mPermute[i] = i;
      mO[mON] = OBB.Build (mP.AsSpan (0, mPN));
      mTodo.Enqueue ((mON, 0, mTN)); mON++;
      Span<Vector3f> axes = stackalloc Vector3f[3];

      // The mTodo queue contains the set of OBBs that we need to partition and create
      // children of. We keep processing as long as this queue is not empty
      while (mTodo.Count > 0) {
         var (parent, start, count) = mTodo.Dequeue ();
         int end = start + count;
         ref OBB box = ref mO[parent];

         // There's a set of triangles [start..end), pointing within the mPermute
         // array. We are going to split them by the median cut method. 
         // 1. Compute the mean
         var mean = Point3f.Zero;
         for (int i = start; i < end; i++) mean += mT[mPermute[i]].Centroid;
         mean *= 1f / count;
         // 2. Compute their spread within this OBB
         var variance = Vector3f.Zero;
         for (int i = start; i < end; i++) {
            var d = mT[mPermute[i]].Centroid - mean;
            var (x, y, z) = (box.X.Dot (d), box.Y.Dot (d), box.Z.Dot (d));
            variance += new Vector3f (x * x, y * y, z * z);
         }

         // 3. Determine the split directions based on the spread
         if (variance.X > variance.Y) {
            if (variance.Z < variance.Y) return 321;         // ZYX
            if (variance.Z > vec.X) return 213;         // YXZ
            return 231;                            // YZX
         } else {
            if (vec.Z < vec.X) return 312;         // ZXY
            if (vec.Z > vec.Y) return 123;         // XYZ
            return 132;                            // XZY
         }
      }
   }
   Queue<(int Box, int Start, int Count)> mTodo = [];


   // This builds an OBB using the set of triangles from the interval [start..end)
   //   start = Starting index from the mTriIdx array (inclusive)
   //   end = Ending index from the mTriIdx array (exclusive)
   // Returns the index of the newly crated OBB node (in mO)
   int BuildOBB (int start, int end) {
      if (end <= start) return 0; 

   }

   /// <summary>
   /// Return a borrowed builder back to the pool
   /// </summary>
   public void Dispose () => BorrowPool<OBBTreeBuilder>.Return (this);

   // Implementation -----------------------------------------------------------
   // Helper to grow an array (more optimized than Array.Resize, since it
   // copies only the 'used' elements, not all the elements currently in the array)
   void Grow<T> (ref T[] array, int used, int delta) {
         int size = array.Length, total = used + delta;
         while (size <= total) size *= 2;
         if (size > array.Length) {
            var final = new T[size];
            if (used > 0) Array.Copy (array, final, used);
            array = final;
         }
   }

   void Reset () {
      mPtMap.Clear (); mTodo.Clear (); 
      mPN = 0; mON = mTN = 1;
   }

   // IBorrowable implementation -----------------------------------------------
   static OBBTreeBuilder IBorrowable<OBBTreeBuilder>.Make () => new ();
   OBBTreeBuilder () { }

   static ref OBBTreeBuilder? IBorrowable<OBBTreeBuilder>.Next (OBBTreeBuilder item) => ref item.mNext;
   OBBTreeBuilder? mNext;

   // Private data -------------------------------------------------------------
   Dictionary<Point3f, int> mPtMap = new (Point3fComparer.Delta);
   int[] mVertexMap = [];           // Maps vertex numbers in mesh to indices in PtMap
   int[] mPermute = [];             // A permutation of the mT triangles
   Point3f[] mP = []; int mPN;      // Set of all points, and count of how many of those are used
   CTri[] mT = []; int mTN;         // Set of all CTri, and count of how many of those are used
   OBB[] mO = []; int mON;          // Set of all OBB, and count of how many of those are used
}
