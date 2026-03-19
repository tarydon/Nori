// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBCollider.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Alt;

public class OBBCollider : IBorrowable<OBBCollider> {
   /// <summary>
   /// Borrows an OBBCollider from the borrow-pool
   /// </summary>
   public static OBBCollider Borrow () => BorrowPool<OBBCollider>.Borrow ();

   /// <summary>
   /// Checks two OBBTree for collisions (returns at the first collision)
   /// </summary>
   public bool Check (OBBTree a, OBBTree b) {
      // First, try to rearrange things so that b has a smaller number of triangles.
      // We do this because we are going to transform OBBs, Triangles, Pts from B to
      // A's space, and the fewer we have to transform, the better
      if (a.Tris.Length < b.Tris.Length) return Check (b, a);
      if (b.IsEmpty) return false;

      // Preparing for the collision check
      // - Compute mBtoA (the transform from B's space to A's)
      // - Grow the mBAPts array to be as long as the mBPts array
      mBtoA = (mB = b).Xfm * (mA = a).InvXfm;

      Lib.Grow (ref mBAPts, 0, b.Pts.Length);
      if (++mRung == 0) {
         // Rare edge case - we've bumped up Rung 4 billion times, and wrapped around, so
         // all existing rung numbers will be bad
         mRung = 1; mTriRung = mPtRung = mOBBRung = [];
      }
      // Grow the B-to-A arrays 
      Lib.Grow (ref mBAPts, 0, b.Pts.Length); Lib.Grow (ref mPtRung, 0, b.Pts.Length);
      Lib.Grow (ref mBATris, 0, b.Tris.Length); Lib.Grow (ref mTriRung, 0, b.Tris.Length);
      Lib.Grow (ref mBAOBBs, 0, b.OBBs.Length); Lib.Grow (ref mOBBRung, 0, b.OBBs.Length);

      mStack.Push ((1, 1));
      mATri.Clear (); mBTri.Clear ();
      mOneCrash = true; mCrashing = mDone = false;
      Process (); 
      return mCrashing;
   }

   public void Dispose () => BorrowPool<OBBCollider>.Return (this);

   // Implementation -----------------------------------------------------------
   // Returns a particular OBB from B, transformed into A's space
   ref OBB GetBBox (int n) {
      if (mRung != mOBBRung[n]) {
         mBAOBBs[n] = mB.OBBs[n] * mBtoA;
         mOBBRung[n] = mRung;
      }
      return ref mBAOBBs[n];
   }

   // Returns a particular triangle from B, transformed into A's space
   ref CTri GetBTri (int n) {
      if (mRung != mTriRung[n]) {
         ref readonly CTri t = ref mB.Tris[n];
         GetBPt (t.A); GetBPt (t.B); GetBPt (t.C);
         mBATris[n] = new (mBAPts, t.A, t.B, t.C);
         mTriRung[n] = mRung;
      }
      return ref mBATris[n];
   }

   // Copy a point from B, transformed into A's space
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   void GetBPt (int n) {
      if (mRung != mPtRung[n]) {
         mBAPts[n] = mB.Pts[n] * mBtoA;
         mPtRung[n] = mRung;
      }
   }

   void Process () {
      Pts.Clear (); 
      var (aBoxes, aTris, aPts) = (mA.OBBs, mA.Tris, mA.Pts);
      while (mStack.TryPop (out var tup) && !mDone) {
         int a = tup.A, b = tup.B;
         if (a > 0) {
            if (b > 0) {
               // If both a and b are > 0, then they are both OBBs. 
               // - Do an OBBxOBB check after transforming OBB.B into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each combination of (a.Left, a.Right) with (b.Left, b.Right)
               ref readonly OBB boxA = ref aBoxes[a], boxB = ref GetBBox (b);
               if (!Collision.Check (in boxA, in boxB)) continue;    // No collision, skip this subtree
               mStack.Push ((boxA.Left, boxB.Left)); mStack.Push ((boxA.Left, boxB.Right));
               mStack.Push ((boxA.Right, boxB.Left)); mStack.Push ((boxA.Right, boxB.Right));
            } else {
               // a is an OBB index, b is a triangle index.
               // - Do an OBBxTri check after transforming the triangle into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each of (a.Left, a.Right) with the triangle b
               ref readonly OBB boxA = ref aBoxes[a];
               ref readonly CTri triB = ref GetBTri (-b);
               if (!Collision.Check (mBAPts, in triB, in boxA)) continue;   // No collision, skip this subtree
               mStack.Push ((boxA.Left, b)); mStack.Push ((boxA.Right, b));
            }
         } else {
            if (b > 0) {
               // a is a TRI index, b is an OBB
               // - Do an OBBxTri check after transforming the OBB into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each of (b.Left, b.Right) with the triangle a
               ref readonly CTri triA = ref aTris[-a];
               ref readonly OBB boxB = ref GetBBox (b);
               if (!Collision.Check (aPts, in triA, in boxB)) continue;  // No collision, skip this subtree
               mStack.Push ((a, boxB.Left)); mStack.Push ((a, boxB.Right));
            } else {
               // Both are tris - do the lowest level check at which collisions are
               // actually detected!
               ref readonly CTri triA = ref aTris[-a], triB = ref GetBTri (-b);
               if (!Collision.Check (aPts, in triA, mBAPts, in triB)) continue;
               AddTri (mA, -a); AddTri (mB, -b);
               mATri.Add (-a); mBTri.Add (-b);
               if (mOneCrash) mDone = true; 
               mCrashing = true;
            }
         }
      }
   }

   void AddTri (OBBTree tree, int ntri) {
      var xfm = tree.Xfm;
      CTri tri = tree.Tris[ntri];
      Point3f pa = tree.Pts[tri.A] * xfm, pb = tree.Pts[tri.B] * xfm, pc = tree.Pts[tri.C] * xfm;
      Pts.AddM (pa, pb, pb, pc, pc, pa);
   }

   static public List<Point3f> Pts = [];

   // IBorrowable implementation -----------------------------------------------
   static OBBCollider IBorrowable<OBBCollider>.Make () => new ();
   static ref OBBCollider? IBorrowable<OBBCollider>.Next (OBBCollider item) => ref item.mNext;
   OBBCollider? mNext;

   // Private data -------------------------------------------------------------
   // Core data - the two OBBTree, the transform from B..A, and the original copy
   // of B's points   
   OBBTree mA = OBBTree.Empty, mB = OBBTree.Empty; // The OBBTree we are testing
   Matrix3 mBtoA = Matrix3.Identity;   // Transform from B to A's space
   Stack<(int A, int B)> mStack = [];  // Stack of checks to do 
   List<int> mATri = [], mBTri = [];   // Taken in pairs, mATri[N] and mBTri[N] are the pairs of triangles crashing
   bool mCrashing, mOneCrash, mDone;

   // Optimization notes.
   // In the Check routine above, we often need to transform OBBs, Triangles, Points from B's
   // space to A's space (we do all collision checks in A's space). One optimization we have
   // already done is to ensure B has fewer triangles than A (minimizing the number of transforms
   // we need to do). 
   // However, each OBB, Triangle, from B is potentially used multiple times for collision, and
   // we want to try and avoid doing this transform multiple times for the same OBB/Tri/Pt. 
   // For this, the following 3 array contain transformed copies of the Pts, Tris and OBBs. 
   // Naively transforming all of the OBBs/Tris/Pts from B to A will be wasteful. Very often,
   // only a small part of the tree will be visited during the collision check, and only that
   // subsection needs to be transformed. That's handled using the rung counters (see below)
   Point3f[] mBAPts = []; CTri[] mBATris = []; OBB[] mBAOBBs = [];

   // When an OBB / Tri / Pt appears first on the right side of a Check() call, we will transform
   // it and store the copy in the corresponding slot in mBAOBB / mBATri / mBAPt. To keep track of
   // whether a particular object has already been transformed, we use the rung arrays. On each 
   // Check call, the mRung is bumped up and if mTriRung[n] != mRung, then it means that B.Tri[n]
   // has not yet been transformed. 
   // Each time we do a transform, we store the transformed copy, and also set the corresponding
   // rung to mRung so it will never get transformed another time during this Check cycle. 
   uint[] mPtRung = [], mTriRung = [], mOBBRung = [];
   uint mRung;
}
