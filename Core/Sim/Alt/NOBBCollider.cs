// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBBCollider.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.Intrinsics.X86;

namespace Nori.Alt;

public class OBBCollider {
   public bool Check (OBBTree a, OBBTree b, bool oneCrash = true) {
      // First, try to rearrange things so that b has a smaller number of triangles.
      // We do this because we are going to transform OBBs, Triangles, Pts from B to
      // A's space, and the fewer we have to transform, the better
      if (a.Tris.Length < b.Tris.Length) return Check (b, a);
      if (b.IsEmpty) return false;

      // Preparing for the collision check
      // - Compute mBtoA (the transform from B's space to A's)
      // - Grow the mBAPts array to be as long as the mBPts array
      mBtoA = (mB = b).InvXfm * (mA = a).Xfm;
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

      mBoxes.Push ((0, 0));
      mATri.Clear (); mBTri.Clear ();
      mOneCrash = oneCrash; mCrashing = mDone = false;
      Process (); 
      return mCrashing;
   }

   // Implementation -----------------------------------------------------------
   void Process () {
      
   }

   // Private data -------------------------------------------------------------
   // Core data - the two OBBTree, the transform from B..A, and the original copy
   // of B's points   
   OBBTree mA = OBBTree.Empty, mB = OBBTree.Empty; // The OBBTree we are testing
   Matrix3 mBtoA = Matrix3.Identity;   // Transform from B to A's space
   Stack<(int A, int B)> mBoxes = [];  // Stack of box-box checks to do
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
