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

      double d0 = double.MinValue, d1 = double.MaxValue;
      mPts[max] = new (d0, d1); mPts[max + 1] = new (d0, d0);
      mPts[max + 2] = new (d1, d1); mPts[max + 3] = new (d1, d0);
      mSegs[max] = new (mPts, max, max + 1);
      mSegs[max + 1] = new (mPts, max + 2, max + 3);
      mRoot = new (++mNodeID, EKind.Leaf, 0);
      var box = new Trapezoid (mTraps.Count, d0, d1, max, max + 1, mRoot); 
      mTraps.Add (box);
   }
   int mNodeID;

   public void Process () {
      foreach (var n in mShuffle) {
         var seg = mSegs[mShuffle[n]];

         Point2 pa = mPts[seg.A], pb = mPts[seg.B];
         InsertPoint (seg.A); 
         var botSlice = InsertPoint (seg.B) ?? FindSlice (pb, false);
         var topSlice = FindSlice (pa, true);
      }

      var sb = new StringBuilder ();
      mRoot.Dump (this, sb, 0);
      System.IO.File.WriteAllText ("c:/etc/dump.txt", sb.ToString ());
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
         return (a.X - b.X) * (p.Y - b.Y) - (a.Y - b.Y) * (p.X - b.X) > 1;
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

      public void UpdateBottom (Trapezoid old, Trapezoid newT) {
         if (BotA == old) BotA = newT;
         else if (BotB == old) BotB = newT;
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
         if (First != null) First.Dump (owner, sb, level + 1);
         if (Second != null) Second.Dump (owner, sb, level + 1);
      }

      public Node Find (Triangulator owner, Point2 pt) {
         switch (Kind) {
            case EKind.Y: return pt.Y < owner.mPts[Index].Y ? First! : Second!;
            case EKind.X: return owner.mSegs[Index].Left (owner.mPts, pt) ? First! : Second!;
            default: return this;
         }
      }
   }

   // Private data -------------------------------------------------------------
   Point2[] mPts;
   bool[] mDone;           
   Segment[] mSegs;
   int[] mShuffle = [];
   List<Trapezoid> mTraps = [];
   Node mRoot;
}
