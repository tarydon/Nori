// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CurveUnloft.cs
// ║║║║╬║╔╣║ Implements the CurveUnlofter class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class CurveUnlofter ------------------------------------------------------------------------
/// <summary>Used to 'unloft' a point on any parametric curve</summary>
/// Given a parametric curve, this can return the T value corresponding to a given
/// point on the curve. Typically, this is used with NurbsCurves since most other curve
/// types will have simpler analytical methods to unloft. 
/// 
/// The CurveUnlofter works by creating a piecewise linear approximation of the curve
/// with an adaptive number of segments. Initially, we divide the curve into just 4 segments,
/// and we store for each segment just the midpoint. Thus, suppose the domain of the curve is
/// 0..1, the segment boundaries (with 4 segments) would be at 0, 0.25, 0.5, 0.75 and 1 and 
/// the centers of these segments (called Nodes) would be at t = 0.125, 0.375, 0.625 and 0.875.
/// We pick the initial segment by just picking the closest node. 
/// 
/// Then, we ask that segment to Unloft the point, and it may end up recursively subdividing 
/// itself until the smaller and smaller pieces are 'flat enough' for a simple linear 
/// interpolation to work. 
public class CurveUnlofter {
   // Constructors -------------------------------------------------------------
   public CurveUnlofter (Curve3 curve) {
      mDomain = (mCurve = curve).Domain;
      // Create an initial subdivision with 4 segs
      double dt = mDomain.Length / mRootSegs;
      for (int i = 0; i <  mRootSegs; i++) {
         AddNode (dt * (i + 0.5) + mDomain.Min);
         AddSeg (i, dt / 2);
      }
   }
   readonly Curve3 mCurve;    // The curve we're working with
   readonly Bound1 mDomain;   // The domain of that curve
   const int mRootSegs = 4;   // Initial number of segments

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the T value corresponding to the given Point</summary>
   public double GetT (Point3 pt) {
      // First, figure out one of the 4 root segments this point is closest to,
      int iRoot = -1;
      double minDist = double.MaxValue;
      for (int i = 0; i < mRootSegs; i++) {
         ref Node node = ref mNodes[i];
         double dist = pt.DistToSq (node.Pt);
         if (dist < minDist) (minDist, iRoot) = (dist, i);
      }
      // Then, ask that segment to evaluate the point. It returns a t value and also
      // a possible 'overrun' code (meaning the t value might be on the segment to 
      // the left or right)
      var (t, over) = GetT (iRoot, pt);
      if (over == EOverrun.Nil) return t;    // This is the common path

      // If the segment suggest an overrun, evaluate a t value at the adjacent segment
      // (to the left or right), and evaluate a t value on that segment. 
      int iAltRoot = over == EOverrun.Left ? iRoot - 1 : iRoot + 1;
      if (iAltRoot is < 0 or >= mRootSegs) return t;
      var tAlt = GetT (iAltRoot, pt).T;
      // We can use the reverse function (evaluate point at t) at these two potential
      // values to figure out which one to return
      double err = mCurve.GetPoint (t).DistToSq (pt), errAlt = mCurve.GetPoint (tAlt).DistToSq (pt);
      return err < errAlt ? t : tAlt;
   }

   // Implementation -----------------------------------------------------------
   // Adds a node at the given value of t (and returns the index of that node)
   int AddNode (double t) { 
      mNodes[mUsedNodes] = new Node (mCurve, t); 
      return mUsedNodes++; 
   }
   int mUsedNodes;

   // Adds a segment given the index of the center node, and the half-span (in T) of the segment.
   // Returns the index of the newly added segment. 
   int AddSeg (int nCenter, double dtHalf) { 
      mSegs[mUsedSegs] = new Seg (mUsedSegs, nCenter, dtHalf);
      return mUsedSegs++;
   }
   int mUsedSegs;

   // Core routine that asks this segment to unloft the given point.
   // This may cause this node to subdivide itself if it is not flat enough.
   // In addition to returning a potential value of T, this also returns an  overrun code
   // (Left / Right) if the returned interpolated value of T slightly  overruns the span of this
   // Segment (this is a hint that we should try the neighboring segment on that side)
   (double T, EOverrun over) GetT (int nSeg, Point3 pt) {
      for (; ; ) {
         // First, figure out if we may want to subdivide this node. This routine does any
         // subdivision required, and also returns a code that indicates if this node has been
         // subdivided (an interior) or not (a leaf)
         var state = CheckAndSubdivide (nSeg);
         ref Seg seg = ref mSegs[nSeg];
         switch (state) {
            case EState.Divided:
               // If we've been subdivided, recurse into either the left or right child
               // depending on which segment's center the given point is closer to
               int children = seg.Children;
               ref Seg left = ref mSegs[children], right = ref mSegs[children + 1];
               ref Node lNode = ref mNodes[left.Center], rNode = ref mNodes[right.Center];
               double lDist = pt.DistToSq (lNode.Pt), rDist = pt.DistToSq (rNode.Pt);
               nSeg = lDist < rDist ? children : children + 1;
               break;
            default:
               // We've reached a leaf node, so ask it to do an evaluation
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
         if (cen.DistToLineSq (left, right) < Lib.FineTessSq
          && Math.Abs (cen.DistTo (left) - cen.DistTo (right)) < Lib.FineTess) {
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
#endregion
