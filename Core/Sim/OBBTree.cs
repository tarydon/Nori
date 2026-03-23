// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBTree.cs
// ║║║║╬║╔╣║ Implements OBBTree (bounding-box hierarchy using OBB primitives), OBBTreeBuilder
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class OBBTree ------------------------------------------------------------------------------
/// <summary>Represents a collision hierarchy where each node is an Oriented Bounding Box</summary>
public class OBBTree {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an OBBTree from a mesh</summary>
   public static OBBTree From (Mesh3 mesh, string? tag = null) {
      using var mb = OBBTreeBuilder.Borrow ();
      mb.AddMesh (mesh);
      return mb.Build (tag);
   }

   /// <summary>Create a copy of this OBBTree with a new transform</summary>
   public OBBTree With (Matrix3 xfm) => new (Pts, Tris, OBBs, mTag) { mXfm = xfm };

   /// <summary>An 'empty' OBBTree (to represent no collisions)</summary>
   public static readonly OBBTree Empty = new ([], [], [], "EMPTY");

   // Properties ---------------------------------------------------------------
   /// <summary>The hierarchy of oriented bounding boxes</summary>
   /// OBBs[0] is the root OBB of the entire mesh and will contain all the N 
   /// triangles in the mesh. The left and right children will contain a (close to equal) partition 
   /// of these children with A and B triangles such that A+B = N. The binary tree keeps going 
   /// down until we finally reach individual triangles. At that point, we don't actually build 
   /// OBBs surronding single triangles, but switch to storing a pointer to the leaf triangle 
   /// directly in Left/Right (these are stored as negative values)
   public readonly OBB[] OBBs;
   /// <summary>List of points, referenced by triangle indices</summary>
   /// These are typically obtained from a mesh, but are de-duplicated with a resolution 1e-3
   public readonly Point3f[] Pts;
   /// <summary>Set of triangles in the OBB</summary>
   /// Each triangle points to 3 indices from the Pts array defining the endpoints, and
   /// also stores some cached values like the normal vector, predominant projection direction etc.
   /// Tris[0] is not used, since the OBBs use negative indices to point to triangles (while using
   /// 0 or positive indices to point to sub-OBBs), and we don't want any confusion about the 
   /// index 0
   public readonly CTri[] Tris;

   /// <summary>Is this an 'empty' OBBTree?</summary>
   public bool IsEmpty => Tris.Length == 0;

   /// <summary>A tag we attach to an OBBTree (useful for tracing/debugging)</summary>
   public string Tag => mTag ?? string.Empty;
   string? mTag = null;

   /// <summary>The transformation matrix for this OBBTree</summary>
   public Matrix3 Xfm => mXfm ?? Matrix3.Identity;
   Matrix3? mXfm;
   /// <summary>Inverse-transformation matrix for this OBBTree</summary>
   public Matrix3 InvXfm => mInvXfm ??= Xfm.GetInverse ();
   Matrix3? mInvXfm;

   // Methods ------------------------------------------------------------------
   /// <summary>Outputs OBBs a given heirarchy level.</summary>
   /// It also includes the leaf nodes to reflect a realistic 
   /// collision complexity contributed by that level.
   public IEnumerable<OBB> EnumBoxes (int maxLevel) {
      Queue<(int NBox, int Level)> todo = [];
      todo.Enqueue ((0, 0));
      while (todo.TryDequeue (out var tup)) {
         OBB b = OBBs[tup.NBox];
         if (tup.Level <= maxLevel) {
            // Output boxes at maxLevel. Also output leaf boxes from 
            // earlier levels (ones that don't have two boxes as children)
            if (tup.Level == maxLevel || b.Left <= 0 || b.Right <= 0) yield return b;
            if (b.Left > 0 && tup.Level < maxLevel) todo.Enqueue ((b.Left, tup.Level + 1));
            if (b.Right > 0 && tup.Level < maxLevel) todo.Enqueue ((b.Right, tup.Level + 1));
         }
      }
   }

   // Implementation -----------------------------------------------------------
   internal OBBTree (ReadOnlySpan<Point3f> pts, ReadOnlySpan<CTri> tris, ReadOnlySpan<OBB> obbs, string? tag)
      => (Pts, Tris, OBBs, mTag) = ([..pts], [..tris], [..obbs], tag);

   internal OBBTree (Point3f[] pts, CTri[] tris, OBB[] obbs, string? tag)
      => (Pts, Tris, OBBs, mTag) = (pts, tris, obbs, tag);
}
#endregion

#region class OBBTreeBuilder -----------------------------------------------------------------------
/// <summary>OBBTreeBuilder is used to build OBBTree hierarchies from one or more Mesh3</summary>
public class OBBTreeBuilder : IBorrowable<OBBTreeBuilder> {
   // Methods ------------------------------------------------------------------
   /// <summary>Borrow a Builder from the pool of builders</summary>
   public static OBBTreeBuilder Borrow () {
      var builder = BorrowPool<OBBTreeBuilder>.Borrow ();
      builder.Reset ();
      return builder;
   }

   /// <summary>Adds a Mesh into the OBBTreeBuilder (multiple meshes can be added before a collider is built)</summary>
   public void AddMesh (Mesh3 mesh) {
      var (v, t) = (mesh.Vertex, mesh.Triangle);
      Lib.Grow (ref mPt, mPtN, v.Length); Lib.Grow (ref mPtMap, 0, v.Length);
      Lib.Grow (ref mTri, mTriN, t.Length); Lib.Grow (ref mBox, mBoxN, t.Length);

      // First add all the unique points from this mesh's vertex set into mP, and
      // build the vertex map (mPMap) that maps vertex numbers in the mesh to points
      // in mP
      for (int i = 0; i < v.Length; i++) {
         var pt = v[i].Pos;
         if (!mPDict.TryGetValue (pt, out int n)) { 
            mPt[mPtN] = pt; mPDict.Add (pt, mPtN); n = mPtN;
            mPtN++; 
         }
         mPtMap[i] = n;
      }

      // Now, we can build the CTri objects using the points we've added in
      for (int i = 0; i < t.Length; i += 3) {
         int a = t[i], b = t[i + 1], c = t[i + 2];
         Point3f pa = v[a].Pos, pb = v[b].Pos, pc = v[c].Pos;
         // Discard zero-area triangles and add in the rest 
         double area = ((pb - pa) * (pc - pa)).LengthSq; if (area < 1e-8) continue;
         mTri[mTriN++] = new CTri (mPt, mPtMap[a], mPtMap[b], mPtMap[c]);
      }
   }
   // This array (used during AddMesh) maps vertex numbers from the current mesh 
   // to indices within the mP array. This map is needed:
   // - because we may add multiple meshes in before building
   // - because we de-duplicate vertex points (using mPtDict)
   int[] mPtMap = new int[8];                

   /// <summary>Builds an OBBTree hierarchy</summary>
   /// The input (composed by AddMesh calls) is:
   /// - mPt : A list of de-duplicated points
   /// - mTri : A set of CTri structs that index into these mPt 
   /// The top level OBB is built with the complete set of mPt, and stored in mBox[0]. Starting
   /// with that, we keep subdividing the set of triangles into two each time, picking a suitable
   /// partition axis (aligning with one of the axes of the OBB, and starting with the axis with
   /// the highest variance). 
   /// This will create two child OBBs (a 'left' and a 'right' one). We will then permute the
   /// mTri so that all the triangles in the left child come first, followed by all the triangles
   /// in the right child. Each of these smaller OBBs (with a smaller range of triangles) is 
   /// again recursively subdivided. 
   /// Since the CTri struct is not trivially small, we don't actually keep shuffling the CTri
   /// during this build process - we maintain a permutation of CTri called mTriMap (just an array
   /// of integers) and shuffle those integers around. This speeds up the process consderably.
   public OBBTree Build (string? tag) {
      Lib.Grow (ref mTriMap, 0, mTriN);
      Lib.Grow (ref mPtRung, 0, mPtN); Lib.Grow (ref mPtSubset, 0, mPtN);
      for (int i = 0; i < mTriN; i++) mTriMap[i] = i;
      mBox[mBoxN] = OBB.Build (mPt.AsSpan (0, mPtN));
      mTodo.Enqueue ((mBoxN, 0, mTriN)); mBoxN++;
      Span<Vector3f> axes = stackalloc Vector3f[3];

      // The mTodo queue contains the set of OBBs that we need to partition and create
      // children of. We keep processing as long as this queue is not empty
      while (mTodo.Count > 0) {
         // 0. Fetch the next box to be partitioned from the queue
         var (parent, start, count) = mTodo.Dequeue ();
         int end = start + count;
         ref OBB box = ref mBox[parent];
         axes[0] = box.X; axes[1] = box.Y; axes[2] = box.Z;

         // There's a set of triangles [start..end), pointing within the mPermute
         // array. We are going to split them by the median cut method. 
         // 1. Compute the mean
         var mean = Point3f.Zero;
         for (int i = start; i < end; i++) mean += mTri[mTriMap[i]].Centroid;
         mean *= 1f / count;

         // 2. Compute their spread within this OBB
         var variance = Vector3f.Zero;
         for (int i = start; i < end; i++) {
            var d = mTri[mTriMap[i]].Centroid - mean;
            var (x, y, z) = (box.X.Dot (d), box.Y.Dot (d), box.Z.Dot (d));
            variance += new Vector3f (x * x, y * y, z * z);
         }

         // 3. Try the split in decreasing order of axis variance
         bool ok = false; int split = 0;
         int order = GetAxisOrder (variance);
         for (int i = 0; i < 3; i++) {
            split = Partition (start, end, mean, axes[order % 10]);
            if (split > start && split < end) { ok = true; break; }
            order /= 10; 
         }
         if (!ok) split = (start + end) / 2;

         // 4. Make the two child OBBs and add them into the queue for 
         // further subdivision
         if (split > start + 1) {
            box.Left = MakeOBB (start, split);
            mTodo.Enqueue ((box.Left, start, split - start));
         } else
            box.Left = -start;   // Point (via mPermute) directly into a triangle
         if (end > split + 1) {
            box.Right = MakeOBB (split, end);
            mTodo.Enqueue ((box.Right, split, end - split));
         } else
            box.Right = -split;  // Point (via mPermute) directly into a triangle
      }

      // Final cleanup: the left and right pointers of each OBB we have created could
      // be negative - this means they are pointing to a triangle. The code below has
      // set these up to point to triangles indirectly (via the mPermute permutation).
      // Remove that level of indirection here
      for (int i = 1; i < mBoxN; i++) {
         ref OBB box = ref mBox[i];
         if (box.Left < 0) box.Left = -mTriMap[-box.Left];
         if (box.Right < 0) box.Right = -mTriMap[-box.Right];
      }
      return new OBBTree (mPt.AsSpan (0, mPtN), mTri.AsSpan (0, mTriN), mBox.AsSpan (0, mBoxN), tag);
   }
   Queue<(int Box, int Start, int Count)> mTodo = [];

   /// <summary>Return a borrowed builder back to the pool</summary>
   public void Dispose () => BorrowPool<OBBTreeBuilder>.Return (this);

   // Implementation -----------------------------------------------------------
   // Given the 'spread' vector, it retuns an encoded order in which the split
   // should be attempted (we are going to peel off the digits from right-to-end)
   static int GetAxisOrder (Vector3f vec) {
      if (vec.X > vec.Y) {
         if (vec.Z < vec.Y) return 210;         // ZYX
         if (vec.Z > vec.X) return 102;         // YXZ
         return 120;                            // YZX
      } else {
         if (vec.Z < vec.X) return 201;         // ZXY
         if (vec.Z > vec.Y) return 12;          // XYZ
         return 21;                             // XZY
      }
   }

   // Given a range of triangles (indices into the mPermute array), creates an OBB
   // from the points referenced in them. Note that we have to de-duplicate the points
   // and we use the mPtRung array for that
   int MakeOBB (int start, int end) {
      // For each child OBB we are building, we are going to gather a subset of points
      // into mPtSubset (only the points referenced by the points from the triangles 
      // from start..end). Note that we have to de-duplicate these points and we use 
      // a rung mechanism for that (rather than the more expensive HashSet)
      mRung++; int cPts = 0;
      for (int i = start; i < end; i++) {
         ref CTri t = ref mTri[mTriMap[i]];
         int a = t.A, b = t.B, c = t.C;
         if (mPtRung[a] != mRung) { mPtRung[a] = mRung; mPtSubset[cPts++] = mPt[a]; }
         if (mPtRung[b] != mRung) { mPtRung[b] = mRung; mPtSubset[cPts++] = mPt[b]; }
         if (mPtRung[c] != mRung) { mPtRung[c] = mRung; mPtSubset[cPts++] = mPt[c]; }
      }
      mBox[mBoxN] = OBB.Build (mPtSubset.AsSpan (0, cPts));
      return mBoxN++;
   }
   uint[] mPtRung = []; uint mRung;
   Point3f[] mPtSubset = [];

   // Tries to partition the triangle subrange from start..end along a given 
   // direction, about the given mean point. Note that just does a shuffling of
   // the mPermute array (through which the tris are read indirectly)
   int Partition (int start, int end, Point3f mean, Vector3f dir) {
      int split = start;
      var fMean = mean.X * dir.X + mean.Y * dir.Y + mean.Z * dir.Z;
      for (int i = start; i < end; i++) {
         var pt = mTri[mTriMap[i]].Centroid;
         var f = pt.X * dir.X + pt.Y * dir.Y + pt.Z * dir.Z;
         if (f < fMean) {
            (mTriMap[i], mTriMap[split]) = (mTriMap[split], mTriMap[i]);
            split++;
         }
      }
      return split;
   }

   // Reset is called before each build cycle
   void Reset () {
      mPDict.Clear (); mTodo.Clear (); 
      mPtN = mTriN = mBoxN = 0;
   }

   // IBorrowable implementation -----------------------------------------------
   static OBBTreeBuilder IBorrowable<OBBTreeBuilder>.Make () => new ();
   OBBTreeBuilder () { }

   static ref OBBTreeBuilder? IBorrowable<OBBTreeBuilder>.Next (OBBTreeBuilder item) => ref item.mNext;
   OBBTreeBuilder? mNext;

   // Private data -------------------------------------------------------------
   Dictionary<Point3f, int> mPDict = new (Point3fComparer.Delta);
   Point3f[] mPt = []; int mPtN;          // Set of all points, and count of how many of those are used
   CTri[] mTri = []; int mTriN;           // Set of all CTri, and count of how many of those are used
   OBB[] mBox = new OBB [8]; int mBoxN;   // Set of all OBB, and count of how many of those are used
   // mTriMap is a permutation of the mTriN triangles. Initially this is an 'identity' permutation
   // which is (0,1,2,...mTriN-1). As we subdivide this set of triangles into two (partitioning
   // along an axis), we shuffle this so all the triangles belonging to the first sub-OBB appear
   // first, followed by all those belonging to the second. It is faster to shuffle these 
   // 'permutation' indices rather than shuffling actual CTri objects
   int[] mTriMap = [];         
}
#endregion
