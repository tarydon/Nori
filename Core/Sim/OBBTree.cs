// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBTree.cs
// ║║║║╬║╔╣║ Represents a collision BVH using OBBs as the bounding primitive
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class OBBTree {
   // Routine to build an OBBTree from a mesh
   public OBBTree (Mesh3 mesh) {
      // Here are the steps:
      // 1. The Mesh3 could contain multiple Node with the same Pos, but with 
      //    different normal vertices. In the OBBTree, we don't want duplicate vertices
      //    at all, so de-duplicate them using a dictionary (use Point3fComparer.Epsilon)
      //    and store these unique set of points in Pts
      // 2. Build the list of Tris based on the triangles from the Mesh3, eliminating zero
      //    area triangles (this may result in a few orphaned entries in the Pts array, but that
      //    is low probability, so don't worry about it)
      Dictionary<Point3f, int> dict = new (Point3fComparer.Epsilon);
      Pts = new Point3f[mesh.Vertex.Length];
      Tris = new CTri[mesh.Triangle.Length / 3 + 1]; // +1 since we are not using Tris[0]
      
      var triangles = mesh.Triangle; int nTris = 0;
      for (int i = 0; i < triangles.Length; i += 3) {
         var pa = mesh.Vertex[triangles[i]].Pos;
         var pb = mesh.Vertex[triangles[i + 1]].Pos;
         var pc = mesh.Vertex[triangles[i + 2]].Pos;
         // Skip zero area triangles 
         if (((pb - pa) * (pc - pa)).LengthSq.IsZero (1E-10)) continue; 
         Tris[++nTris] = new (Pts, Idx (pa), Idx (pb), Idx (pc));

         int Idx (Point3f pt) {
            if (dict.TryGetValue (pt, out int idx)) return idx;
            idx = dict.Count;
            Pts[idx] = pt;
            dict[pt] = idx;
            return idx;
         }
      }
      Array.Resize (ref Pts, dict.Count);
      Array.Resize (ref Tris, nTris + 1); // +1 since we are not using Tris[0]

      // 3. Now we are ready to starting building the tree of OBBs. Create a queue that is going
      //    to contain spans of triangles. Initially, this will be the complete set of triangles in
      //    the mesh, and seed it thus:
      // This queue maintains the list of OBBs we need to create. Each entry in this queue contains
      // a range of triangles (within the Tris array) that need to be built into an OBB. 
      Queue<(int Start, int Count, int Parent, bool IsLeft)> todo = [];
      todo.Enqueue ((1, Tris.Length - 1, -1, false));
      OBBs = new OBB[Tris.Length];
      OBBs[0] = OBB.Zero; OBBs[1] = OBB.Build (Pts);
      int nOBB = 0, nPts; // We will build the root OBB at index 1, so we start building children from index 2
      HashSet<int> set = []; Point3f[] pts = [.. Pts]; // Temporary stuff to avoid re-allocating in the loop below
      Span<Vector3f> axes = stackalloc Vector3f[4];

      // 4. While the todo list is not empty, dequeue each entry from the list and build an 
      //    OBB for that set of triangles. You can use a HashSet to gather the unique set of indices
      //    for each subset of triangles, build a temporary list of Point3f and then pass it to 
      //    OBB.Build (optimize for the first OBB by just using the entire Pts.AsSpan()). 
      while (todo.TryDequeue (out var res)) {
         var (start, count, nparent, left) = res;
         set.Clear (); nPts = 0; 
         int end = start + count;
         ref OBB box = ref OBBs[++nOBB];
         if (nOBB > 1) {
            for (int i = start; i < end; i++) {
               set.Add (Tris[i].A); set.Add (Tris[i].B); set.Add (Tris[i].C);
            }
            foreach (var idx in set) pts[nPts++] = Pts[idx];
            box = OBB.Build (pts.AsSpan (0, nPts));
            ref var parent = ref OBBs[nparent];
            if (left) parent.Left = nOBB;
            else parent.Right = nOBB;
         }
         if (count <= 2) {
            // Leaf nodes
            box.Left = -start; box.Right = -(end - 1); 
            continue;
         }

         // 5. After this OBB is built, do a split of the range of triangles into two. By our
         //    definition, we will never build an OBB with less than 2 triangles so this is always 
         //    possible. Use a suitable strategy (median split / surface area ...) trying to ensure 
         //    as balanced a tree as possible. Once a split function is available, rearrange the Tris
         //    array to ensure that all the LEFT children appear first. Add the two new child subranges
         //    to the queue, update the parent OBB Left and Right pointers, and add the parent OBB to
         //    the master list OBBs[]. Note that this rearrangement of the Tris array is always safe,
         //    since we are always only rearranging _within_ the set of triangles used by an OBB so 
         //    it continues to remain valid. 
         Point3f mean = Point3f.Zero;
         for (int i = start; i < end; i++) mean += Tris[i].Centroid;
         mean *= 1f / count;

         var variance = Vector3f.Zero;
         for (int i = start; i < end; i++) {
            var d = Tris[i].Centroid - mean;
            var (x, y, z) = (box.X.Dot (d), box.Y.Dot (d), box.Z.Dot (d));
            variance += new Vector3f (x * x, y * y, z * z);
         }
         variance *= 1f / count;

         bool iOK = false;
         axes[1] = box.X; axes[2] = box.Y; axes[3] = box.Z;
         int order = GetAxisOrder (variance), split = start;

         for (int i = 0; i < 3; i++) {
            split = Partition (start, end, mean, axes[order % 10]);
            if (split > start && split < end) { iOK = true; break; }
            order /= 10;
         }
         if (!iOK) split = (start + end) / 2;
         // 6. At the point of splitting, if we find only one triangle on one side:
         //    - we don't build an OBB around that one triangle, but use the Left/Right pointer of the
         //      the parent to point directly into that CTri (using a negative index)
         //    - note that even in this case we need to shuffle the triangles around so LEFT triangle(s)
         //      appear before RIGHT triangle(s). Also note that since this LEFT or RIGHT triangle
         //      will never henceforth be referenced in any OBB, its position in the Tris array is 
         //      locked - this is important since we have now stored a (negative) index to it inside an
         //      OBB!
         // 
         if (split > start + 1) todo.Enqueue ((start, split - start, nOBB, true));
         else box.Left = -start; // Point directly to the leaf triangle
         if (end > split + 1) todo.Enqueue ((split, end - split, nOBB, false));
         else box.Right = -split; // Point directly to the leaf triangle
      }
      Array.Resize (ref OBBs, nOBB + 1);
   }

   int Partition (int start, int end, Point3f at, Vector3f dir) {
      int split = start;
      var d = (dir.X * at.X + dir.Y * at.Y + dir.Z * at.Z);
      for (int i = start; i < end; i++) {
         var c = Tris[i].Centroid;
         if ((dir.X * c.X + dir.Y * c.Y + dir.Z * c.Z) < d) {
            if (split != i) {
               (Tris[i], Tris[split]) = (Tris[split], Tris[i]);
            }
            split++;
         }
      }
      return split;
   }

   int GetAxisOrder (Vector3f vec) {
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

   public IEnumerable<OBB> EnumBoxes (int maxLevel) {
      Queue<(int NBox, int Level)> todo = [];
      todo.Enqueue ((1, 0));
      while (todo.TryDequeue (out var tup)) {
         var b = OBBs[tup.NBox];
         if (tup.Level <= maxLevel) {
            // Output boxes at maxLevel. Also output leaf boxes from 
            // earlier levels (ones that don't have two boxes as children)
            if (tup.Level == maxLevel || (b.Left < 0 || b.Right < 0)) {
               yield return b;
            }
            if (b.Left > 0 && tup.Level < maxLevel) todo.Enqueue ((b.Left, tup.Level + 1));
            if (b.Right > 0 && tup.Level < maxLevel) todo.Enqueue ((b.Right, tup.Level + 1));
         }
      }
   }

   // Raw list of points. These are referenced by triangles
   public readonly Point3f[] Pts = [];
   // Set of triangles in the mesh - these contain indices pointing into
   // the Pts array (along with normal, points etc). Note that Tris[0] is not used, 
   // to avoid ambiguity in the meaning of 0 being used for an OBB Left/Right pointer
   public readonly CTri[] Tris = [];
   // The hierarchy of oriented bounding boxes. OBBs[0] is the root OBB of the entire
   // mesh and will contain all the N triangles in the mesh. The left and right children
   // will contain a (close to equal) partition of these children with A and B triangles
   // such that A+B = N. The binary tree keeps going down until we finally reach individual
   // triangles. At that point, we don't actually build OBBs surronding single triangles,
   // but switch to storing a pointer to the leaf triangle directly in Left/Right. 
   public readonly OBB[] OBBs = [];
}

// This is the class that implements a complete collision between two OBBTree. There is some
// useful state to maintain during the collision check so we make this a class. 
public class OBBCollider {
   public bool Check (OBBTree ta, in CoordSystem csA, OBBTree tb, in CoordSystem csB, bool oneCrash = true) {
      // We're going to do the check by projecting all the data from tree B into tree A's
      // coordinate system. Thus, we want the smaller tree as B (less transformation). 
      if (ta.Tris.Length < tb.Tris.Length) return Check (tb, in csB, ta, in csA, oneCrash);

      mA = ta; mB = tb;
      mDone = mCrashing = false; mOneCrash = oneCrash;
      mBtoA = Matrix3.From (in csB) * Matrix3.To (in csA);
      BObb = n => mB.OBBs[n]; BTri = n => mB.Tris[n];
      // Each time the top level Check routine is called (a fresh collision check is starting), we do
      // this initialization:
      // - Grow mBAPts to be at least as big as B.Pts.Length
      // - Likewise the three rung arrays
      // - Likewise mBATris and mBAOBBs should be grown so they are at least as big as B.Tris, B.OBBs
      // - mRung is bumped up - this is effectively like a TimeStamp. 
      if (!mBtoA.IsIdentity) {
         mRung++; 
         if (mRung == 0) {
            // Rare Edge case: when we bump up mRung, if it is 0, that means we have wrapped around and
            // done 4 billion collision checks. At this point, all the rung values are no longer reliable 
            // so: Reset mPtRung, mTriRung, mOBBRung to 0, and set mRung to 1. 
            mRung = 1; Array.Clear (mTriRung);
            Array.Clear (mPtRung); Array.Clear (mOBBRung);
         }
         BObb = BAObb; BTri = BATri;
         if (mBAOBBs.Length < mB.OBBs.Length) {
            Array.Resize (ref mOBBRung, mB.OBBs.Length);
            Array.Resize (ref mBAOBBs, mB.OBBs.Length);
         }

         if (mBATris.Length < mB.Tris.Length) {
            Array.Resize (ref mTriRung, mB.Tris.Length);
            Array.Resize (ref mBATris, mB.Tris.Length);
         }

         if (mBAPts.Length < mB.Pts.Length) {
            Array.Resize (ref mPtRung, mB.Pts.Length);
            Array.Resize (ref mBAPts, mB.Pts.Length);
         }
      }
      mATris.Clear (); mBTris.Clear ();
      Check (1, 1);
      return mCrashing;
   }

   // Checks two entities for collision. The entities could be OBBs (if the index is non-negative),
   // or triangles (if the index is negative). This is a recursive routine that checks one entity
   // from OBBTree A with an entity from OBBTree b. 
   void Check (int a, int b) {
      if (mDone) return;
      if (a > 0 && b > 0) {
         // 1. If both a & b are OBBs (a > 0, b >= 0), then:
         //    Do OBBxOBB collision check, transforming the center, X and Y of OBB B into 
         //    A's space using mAtoB. If there is no collision return.
         //    Otherwise, Check (a.Left, b.Left) and likewise a.Left x b.Right, a.Right x b.Left, a.Right x b.Right 
         //    After each check, if mDone is set, return. 
         OBB boxA = mA.OBBs[a], boxB = BObb (b);
         if (!Collision.Check (boxA, boxB)) return;
         Check (boxA.Left, boxB.Left); if (mDone) return;
         Check (boxA.Left, boxB.Right); if (mDone) return;
         Check (boxA.Right, boxB.Left); if (mDone) return;
         Check (boxA.Right, boxB.Right);
      } else if (a < 0 && b > 0) {
         // 2. If one is OBB and one is Tri
         //    Do OBBxTri collision check, transforming whichever is on the right to the left side
         //    coordinate system using mAtoB. If there is no collision return. 
         //    Otherwise, assuming a is the OBB, check a.Left x b, a.Right x b (likewise symmetrically
         //    if b is the OBB). 
         //    After each check, if mDone is set, return
         OBB boxB = BObb (b);
         if (!Collision.Check (mA.Pts, mA.Tris[-a], boxB)) return;
         Check (a, boxB.Left); if (mDone) return;
         Check (a, boxB.Right);
      } else if (a > 0 && b < 0) {
         // OBBxTri collision check
         OBB boxA = mA.OBBs[a];
         if (!Collision.Check (mBAPts, BTri (-b), boxA)) return;
         if (mDone) return;
         Check (boxA.Left, b); if (mDone) return;
         Check (boxA.Right, b);
      } else if (a < 0 && b < 0) {
         if (mDone) return;
         // 3. If both are Tri (we will recurse down to this leaf level finally)
         //    Transform B in to A's coordinates and test. If there is a collision:
         //    - Set mCrashing
         //    - Add a & b to mATris, mBTris
         if (Collision.TriTri (mA.Pts, mA.Tris[-a], mBAPts, BTri (-b))) {
            mCrashing = true;
            mATris.Add (-a); mBTris.Add (-b);
            // If mOneCrash, set mDone (we don't need to continue any further)
            if (mOneCrash) mDone = true;
         }
      }
   }

   OBB BAObb (int n) {
      if (mRung == mOBBRung[n]) return mBAOBBs[n];
      mBAOBBs[n] = mB.OBBs[n] * mBtoA;
      mOBBRung[n] = mRung;
      return mBAOBBs[n];
   }

   CTri BATri (int n) {
      if (mRung == mTriRung[n]) return mBATris[n];
      var pts = mB.Pts; CTri t = mB.Tris[n];
      UpdatePt (t.A); UpdatePt (t.B); UpdatePt (t.C);
      mBATris[n] = new (mBAPts, t.A, t.B, t.C);
      mTriRung[n] = mRung;

      [MethodImpl (MethodImplOptions.AggressiveInlining)]
      void UpdatePt (int n) {
         if (mPtRung[n] != mRung) {
            mBAPts[n] = pts[n] * mBtoA;
            mPtRung[n] = mRung;
         }
      }
      return mBATris[n];
   }

   Func<int, OBB> BObb = null!;        // Given index, returns B's OBB
   Func<int, CTri> BTri = null!;       // Given index, returns B's triangle
   bool mOneCrash;                     // If set, we stop after detecting one crash. Otherwise, we detect all colliding pairs of triangles
   Matrix3 mBtoA = Matrix3.Identity;   // Matrix to move from B coordinate system to A coordinate system
   bool mCrashing;                     // Collision result after the check
   bool mDone;                         // Flag used to intercept and stop the recursive check.
   OBBTree mA = null!, mB = null!;     // A and B OBB trees
   List<int> mATris = [], mBTris = []; // Take in pairs, mATris[N] and mBTris[N] are triangles from A and B that crash
   uint mRung = 0;                     // A timestamp for 'this' collision session.

   // We'll use a thread-static singleton of this to avoid re-making the object each time
   public static OBBCollider It => sIt ??= new ();
   [ThreadStatic] static OBBCollider? sIt;

   // OPTIMIZATION: 
   // In the Check routine above we often need to transform OBBs or triangles from B to A's space.
   // To keep this less impactful, we have already sorted things so that A has more triangles than B
   // (and thus presumably fewer OBBs in the tree). 
   // However, each OBB from B and each triangle from B is potentially used multiple times for collision,
   // and will undergo this transformation multiple times. It will be worth spending some effort to 
   // avoid that (effort that MetaCAM / Flux don't do now). 
   // 
   // These are the data structures needed to support this optimization:
   // These 3 arrays contain a 'transformed' version of OBBTree B, transformed into A's space using
   // mBtoA. Note that we need to transform each of these sets of data: 
   // - the points obviously need to be transformed
   // - the CTri contain normal vectors that need to be transformed (I think the D values will not
   //   change, but let's verify that is the case!)
   // - the OBBs need to be transformed (Center, X, Y)
   Point3f[] mBAPts = [];
   CTri[] mBATris = [];
   OBB[] mBAOBBs = [];

   // However, naively transforming all the points, tris and OBBs will be very expensive. So we are 
   // going to do this incrementally. Only when an OBB first appears on the right side of a Check()
   // call we will transform it and store the copy in the corresponding slot in mBAOBBs. To keep
   // track of whether a particular OBB / Point3f / CTri has already been transformed and stored in 
   // one of the 3 arrays above, we will use these sentinels.
   // Now, if mPtRung[N] != mRung, that means that that particular point has not yet been transformed
   // from B to A. We transform it, store it in mPtRung and set mPtRung[N] = mRung. 
   // 
   // 
   // With this optimization, each OBB, Tri and finally each Pt will be transformed only once from
   // B to A's space. And only incrementally - large sections of the tris, OBBs, points wil never
   // need to be transformed, only the portions of the tree that Check visits. Hopefully this optimization
   // will make this pretty fast. 
   //
   // This means that we will never be operating off B.Pts array, but always only off the mBAPts[]
   // array during the TriTri collision checks. 
   // 
   uint[] mPtRung = [], mTriRung = [], mOBBRung = [];
   const uint MaxRung = 4_000_000_000;
}
