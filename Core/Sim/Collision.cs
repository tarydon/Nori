// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collision.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Math;
using CBox = CMesh.Box;

public class Collision {
   public static bool Between (CMesh c1, CMesh c2) {
      if (c1.Boxes.Length <= c2.Boxes.Length) It.Check (c1, c2);
      else It.Check (c2, c1);
      return false;
   }

   bool Check (CMesh a, CMesh b) {
      if (a.Boxes.Length <= 1) return false;   //
      mABoxes = (mA = a).Boxes; mX0to1 = a.Xfm * b.InvXfm; mALeaf = -1;
      mBBoxes = (mB = b).Boxes; mX1to0 = b.Xfm * a.InvXfm; mBLeaf = -1;
      mPairs.Clear (); mCrash = false;
      mAR = mX1to0.ExtractAbs ();
      CrashBox (1, 1);
      return mCrash;
   }

   // This is the basic routine used to collide a box from collider A with one from collider B.
   //    aBox = The index of the box from collider A
   //    bBox = The index of the box from collider B
   // If a collision is detected, this sets the mCrash variable to true and returns immediately.
   unsafe void CrashBox (int aBox, int bBox) {
      fixed (CBox* paBase = mABoxes, pbBase = mBBoxes) {
         // First, we check if the two OBBs themselves intersect. If not, we are done and there
         // is no need to go any further.
         CBox* pa = paBase + aBox, pb = pbBase + bBox;
         if (!BoxBox (pa->Extent, pa->Center, pb->Extent, pb->Center)) return;

         // Otherwise, we have to check the two children of box A with the two children of
         // box B. Note that each child may itself be either a smaller box, or a triangle. 
         // So we have to handle several cases:
         // - Box vs Box
         // - Box vs Triangle / Triangle vs Box
         // - Triangle vs Triangle
         bool bLeftTriangle = pb->Left >= 0;     // Is Box B.Left a triangle?
         bool bRightTriangle = pb->Right >= 0;

         // Left child of A with both the children of B
         if (pa->Left >= 0) {
            // Left child of A is a triangle. Get that into mLeaf
            FetchLeaf ('A', pa->Left);

            if (bLeftTriangle) CollideTriTriAB (pb->Left);
            else CollideTriBox (pbBase, pbBase - pb->Left);                 // -ve becasue pb->Left is a negative box index
            if (mCrash && mOnlyOne) return;

            if (bRightTriangle) CollideTriTriAB (pb->Right);
            else CollideTriBox (pbBase, pbBase - pb->Right);

         } else {
            // A has a left box
            if (bLeftTriangle) {
               FetchLeaf ('B', pb->Left);
               CollideBoxTri (paBase, paBase - pa->Left);
            } else
               Collide (-pa->Left, -pb->Left);
            if (mCrash && mOnlyOne) return;

            if (bRightTriangle) {
               FetchLeaf ('B', pb->Right);
               CollideBoxTri (paBase, paBase - pa->Left);
            } else
               Collide (-pa->Left, -pb->Right);
         }
         if (mCrash && mOnlyOne) return;

         // Handling the right child of A with both the children of A
         if (pa->Right >= 0) {
            // A has a right leaf. Fetch the leaf coordinates into mLeaf
            FetchLeaf ('A', pa->Right);

            if (bLeftTriangle) CollideTriTriAB (pb->Left);
            else CollideTriBox (pbBase, pbBase - pb->Left);                 // -ve becasue pb->Left is a negative box index
            if (mCrash && miOnlyOne) return;

            if (bRightTriangle) CollideTriTriAB (pb->Right);
            else CollideTriBox (pbBase, pbBase - pb->Right);

         } else {
            // A has a right box
            if (bLeftTriangle) {
               FetchLeaf ('B', pb->Left);
               CollideBoxTri (paBase, paBase - pa->Right);
            } else
               Collide (-pa->Right, -pb->Left);
            if (mCrash && mOnlyOne) return;

            if (bRightTriangle) {
               FetchLeaf ('B', pb->Right);
               CollideBoxTri (paBase, paBase - pa->Right);
            } else
               Collide (-pa->Right, -pb->Right);
         }
      }
   }

   // Helper function used to fetch a leaf triangle's vertices into mLeaf0, mLeaf1 and mLeaf2,
   // and transform them into the space of the other CMesh's coordinate system
   //    which = 'A' or 'B' to fetch the leaf from collider A, or collider B
   //    nLeaf = The index of the leaf triangle to fetch
   // This uses the mALeaf and mBLeaf parameters to cache repeated re-fetches of the same leaf
   // into mLeaf0, mLeaf1, mLeaf2. Otherwise, after fetching the leaf it gets transformed into
   // the coordinate system of the other CMesh so the vertices can be used directly there. 
   void FetchLeaf (char which, int nLeaf) {
      Point3 p0, p1, p2;
      if (which == 'A') {
         if (mALeaf != nLeaf) {
            mA.GetRawTriangle (mALeaf = nLeaf, out p0, out p1, out p2); mBLeaf = -1;
            mLeaf0 = p0 * mX0to1; mLeaf1 = p1 * mX0to1; mLeaf2 = p2 * mX0to1;
         }
      } else {
         if (mBLeaf != nLeaf) {
            mB.GetRawTriangle (mBLeaf = nLeaf, out p0, out p1, out p2); mALeaf = -1;
            mLeaf0 = p0 * mX1to0; mLeaf1 = p1 * mX1to0; mLeaf2 = p2 * mX1to0;
         }
      }
   }

   static Collision It => mIt ??= new ();
   [ThreadStatic] static Collision? mIt;

   Matrix3 mX0to1 = Matrix3.Identity, mX1to0 = Matrix3.Identity;
   Matrix3 mAR = Matrix3.Identity;
   List<int> mPairs = [];
   int mALeaf, mBLeaf;
   Point3 mLeaf0, mLeaf1, mLeaf2;
   CMesh mA = null!, mB = null!;
   CBox[] mABoxes = [], mBBoxes = [];
   bool mCrash, mOnlyOne;
}
