// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collision.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using CBox = CMesh.Box;

public class Collision {
   public static bool Between (CMesh c1, CMesh c2) {
      if (c1.Boxes.Length <= c2.Boxes.Length) It.Check (c1, c2);
      else It.Check (c2, c1);
      return false;
   }

   // Implementation -----------------------------------------------------------
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

   // Collision primitives -----------------------------------------------------
   // Checks for collision of one box (from CMesh A) against another box (from CMesh B)
   //    ca = Center of the first box
   //    ea = Half-extents of the first box
   //    cb = Center of the second box
   //    eb = Half-extents of the second box
   // Returns true if the boxes collide. 
   // 
   // This routine uses the matrix mX1to0 to rotate and translate the coordinates from
   // box B into the space of A before performing the test. There are 3 types of tests to
   // be performed:
   // - Class I tests are ones where the separating axes are the basis vectors of box A.
   //   There are 3 of these.
   // - Class II tests are ones where the separating axes are the basis vectors of box B
   //   There are 3 of these. 
   // - Class III tests are the ones where the separating axes are the cross products of
   //   the basis vectors. There are 9 of these
   // In all, there are 15 tests to perform
   bool BoxBox (Vec3F ea, Vec3F ca, Vec3F eb, Vec3F cb) {
      // Class I: A's basis vectors
      double Tx = mX1to0.M11 * cb.X + mX1to0.M21 * cb.Y + mX1to0.M31 * cb.Z + mX1to0.DX - ca.X;
      double t = ea.X + eb.X * mAR.M11 + eb.Y * mAR.M21 + eb.Z * mAR.M31;
      if (Math.Abs (Tx) > t) return false;

      double Ty = mX1to0.M12 * cb.X + mX1to0.M22 * cb.Y + mX1to0.M32 * cb.Z + mX1to0.DY - ca.Y;
      t = ea.Y + eb.X * mAR.M12 + eb.Y * mAR.M22 + eb.Z * mAR.M32;
      if (Math.Abs (Ty) > t) return false;

      double Tz = mX1to0.M13 * cb.X + mX1to0.M23 * cb.Y + mX1to0.M33 * cb.Z + mX1to0.DZ - ca.Z;
      t = ea.Z + eb.X * mAR.M13 + eb.Y * mAR.M23 + eb.Z * mAR.M33;
      if (Math.Abs (Tz) > t) return false;

      // Class II: B's basis vectors
      t = Tx * mX1to0.M11 + Ty * mX1to0.M12 + Tz * mX1to0.M13;
      double t2 = ea.X * mAR.M11 + ea.Y * mAR.M12 + ea.Z * mAR.M13 + eb.X;
      if (Math.Abs (t) > t2) return false;

      t = Tx * mX1to0.M21 + Ty * mX1to0.M22 + Tz * mX1to0.M23;
      t2 = ea.X * mAR.M21 + ea.Y * mAR.M22 + ea.Z * mAR.M23 + eb.Y;
      if (Math.Abs (t) > t2) return false;

      t = Tx * mX1to0.M31 + Ty * mX1to0.M32 + Tz * mX1to0.M33;
      t2 = ea.X * mAR.M31 + ea.Y * mAR.M32 + ea.Z * mAR.M33 + eb.Z;
      if (Math.Abs (t) > t2) return false;

      // Class III: 9 cross products
      t = Tz * mX1to0.M12 - Ty * mX1to0.M13; t2 = ea.Y * mAR.M13 + ea.Z * mAR.M12 + eb.Y * mAR.M31 + eb.Z * mAR.M21;
      if (Math.Abs (t) > t2) return false;	// L = A0 x B0
      t = Tz * mX1to0.M22 - Ty * mX1to0.M23; t2 = ea.Y * mAR.M23 + ea.Z * mAR.M22 + eb.X * mAR.M31 + eb.Z * mAR.M11;
      if (Math.Abs (t) > t2) return false;	// L = A0 x B1
      t = Tz * mX1to0.M32 - Ty * mX1to0.M33; t2 = ea.Y * mAR.M33 + ea.Z * mAR.M32 + eb.X * mAR.M21 + eb.Y * mAR.M11;
      if (Math.Abs (t) > t2) return false;	// L = A0 x B2
      t = Tx * mX1to0.M13 - Tz * mX1to0.M11; t2 = ea.X * mAR.M13 + ea.Z * mAR.M11 + eb.Y * mAR.M32 + eb.Z * mAR.M22;
      if (Math.Abs (t) > t2) return false;	// L = A1 x B0
      t = Tx * mX1to0.M23 - Tz * mX1to0.M21; t2 = ea.X * mAR.M23 + ea.Z * mAR.M21 + eb.X * mAR.M32 + eb.Z * mAR.M12;
      if (Math.Abs (t) > t2) return false;	// L = A1 x B1
      t = Tx * mX1to0.M33 - Tz * mX1to0.M31; t2 = ea.X * mAR.M33 + ea.Z * mAR.M31 + eb.X * mAR.M22 + eb.Y * mAR.M12;
      if (Math.Abs (t) > t2) return false;	// L = A1 x B2
      t = Ty * mX1to0.M11 - Tx * mX1to0.M12; t2 = ea.X * mAR.M12 + ea.Y * mAR.M11 + eb.Y * mAR.M33 + eb.Z * mAR.M23;
      if (Math.Abs (t) > t2) return false;	// L = A2 x B0
      t = Ty * mX1to0.M21 - Tx * mX1to0.M22; t2 = ea.X * mAR.M22 + ea.Y * mAR.M21 + eb.X * mAR.M33 + eb.Z * mAR.M13;
      if (Math.Abs (t) > t2) return false;	// L = A2 x B1
      t = Ty * mX1to0.M31 - Tx * mX1to0.M32; t2 = ea.X * mAR.M32 + ea.Y * mAR.M31 + eb.X * mAR.M23 + eb.Y * mAR.M13;
      return !(Math.Abs (t) > t2);
   }

   // Private data -------------------------------------------------------------
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
