using System.Collections.Generic;
using System.Security.RightsManagement;
using System.Text;
namespace Nori;

partial class Triangulator {
   public Triangulator (List<Poly> polys) {
      int max = polys.Sum (a => a.Count);
      mPts = new Point2[max + 4]; mDone = new bool[max];
      mSegs = new Segment[max + 2]; mShuffle = new int[max];

      int n = 0;
      foreach (var poly in polys) {
         var pts = poly.Pts; int c = pts.Length;
         for (int i = 0; i < pts.Length; i++) {
            mPts[i + n] = pts[i]; mDone[i + n] = false;
         }
         for (int i = 0; i < pts.Length; i++) {
            mSegs[i + n] = new Segment (mPts, i + n, (i + 1) % c + n);
            mShuffle[i + n] = i + n;
         }
         n += c;
      }

      Random r = new (4);
      for (int i = max - 1; i >= 0; i--) {
         int j = r.Next (max);
         (mShuffle[i], mShuffle[j]) = (mShuffle[j], mShuffle[i]);
      }

      Bound2 b = (new Bound2 (polys.Select (a => a.GetBound ()))).InflatedL (1);
      mPts[max] = new (b.X.Min, b.Y.Min); mPts[max + 1] = new (b.X.Min, b.Y.Max);
      mPts[max + 2] = new (b.X.Max, b.Y.Min); mPts[max + 3] = new (b.X.Max, b.Y.Max);
      mSegs[max] = new (mPts, max, max + 1);
      mSegs[max + 1] = new (mPts, max + 2, max + 3);
      mRoot = new (++mNodeID, EKind.Leaf, 0);
      var box = new Trapezoid (mTraps.Count, b.Y.Min, b.Y.Max, max, max + 1, mRoot); 
      mTraps.Add (box);
   }
   int mNodeID;

   public void DumpNodes (string file) {
      var sb = new StringBuilder ();
      mRoot.Dump (this, sb, 0);
      System.IO.File.WriteAllText (file, sb.ToString ());
   }

   public IEnumerable<string> Process () {
      for (int i = 0; i < mShuffle.Length; i++) {
         int nSeg = mShuffle[i]; 
         var seg = mSegs[nSeg];
         Point2 pa = mPts[seg.A], pb = mPts[seg.B];

         InsertPoint (seg.A);
         yield return $"Added start point {pa} of seg {seg}";
         InsertPoint (seg.B);
         yield return $"Added end point {pb} of seg {seg}";

         var t0 = FindSlice (0.001.Along (pa, pb), true);
         var t1 = FindSlice (0.999.Along (pa, pb), false);
         var traps = GatherChain (t0, t1, nSeg);
         foreach (var s in Slice (traps, nSeg)) yield return s; 
      }
   }

   List<Trapezoid> GatherChain (Trapezoid t0, Trapezoid t1, int nSeg) {
      List<Trapezoid> chain = [];
      chain.Add (t0); if (t1 == t0) return chain;
      if (t0.BotA == t1 || t0.BotB == t1) { chain.Add (t1); return chain; }     
      Segment seg = mSegs[nSeg];
      while (t0 != t1) {
         if (t0.BotB == null) t0 = t0.BotA!;
         else {
            // This node has both BotA and BotB set, figure out which one contains
            // the segment in question 
            double y = (Math.Max (t0.BotA!.YMin, t0.BotB.YMin) + t0.YMin) / 2;
            double x = seg.GetX (mPts, y), xL = mSegs[t0.BotA.Right].GetX (mPts, y);
            if (x < xL) t0 = t0.BotA;
            else {
               Check (x > mSegs[t0.BotB.Left].GetX (mPts, y));
               t0 = t0.BotB; 
            }
         }
         chain.Add (t0); 
      }
      return chain; 
   }

   IEnumerable<string> Slice (List<Trapezoid> traps, int nSeg) {
      // First, do all the work of dividing each of the trapezoids into two, 
      // wiring up the LeafNodes etc
      Segment seg = mSegs[nSeg];
      List<Trapezoid> rights = [];
      foreach (var t0 in traps) {
         if (Verify) {
            Segment sL = mSegs[t0.Left], sR = mSegs[t0.Right];
            double yMid = (t0.YMin + t0.YMax) / 2, xL = sL.GetX (mPts, yMid), xR = sR.GetX (mPts, yMid), x = seg.GetX (mPts, yMid);
            Check (x > xL && x < xR); Check (t0.Node.Kind == EKind.Leaf);
         }

         // We're going to split this into two trapezoids t0 and t1 by the segment.
         // First create two LeafNodes to hold onto these new trapezoids, noting that we're going
         // to be reusing t0 with a new right segment (it's one of the two new trapezoids we're 
         // going to create)
         Node leaf = t0.Node;
         Node n1 = new (++mNodeID, EKind.Leaf, mTraps.Count);
         Trapezoid t1 = new (mTraps.Count, t0.YMin, t0.YMax, nSeg, t0.Right, n1); mTraps.Add (t1);
         Node n0 = new (++mNodeID, EKind.Leaf, leaf.Index);
         t0.Right = nSeg; t0.Node = n0;
         yield return $"  Sliced {t0.Id} -> {t1.Id}";
         rights.Add (t1);

         // The erstwhile leaf node pointing to this will now become an interior node of type X
         // (meaning it is split by a Segment), and the two new nodes we just created will be its
         /// children
         leaf.First = n0; leaf.Second = n1; leaf.Kind = EKind.X; leaf.Index = nSeg;
      }
   }

   //// This 'slices' the trapezoid t into a left and right using the given segment
   //void Slice (Trapezoid t0, int nSeg) {
   //   Segment seg = mSegs[nSeg];
   //   if (Verify) {
   //      Segment sL = mSegs[t0.Left], sR = mSegs[t0.Right];
   //      double yMid = (t0.YMin + t0.YMax) / 2, xL = sL.GetX (mPts, yMid), xR = sR.GetX (mPts, yMid), x = seg.GetX (mPts, yMid);
   //      Check (x > xL && x < xR); Check (t0.Node.Kind == EKind.Leaf);
   //   }

   //   // We're going to split the trapezoid t0 into two trapezoids t0 and t1 by the 
   //   // segment nSeg. First, create two LeafNodes to hold onto these new trapezoids
   //   // (noting that t0 is being recycled with a new right segment)
   //   Node leaf = t0.Node;
   //   double yMin = t0.YMin, yMax = t0.YMax;
   //   Node n1 = new (++mNodeID, EKind.Leaf, mTraps.Count);
   //   Trapezoid t1 = new (mTraps.Count, yMin, yMax, nSeg, t0.Right, n1); mTraps.Add (t1);
   //   Node n0 = new (++mNodeID, EKind.Leaf, leaf.Index);
   //   t0.Right = nSeg; t0.Node = n0;

   //   // The erstwhile 'leaf' node pointing to this trapezoid will now become an 
   //   // interior node of type X (meaning it is split by a Segment) and the two new nodes we
   //   // created will become children of it
   //   leaf.First = n0; leaf.Second = n1; leaf.Kind = EKind.X; leaf.Index = nSeg;

   //   // We've created two trapezoids t0 and t1 - fix up the connections on the 'upward'
   //   // side
   //   Check (t0.TopA != null && t0.BotA != null);
   //   Trapezoid above0 = t0.TopA!;
   //   if (t0.TopB == null) {
   //      // t0 had only one top neighbor. Now that becomes the top neighbor for both 
   //      // t0 and t1, and t1 gets added to that as a new bottom neighbor
   //      if (above0.AddBottom1 (t1)) t1.AddTop (above0);
   //      else {
   //         // If the above0 is already full, either t0 or t1 has no top (ends at a point)
   //         if (t1.NoTop (this, seg.A)) { }
   //         else if (t0.NoTop (this, seg.A)) {
   //            t0.RemoveTop (above0); t1.AddTop (above0); above0.UpdateBottom (t0, t1);
   //         }
   //         else Unexpected ();
   //      }
   //   } else {
   //      // There are two neighbors on the top. After adding this new slice line (marked
   //      // with the *) three scenarios are possible:
   //      //          |                 |                         |
   //      // A. --------------    B. -------------    C. -------------
   //      //          |                    |                   |
   //      //          *                    *                   * 
   //      Trapezoid above1 = t0.TopB!;
   //      Check (above0.Right == above1.Left);
   //      double xAbove = mSegs[above0.Right].GetX (mPts, yMax), xBelow = seg.GetX (mPts, yMax);
   //      if (xAbove.EQ (xBelow)) {        // Case A
   //         // above0 is connected to t0, above1 to t1
   //         t0.RemoveTop (above1); above1.UpdateBottom (t0, t1); t1.AddTop (above1);
   //      } else if (xAbove < xBelow) {   // Case B
   //         // above0 is connected only to t0, while above1 is connected
   //         // to both t0 and t1
   //         above1.AddBottom (t1); t1.AddTop (above1); 
   //      } else {                         // Case C
   //         // Above0 is now connected to both t0 and t1, while t1
   //         // is connected to above1 only
   //         t0.RemoveTop (above1); above0.UpdateBottom (t0, t1); t1.AddTop (above0);
   //         above1.AddBottom (t1); t1.AddTop (above1);
   //      }
   //   }

   //   // Next, fix up the connections on the 'downward' side
   //   Trapezoid below0 = t0.BotA!;
   //   if (t0.BotB == null) {
   //      // t0 had only one bottom neighbor. Now that bcomes the bottom neighbor for
   //      // both t0 and t1, and t1 gets added to that asa a new top neighbor
   //      if (below0.AddTop1 (t1)) t1.AddBottom (below0);
   //      else {
   //         // If below0 is already full, either t0 or t1 has no bottom (ends at a point)
   //         if (t1.NoBottom (this, seg.B)) { } 
   //         else if (t0.NoBottom (this, seg.B)) { throw new NotImplementedException (); } 
   //         else Unexpected ();
   //      }
   //   } else {
   //      // There are two neighbors on the bottom. After adding this new slice line (marked
   //      // with a *), three scenarios are possible
   //      //         *                    *                   *
   //      //         |                    |                   |
   //      // A. -------------    B. -------------    C. -------------
   //      //         |                        |           |
   //      Trapezoid below1 = t0.BotB!;
   //      Check (below0.Right == below1.Left);
   //      double xAbove = seg.GetX (mPts, yMin), xBelow = mSegs[below0.Right].GetX (mPts, yMin);
   //      if (xAbove.EQ (xBelow)) {           // Case A
   //         // below0 connects to t0, below1 connects to t1
   //         t0.RemoveBottom (below1); below1.UpdateTop (t0, t1); t1.AddBottom (below1);
   //      } else if (xAbove < xBelow) {       // Case B
   //         // below0 connects to t0 and t1, below1 connects to t1 only
   //         t0.RemoveBottom (below1); below1.UpdateTop (t0, t1); t1.AddBottom (below1); 
   //         t1.AddBottom (below0); below0.AddTop (t1);
   //      } else {                            // Case C
   //         // t0 remains connected to both below0, below1, while t1 is connected
   //         // only to below1
   //         below1.AddTop (t1); t1.AddBottom (below1);
   //      }
   //   }
   //}

   bool Verify = true;

   void Check (bool condition) {
      if (!condition) throw new Exception ("Coding error in Triangulate.cs");
   }

   void Unexpected () => Check (false);

   public List<(int, Poly)> GetTrapezoids () {
      List<(int, Poly)> output = [];
      foreach (var t in mTraps) {
         Segment a = mSegs[t.Left], b = mSegs[t.Right];
         double e = 0, y0 = t.YMin, y1 = t.YMax;
         Point2 bl = new (a.GetX (mPts, y0 + e) + e, y0 + e), br = new (b.GetX (mPts, y0 + e) - e, y0 + e);
         Point2 tl = new (a.GetX (mPts, y1 - e) + e, y1 - e), tr = new (b.GetX (mPts, y1 - e) - e, y1 - e);
         output.Add ((t.Id, Poly.Lines ([bl, br, tr, tl], true)));
      }
      return output;
   }

   Trapezoid FindSlice (Point2 p, bool below) {
      Node node = mRoot;
      for (; ; ) {
         // Expecting to find a Y node that has this Y as the split point
         if (node.Kind == EKind.Leaf) return mTraps[node.Index];
         if (node.Kind == EKind.Y && p.Y == mPts[node.Index].Y) node = below ? node.First! : node.Second!;
         else node = node.Find (this, p);
      }
   }

   Trapezoid? InsertPoint (int n) {
      if (mDone[n]) return null;
      mDone[n] = true;

      // Fetch the leaf pointing to the trapezoid that
      // currently contains this point
      Point2 pt = mPts[n]; 
      Node leaf = Find (pt);
      Trapezoid t0 = mTraps[leaf.Index];
      Lib.Check (pt.Y >= t0.YMin && pt.Y <= t0.YMax, "Fail1");

      // We're going to split t0 into two trapezoids (t0 and t1 along the Y).
      // Create two LeafNodes to hold onto these new trapezoids (note that t0 is
      // just being recycled with a new top margin)
      Node n1 = new (++mNodeID, EKind.Leaf, mTraps.Count);
      Trapezoid t1 = new (mTraps.Count, pt.Y, t0.YMax, t0.Left, t0.Right, n1); mTraps.Add (t1);
      Node n0 = new (++mNodeID, EKind.Leaf, leaf.Index);
      t0.YMax = pt.Y; t0.Node = n0;

      // Connect t1 to the previous 'above' neighbors of t0 (bidirectionally), 
      (t1.TopA = t0.TopA)?.UpdateBottom (t0, t1);
      (t1.TopB = t0.TopB)?.UpdateBottom (t0, t1);
      // Connect t0 and t1 to each other. Note that t0 bottom connections are 
      // already up to date, and don't need to be touched
      t0.TopA = t1; t0.TopB = null; t1.BotA = t0;

      // Connect the two nodes to the leaf, and connect the two newly created
      // trapezoids to each other
      leaf.First = n0; leaf.Second = n1; 
      leaf.Kind = EKind.Y; leaf.Index = n;
      return t1; 
   }

   Node Find (Point2 pt) {
      Node node = mRoot;
      for (; ;) {
         if (node.Kind == EKind.Leaf) return node;
         node = node.Find (this, pt);
      }
   }

   // Private data -------------------------------------------------------------
   Point2[] mPts;
   bool[] mDone;           
   Segment[] mSegs;
   int[] mShuffle = [];
   List<Trapezoid> mTraps = [];
   Node mRoot;
   const double FINE = 1e-9;
}
