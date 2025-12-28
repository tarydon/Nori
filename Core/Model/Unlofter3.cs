namespace Nori;

public class CurveUnlofter {
   public CurveUnlofter (Curve3 curve) {
      mDomain = (mCurve = curve).Domain;

      // Create an initial subdivision with 4 segs
      double dt = mDomain.Length / mRootTiles;
      for (int i = 0; i <  mRootTiles; i++) {
         AddNode (dt * (i + 0.5));
         AddSeg (i, dt / 2);
      }
   }
   readonly Curve3 mCurve;
   readonly Bound1 mDomain;

   public double GetT (Point3 pt) {
      int iRoot = -1;
      double minDist = double.MaxValue;
      for (int i = 0; i < mRootTiles; i++) {
         ref Node node = ref mNodes[i];
         double dist = pt.DistToSq (node.Pt);
         if (dist < minDist) (minDist, iRoot) = (dist, i);
      }

      var (t, over) = GetT (iRoot, pt);
      if (over == EOverrun.Nil) return t;

      int iAltRoot = over == EOverrun.Left ? iRoot - 1 : iRoot + 1;
      if (iAltRoot < 0 || iAltRoot >= mRootTiles) return t;
      var tAlt = GetT (iAltRoot, pt).T;
      double err = mCurve.GetPoint (t).DistToSq (pt), errAlt = mCurve.GetPoint (tAlt).DistToSq (pt);
      return err < errAlt ? t : tAlt;
   }
   const int mRootTiles = 4;

   int AddNode (double t) {
      mNodes[mUsedNodes] = new Node (mCurve, t);
      return mUsedNodes++;
   }
   int mUsedNodes;

   int AddSeg (int center, double dt) {
      mSegs[mUsedSegs] = new Seg (mUsedSegs, center, dt);
      return mUsedSegs++;
   }
   int mUsedSegs;

   (double T, EOverrun over) GetT (int nSeg, Point3 pt) {
      for (; ; ) {
         var state = CheckAndSubdivide (nSeg);
         ref Seg seg = ref mSegs[nSeg];
         switch (state) {
            case EState.Divided:
               int children = seg.Children;
               ref Seg left = ref mSegs[children], right = ref mSegs[children + 1];
               ref Node lNode = ref mNodes[left.Center], rNode = ref mNodes[right.Center];
               double lDist = pt.DistToSq (lNode.Pt), rDist = pt.DistToSq (rNode.Pt);
               nSeg = lDist < rDist ? children : children + 1;
               break;
            default:
               return seg.GetT (this, pt);
         }
      }
   }

   EState CheckAndSubdivide (int nSeg) {
      ref Seg seg = ref mSegs[nSeg];
      if (seg.State == EState.Raw) {
         GrowArrays ();
         ref Seg segN = ref mSegs[nSeg];
         segN.Subdivide (this);
         return segN.State;
      }
      return seg.State;
   }

   void GrowArrays () {
      if (mUsedNodes + 4 >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      if (mUsedSegs + 2 >= mSegs.Length) Array.Resize (ref mSegs, mSegs.Length * 2);
   }

   enum EState { Raw, Divided, Leaf };
   enum EOverrun { Nil, Left, Right };

   readonly struct Node {
      public Node (Curve3 curve, double t) 
         => Pt = curve.GetPoint (T = t); 
      public readonly double T;
      public readonly Point3 Pt;
   }
   Node[] mNodes = new Node[8];

   struct Seg {
      public Seg (int id, int center, double dt) {
         Center = center; DT = dt;
      }
      public int Center;
      public int Left, Right;
      public int Children;
      public double DT;

      public (double T, EOverrun Overrun) GetT (CurveUnlofter owner, Point3 pt) {
         ref Node left = ref owner.mNodes[Left], right = ref owner.mNodes[Right];
         pt = pt.SnappedToLine (left.Pt, right.Pt);
         double lie = pt.GetLieOn (left.Pt, right.Pt);
         EOverrun over = EOverrun.Nil;
         if (lie < 0) over = EOverrun.Left; else if (lie > 1) over = EOverrun.Right;
         return (lie.Along (left.T, right.T), over);
      }

      public void Subdivide (CurveUnlofter owner) {
         var nodes = owner.mNodes;
         ref Node center = ref nodes[Center];
         double tCen = center.T;
         Left = owner.AddNode (tCen - DT); Right = owner.AddNode (tCen + DT);
         Point3 cen = center.Pt;
         Point3 left = nodes[Left].Pt, right = nodes[Right].Pt;
         if (cen.DistToLineSq (left, right) < Lib.FineTessSq) {
            State = EState.Leaf;
            return;
         }

         // We've got to subdivide the seg
         double tStep = DT / 2;
         int nLeft = owner.AddNode (tCen - tStep);
         Children = owner.AddSeg (nLeft, tStep);
         int nRight = owner.AddNode (tCen + tStep);
         owner.AddSeg (nRight, tStep);
         State = EState.Divided;
      }
      public EState State;
   }
   Seg[] mSegs = new Seg[8];
}
