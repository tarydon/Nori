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
      //var triangles = mesh.Triangle.Skip (999).Take (999).ToArray ();
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

      mRung++;
      mA = ta; mB = tb;
      mDone = mCrashing = false; mOneCrash = oneCrash;
      mBtoA = Matrix3.From (in csB) * Matrix3.To (in csA);
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
         // 
         OBB boxA = mA.OBBs[a], boxB = mB.OBBs[b];
         // OBBxOBB collision check
         if (!(mCrashing = Collision.Check (boxA, boxB))) return;
         Check (boxA.Left, boxB.Left); if (mDone) return;
         if (!mCrashing) Check (boxA.Left, boxB.Right); if (mDone) return;
         if (!mCrashing) Check (boxA.Right, boxB.Left); if (mDone) return;
         if (!mCrashing) Check (boxA.Right, boxB.Right); if (mDone) return;
      } else if (a < 0 && b > 0) {
         // 2. If one is OBB and one is Tri
         //    Do OBBxTri collision check, transforming whichever is on the right to the left side
         //    coordinate system using mAtoB. If there is no collision return. 
         //    Otherwise, assuming a is the OBB, check a.Left x b, a.Right x b (likewise symmetrically
         //    if b is the OBB). 
         //    After each check, if mDone is set, return
         // 
         OBB boxB = mB.OBBs[b];
         // OBBxTri collision check, with the OBB on the left and the Tri on the right
         mCrashing = Collision.Check (mA.Pts, mA.Tris[-a], boxB);
         if (!mCrashing || mDone) return;
         Check (a, boxB.Left); if (mDone) return;
         if (!mCrashing) Check (a, boxB.Right);
      } else if (a > 0 && b < 0) {
         OBB boxA = mA.OBBs[a];
         // OBBxTri collision check, with the OBB on the left and the Tri on the right
         mCrashing = Collision.Check (mB.Pts, mB.Tris[-b], boxA);
         if (!mCrashing || mDone) return;
         Check (boxA.Left, b); if (mDone) return;
         if (!mCrashing) Check (boxA.Right, b);
      } else if (a < 0 && b < 0) {
         // 3. If both are Tri (we will recurse down to this leaf level finally)
         //    Transform B in to A's coordinates and test. If there is a collision:
         //    - Set mCrashing
         //    - If mOneCrash, set mDone (we don't need to continue any further)
         //    - Add a & b to mATris, mBTris
         var triA = mA.Tris[-a]; var triB = mB.Tris[-b];
         // Tri x Tri collision check
         mCrashing = Collision.TriTri (mA.Pts, triA, mB.Pts, triB);
         if (mCrashing && mOneCrash) mDone = true;
      }
   }

   bool mOneCrash;      // If set, we stop after detecting one crash. Otherwise, we detect all colliding pairs of triangles
   Matrix3 mBtoA = Matrix3.Identity;   // Matrix to move from B coordinate system to A coordinate system
   bool mDone, mCrashing;
   OBBTree mA = null!, mB = null!;
   List<int> mATris = [], mBTris = []; // Take in pairs, mATris[N] and mBTris[N] are triangles from A and B that crash
   uint mRung = 0;  // See the OPTIMIZATION section below for details on this

   // We'll use a thread-static singleton of this to avoid re-making the object each time
   public static OBBCollider It => sIt ??= new ();
   [ThreadStatic] static OBBCollider? sIt;
}
