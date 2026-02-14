using System.Collections.Generic;
using System.Security.RightsManagement;
using System.Text;
namespace Nori;

class Triangulator {
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
         Slice (t0, nSeg);
         yield return $"Sliced {t0} with seg {seg}";
         //Slice (t0, nSeg);
      }
   }

   // This 'slices' the trapezoid t into a left and right using the given segment
   void Slice (Trapezoid t0, int nSeg) {
      Segment seg = mSegs[nSeg];
      if (Verify) {
         Segment sL = mSegs[t0.Left], sR = mSegs[t0.Right];
         double yMid = (t0.YMin + t0.YMax) / 2, xL = sL.GetX (mPts, yMid), xR = sR.GetX (mPts, yMid), x = seg.GetX (mPts, yMid);
         Check (x > xL && x < xR); Check (t0.Node.Kind == EKind.Leaf);
      }

      // We're going to split the trapezoid t0 into two trapezoids t0 and t1 by the 
      // segment nSeg. First, create two LeafNodes to hold onto these new trapezoids
      // (noting that t0 is being recycled with a new right segment)
      Node leaf = t0.Node;
      double yMin = t0.YMin, yMax = t0.YMax;
      Node n1 = new (++mNodeID, EKind.Leaf, mTraps.Count);
      Trapezoid t1 = new (mTraps.Count, yMin, yMax, nSeg, t0.Right, n1); mTraps.Add (t1);
      Node n0 = new (++mNodeID, EKind.Leaf, leaf.Index);
      t0.Right = nSeg;

      // The erstwhile 'leaf' node pointing to this trapezoid will now become an 
      // interior node of type X (meaning it is split by a Segment) and the two new nodes we
      // created will become children of it
      leaf.First = n0; leaf.Second = n1; leaf.Kind = EKind.X; leaf.Index = nSeg;

      // We've created two trapezoids t0 and t1 - fix up the connections on the 'upward'
      // side
      Check (t0.TopA != null && t0.BotA != null);
      Trapezoid above0 = t0.TopA!;
      if (t0.TopB == null) {
         // t0 had only one top neighbor. Now that becomes the top neighbor for both 
         // t0 and t1, and t1 gets added to that as a new bottom neighbor
         t1.AddTop (above0); above0.AddBottom (t1);
      } else {
         // There are two neighbors on the top. After adding this new slice line (marked
         // with the *) three scenarios are possible:
         //          |                 |                         |
         // A. --------------    B. -------------    C. -------------
         //          |                    |                   |
         //          *                    *                   * 
         Trapezoid above1 = t0.TopB!;
         Check (above0.Right == above1.Left);
         double xAbove = mSegs[above0.Right].GetX (mPts, yMax), xBelow = seg.GetX (mPts, yMax);
         if (xAbove.EQ (xBelow)) {        // Case A
            // above0 is connected to t0, above1 to t1
            t0.RemoveTop (above1); above1.UpdateBottom (t0, t1); t1.AddTop (above1);
         } else if (xAbove < xBelow) {   // Case B
            // above0 is connected only to t0, while above1 is connected
            // to both t0 and t1
            above1.AddBottom (t1); t1.AddTop (above1); 
         } else {                         // Case C
            // Above0 is now connected to both t0 and t1, while t1
            // is connected to above1 only
            t0.RemoveTop (above1); above0.UpdateBottom (t0, t1); t1.AddTop (above0);
            above1.AddBottom (t1); t1.AddTop (above1);
         }
      }

      // Next, fix up the connections on the 'downward' side
      Trapezoid below0 = t0.BotA!;
      if (t0.BotB == null) {
         // t0 had only one bottom neighbor. Now that bcomes the bottom neighbor for
         // both t0 and t1, and t1 gets added to that asa a new top neighbor
         t1.AddBottom (below0); below0.AddTop (t1);
      } else {
         // There are two neighbors on the bottom. After adding this new slice line (marked
         // with a *), three scenarios are possible
         //         *                    *                   *
         //         |                    |                   |
         // A. -------------    B. -------------    C. -------------
         //         |                        |           |
         Trapezoid below1 = t0.BotB!;
         Check (below0.Right == below1.Left);
         double xAbove = seg.GetX (mPts, yMin), xBelow = mSegs[below0.Right].GetX (mPts, yMin);
         if (xAbove.EQ (xBelow)) {           // Case A
            // below0 connects to t0, below1 connects to t1
            t0.RemoveBottom (below1); below1.UpdateTop (t0, t1); t1.AddBottom (below1);
         } else if (xAbove < xBelow) {       // Case B
            // below0 connects to t0 and t1, below1 connects to t1 only
            t0.RemoveBottom (below1); below1.UpdateTop (t0, t1); t1.AddBottom (below1); 
            t1.AddBottom (below0); below0.AddTop (t1);
         } else {                            // Case C
            // t0 remains connected to both below0, below1, while t1 is connected
            // only to below1
            below1.AddTop (t1); t1.AddBottom (below1);
         }
      }
   }

   bool Verify = true;

   void Check (bool condition) {
      if (!condition) throw new Exception ("Coding error in Triangulate.cs");
   }

   public List<(int, Poly)> GetTrapezoids () {
      List<(int, Poly)> output = [];
      foreach (var t in mTraps) {
         Segment a = mSegs[t.Left], b = mSegs[t.Right];
         double e = 0.01, y0 = t.YMin, y1 = t.YMax;
         Point2 bl = new (a.GetX (mPts, y0) + e, y0 + e), br = new (b.GetX (mPts, y0) - e, y0 + e);
         Point2 tl = new (a.GetX (mPts, y1) + e, y1 - e), tr = new (b.GetX (mPts, y1) - e, y1 - e);
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

   // Nested types -------------------------------------------------------------
   // Each segment runs from top to bottom
   class Segment {
      public Segment (Point2[] pts, int a, int b) {
         Point2 pa = pts[a], pb = pts[b];
         if (pa.Y == pb.Y) throw new NotImplementedException ();
         if (pa.Y < pb.Y) (pa, pb, a, b) = (pb, pa, b, a);
         A = a; B = b; 
         Slope = (pa.X - pb.X) / (pa.Y - pb.Y);
      }

      // Get the X value at a given Y
      public double GetX (Point2[] pts, double y) {
         Point2 b = pts[B];
         return b.X + Slope * (y - b.Y);
      }

      // Is the given point to the left of this segment?
      public bool Left (Point2[] pts, Point2 p) {
         Point2 a = pts[A], b = pts[B];
         return (a.X - b.X) * (p.Y - b.Y) - (a.Y - b.Y) * (p.X - b.X) > 0;
      }

      public int A;                    // Upper point
      public int B;                    // Lower point
      public readonly double Slope;

      public override string ToString () => $"Segment {A} to {B}";
   }

   class Trapezoid {
      public Trapezoid (int id, double yMin, double yMax, int left, int right, Node node) 
         => (Id, YMin, YMax, Left, Right, Node) = (id, yMin, yMax, left, right, node);

      public override string ToString () => $"Trap#{Id} Y:{YMin.R6 ()} to {YMax.R6 ()} Left:{Left} Right:{Right}";

      public void UpdateBottom (Trapezoid? old, Trapezoid newT) {
         if (BotA == old) BotA = newT;
         else if (BotB == old) BotB = newT;
         else throw new NotImplementedException (); 
      }

      public void AddBottom (Trapezoid newT) => UpdateBottom (null, newT);

      public void RemoveBottom (Trapezoid old) {
         if (BotA == old) BotA = BotB;
         else if (BotB == old) BotB = null;
         else throw new NotImplementedException (); 
      }

      public void UpdateTop (Trapezoid? old, Trapezoid newT) {
         if (TopA == old) TopA = newT;
         else if (TopB == old) TopB = newT;
         else throw new NotImplementedException (); 
      }

      public void AddTop (Trapezoid newT) => UpdateTop (null, newT);

      public void RemoveTop (Trapezoid old) {
         if (TopA == old) TopA = TopB;
         else if (TopB == old) TopB = null;
         else throw new NotImplementedException (); 
      }

      public readonly int Id;
      public double YMin, YMax;
      public int Left, Right;
      public Node Node;
      public Trapezoid? BotA, BotB, TopA, TopB;
   }

   enum EKind { Y, X, Leaf }

   class Node {
      public Node (int id, EKind kind, int index) => (Id, Kind, Index) = (id, kind, index);
      public EKind Kind;
      public Node? First, Second;
      public int Index;
      public int Id;

      public override string ToString () => $"Node#{Id} Kind:{Kind} Index:{Index}";

      public void Dump (Triangulator owner, StringBuilder sb, int level) {
         sb.Append (new string (' ', level * 3));
         sb.Append ($"#{Index} {Kind} ");
         if (Kind == EKind.Y) sb.Append (owner.mPts[Index].Y.R6 ());
         sb.AppendLine ();
         First?.Dump (owner, sb, level + 1);
         Second?.Dump (owner, sb, level + 1);
      }

      public Node Find (Triangulator owner, Point2 pt) 
         => Kind switch {
            EKind.Y => pt.Y < owner.mPts[Index].Y ? First! : Second!,
            EKind.X => owner.mSegs[Index].Left (owner.mPts, pt) ? First! : Second!,
            _ => this,
         };
   }

   // Private data -------------------------------------------------------------
   Point2[] mPts;
   bool[] mDone;           
   Segment[] mSegs;
   int[] mShuffle = [];
   List<Trapezoid> mTraps = [];
   Node mRoot;
}
