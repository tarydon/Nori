// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBCollider.cs
// ║║║║╬║╔╣║ Implements OBBCollider - collision checker for OBBTrees
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Alt;

/// <summary>Implements a collision-check between two OBBTree</summary>
public class OBBCollider : IBorrowable<OBBCollider> {
   // Methods ------------------------------------------------------------------
   /// <summary>Borrows an OBBCollider for use</summary>
   public static OBBCollider Borrow () => BorrowPool<OBBCollider>.Borrow ();

   /// <summary>Checks two OBBTree for collisions (returns at the first collision)</summary>
   public bool Check (OBBTree a, OBBTree b, bool oneCrash = true) {
      // We're going to do the check by projecting all the data from tree B into tree A's
      // coordinate system. Thus, we want the smaller tree as B (less transformation). 
      if (a.Tris.Length < b.Tris.Length) return Check (b, a);
      if (b.IsEmpty) return false;

      // Preparing for the collision check
      // - Compute mBtoA (the transform from B's space to A's)
      // - Grow the mBAPts array to be as long as the mBPts array
      mBtoA = (mB = b).Xfm * (mA = a).InvXfm;

      // Each time the top level Check routine is called (a fresh collision check is starting), we do
      // this initialization:
      // - Grow mBAPts to be at least as big as B.Pts.Length
      // - Likewise the three rung arrays
      // - Likewise mBATris and mBAOBBs should be grown so they are at least as big as B.Tris, B.OBBs
      // - mRung is bumped up - this is effectively like a TimeStamp. 
      mRung++;
      if (mRung == 0) {
         // Rare Edge case: when we bump up mRung, if it is 0, that means we have wrapped around and
         // done 4 billion collision checks. At this point, all the rung values are no longer reliable 
         // so: Reset mPtRung, mTriRung, mOBBRung to 0, and set mRung to 1. 
         mRung = 1; mTriRung = mPtRung = mOBBRung = [];
      }
      // Grow the B to A arrays
      Lib.Grow (ref mBAOBBs, 0, b.OBBs.Length); Lib.Grow (ref mOBBRung, 0, b.OBBs.Length);
      Lib.Grow (ref mBATris, 0, b.Tris.Length); Lib.Grow (ref mTriRung, 0, b.Tris.Length);
      Lib.Grow (ref mBAPts, 0, b.Pts.Length); Lib.Grow (ref mPtRung, 0, b.Pts.Length);

      // Do a top level check (fast exit path) whether the outermost bounding boxes collide
      ref readonly OBB boxA = ref mA.OBBs[0], boxB = ref GetBBox (0);
      if (!Collision.Check (in boxA, in boxB)) return false;

      // Otherwise, recurse in 
      mDone = mCrashing = false; mOneCrash = oneCrash;
      mATris.Clear (); mBTris.Clear (); mDepth = 0;
      Push (boxA.Left, boxB.Left); Push (boxA.Left, boxB.Right);
      Push (boxA.Right, boxB.Left); Push (boxA.Right, boxB.Right);
      Process ();
      return mCrashing;
   }

   public void Dispose () => BorrowPool<OBBCollider>.Return (this);

   // Implementation -----------------------------------------------------------
   public ReadOnlySpan<Vec3F> GetChalk () {
      mPts.Clear (); 
      Check (mA, mB, false);
      List<Point3> P = [], Ap = [], Bp = []; List<double> D = [];
      for (int i = 0; i < mATris.Count; i++) {
         P.Clear (); Ap.Clear (); Bp.Clear (); D.Clear (); 
         // P[0,1,2] are vertices of first triangle, P[3,4,5] of the second triangle
         // (both in world space)
         ref CTri ta = ref mA.Tris[mATris[i]], tb = ref mB.Tris[mBTris[i]];
         P.AddM (GetPt (mA, ta.A), GetPt (mA, ta.B), GetPt (mA, ta.C));
         P.AddM (GetPt (mB, tb.A), GetPt (mB, tb.B), GetPt (mB, tb.C));
         // Compute A,B the planes of these two triangles, and the signed distances
         // of P[0,1,2] against B and P[3,4,5] against A
         PlaneDef A = new (P[0], P[1], P[2]), B = new (P[3], P[4], P[5]);
         for (int j = 0; j < 3; j++) D.Add (B.SignedDist (P[j]));
         for (int j = 3; j < 6; j++) D.Add (A.SignedDist (P[j]));
         for (int j = 0; j < 3; j++) {
            int p = j, q = (j + 1) % 3;
            if (D[p] * D[q] <= 0) Ap.Add ((D[p] / (D[p] - D[q])).Along (P[p], P[q]));
            p += 3; q += 3;
            if (D[p] * D[q] <= 0) Bp.Add ((D[p] / (D[p] - D[q])).Along (P[p], P[q]));
         }
         // Keep the two extreme points of Ap, and of Bp
         KeepExtreme (Ap); KeepExtreme (Bp);
         if (Ap.Count < 2 || Bp.Count < 2) continue;

         Point3 s = Ap[0], t = Ap[1];
         double aLie = Bp[0].GetLieOn (s, t), bLie = Bp[1].GetLieOn (s, t);
         if (aLie > bLie) (aLie, bLie) = (bLie, aLie);
         aLie = aLie.Clamp (); bLie = bLie.Clamp ();
         if (!aLie.EQ (bLie)) {
            mPts.Add ((Vec3F)aLie.Along (s, t));
            mPts.Add ((Vec3F)bLie.Along (s, t));
         }
      }
      return mPts.AsSpan ();

      // Helpers ...........................................
      static Point3 GetPt (OBBTree tree, int n) 
         => (Point3)(tree.Pts[n] * tree.Xfm);
      static void KeepExtreme (List<Point3> P) {
         if (P.Count == 3) {
            if (P[0].EQ (P[1])) { P.RemoveAt (0); return; }
            if (P[1].EQ (P[2])) { P.RemoveAt (2); return; }
            int kill = P[2].GetLieOn (P[0], P[1]) switch { < 0 => 0, > 1 => 1, _ => 2 };
            P.RemoveAt (kill);
         }
      }
   }
   List<Vec3F> mPts = [];

   // Returns an OBB from B, transformed into A's space
   ref OBB GetBBox (int n) {
      if (mRung != mOBBRung[n]) {
         mOBBRung[n] = mRung;
         mBAOBBs[n] = mB.OBBs[n] * mBtoA;
      }
      return ref mBAOBBs[n];
   }

   // Returns a Tri from B, transformed into A's space
   ref CTri GetBTri (int n) {
      if (mRung != mTriRung[n]) {
         mTriRung[n] = mRung;
         ref readonly CTri t = ref mB.Tris[n];
         GetBPt (t.A); GetBPt (t.B); GetBPt (t.C);
         mBATris[n] = new (mBAPts, t.A, t.B, t.C);
      }
      return ref mBATris[n];
   }

   // Transforms a point from B to A's space.
   void GetBPt (int n) {
      if (mPtRung[n] == mRung) return;
      mBAPts[n] = mB.Pts[n] * mBtoA;
      mPtRung[n] = mRung;
   }

   // Checks two entities for collision. The entities could be OBBs (if the index is non-negative),
   // or triangles (if the index is negative). This is a recursive routine that checks one entity
   // from OBBTree A with an entity from OBBTree b. 
   void Process () {
      var (aBoxes, aTris, aPts) = (mA.OBBs, mA.Tris, mA.Pts);
      while (Pop (out var a, out var b)) {
         if (a > 0) {
            if (b > 0) {
               // If both a and b are > 0, then they are both OBBs. 
               // - Do an OBBxOBB check after transforming OBB.B into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each combination of (a.Left, a.Right) with (b.Left, b.Right)
               ref readonly OBB boxA = ref aBoxes[a], boxB = ref GetBBox (b);
               if (!Collision.Check (in boxA, in boxB)) continue;
               Push (boxA.Left, boxB.Left); Push (boxA.Left, boxB.Right);
               Push (boxA.Right, boxB.Left); Push (boxA.Right, boxB.Right);
            } else {
               // a is an OBB index, b is a triangle index.
               // - Do an OBBxTri check after transforming the triangle into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each of (a.Left, a.Right) with the triangle b
               ref readonly OBB boxA = ref aBoxes[a];
               ref readonly CTri triB = ref GetBTri (-b);
               if (!Collision.Check (mBAPts, in triB, in boxA)) continue;
               Push (boxA.Left, b); Push (boxA.Right, b);
            }
         } else {
            if (b > 0) {
               // a is a TRI index, b is an OBB
               // - Do an OBBxTri check after transforming the OBB into A's space
               // - If there is no collision, we are done with this pair
               // - Otherwise, check each of (b.Left, b.Right) with the triangle a
               ref readonly CTri triA = ref aTris[-a];
               ref readonly OBB boxB = ref GetBBox (b);
               if (!Collision.Check (aPts, in triA, in boxB)) continue;
               Push (a, boxB.Left); Push (a, boxB.Right);
            } else {
               // Both are tris - do the lowest level check at which collisions are
               // actually detected!
               ref readonly CTri triA = ref aTris[-a], triB = ref GetBTri (-b);
               if (!Collision.TriTri (aPts, in triA, mBAPts, in triB)) continue;
               if (mOneCrash) mDone = true;
               else { mATris.Add (-a); mBTris.Add (-b); }
               mCrashing = true;
            }
         }
      }
   }

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   void Push (int a, int b) {
      if (mStack.Length == mDepth) Array.Resize (ref mStack, 2 * mDepth);
      mStack[mDepth] = a; mStack[mDepth + 1] = b;
      mDepth += 2;
   }

   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   bool Pop (out int a, out int b) {
      if (mDone || mDepth <= 0) { a = b = 0; return false; }
      mDepth -= 2; a = mStack[mDepth]; b = mStack[mDepth + 1];
      return true;
   }

   // IBorrowable implementation -----------------------------------------------
   OBBCollider () { }
   static OBBCollider IBorrowable<OBBCollider>.Make () => new ();
   static ref OBBCollider? IBorrowable<OBBCollider>.Next (OBBCollider item) => ref item.mNext;
   OBBCollider? mNext;

   // Private data -------------------------------------------------------------
   // The tree travesal stack and its depth. It contains pair of 
   // elements from 'A' and 'B' trees.
   OBBTree mA = OBBTree.Empty, mB = OBBTree.Empty;    // OBBTrees we're testing
   Matrix3 mBtoA = Matrix3.Identity;      // Transform from B to A's space
   int[] mStack = new int[32];            // Tree traversal stack (pairs of elements from A, B)
   int mDepth;                            // Depth of that stack
   List<int> mATris = [], mBTris = []; // Take in pairs, mATris[N] and mBTris[N] are triangles from A and B that crash
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
