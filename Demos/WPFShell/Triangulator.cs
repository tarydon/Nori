using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.MemoryMarshal;
using static System.Runtime.CompilerServices.Unsafe;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Intrinsics.X86;
namespace Nori;

partial class Triangulator {
   public void Reset () {
      mBound = new ();
      mVN = mSN = mNN = mTN = 0;
      if (Lib.Testing) mR = new (42);
   }
   Bound2 mBound;

   public void AddContour (ReadOnlySpan<Point2> pts, bool hole) {
      int n = pts.Length;

      // Add the points into the mV array
      Grow (ref mV, mVN, n);
      Point2 prev = pts[n - 1], pt = pts[0];
      for (int i = 0; i < n; i++) {
         Point2 next = pts[(i + 1) % n];
         double dy0 = prev.Y - pt.Y, dy1 = next.Y - pt.Y;

         EVKind kind = EVKind.Regular;
         if (dy0 > 0 && dy1 > 0) kind = EVKind.Valley;
         else if (dy0 < 0 && dy1 < 0) kind = EVKind.Mountain;
         mV[mVN] = new (mVN, pt, kind);
         mVN++; mBound += pt; 
         prev = pt; pt = next;
      }

      // Now, add the segments into the mS array
      Grow (ref mS, mSN, n);
      ref Vertex vBase = ref GetReference (mV);
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         mS[mSN] = new (mSN, ref vBase, i, j);
         mSN++;
      }
   }

   public Dwg2 GetDebugDwg () {
      Dwg2 dwg = new ();
      dwg.Add (new Layer2 ("TILE", Color4.Red, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var t in mT.Take (mTN)) 
         dwg.Add (Poly.Lines (Point2.List (t.LMin, t.YMin, t.RMin, t.YMin, t.RMax, t.YMax, t.LMax, t.YMax), true));

      dwg.Add (new Layer2 ("TEXT", Color4.Blue, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      dwg.Add (new Style2 ("STD", "SIMPLEX", 0, 1, 0));
      double size = mBound.Height / 100;
      foreach (var t in mT.Take (mTN)) {
         Point2 pos = new (0.25.Along (t.LMin, t.RMin), t.YMin);
         string text = $"{t.Id}"; if (t.Hole) text += "*";
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, pos, size, 0, 0, 1, ETextAlign.BotCenter));
      }
      return dwg;
   }

   public IEnumerable<string> Process () {
      ShuffleSegs ();
      yield return "Started processing";
      InsertBorder ();
      yield return "Inserted border";

      for (int i = 0; i < mSN; i++) {
         ref Segment s0 = ref GetReference (mS);
         ref int sh0 = ref GetReference (mShuffle);        
         ref Segment seg = ref Add (ref s0, Add (ref sh0, i));
         string s = InsertSeg (ref seg);
         yield return $"Inserted seg {seg} :: {s}";
      }
   }

   // Implementation -----------------------------------------------------------
   // This inserts the 'border' tile (the root tile) of the tiling. It is large enough
   // to encompass the complete tessellation, and is initially created as a 'hole' tile,
   // but as we add vertices and segments it will keep getting subdivided into smaller and
   // smaller trapezoids
   void InsertBorder () {
      var b = mBound.InflatedL (1);
      Grow (ref mV, mVN, 4); Grow (ref mS, mSN, 2);
      Point2 p0 = new (b.X.Min, b.Y.Min), p1 = new (b.X.Min, b.Y.Max);
      Point2 p2 = new (b.X.Max, b.Y.Min), p3 = new (b.X.Max, b.Y.Max);
      
      // Add the 4 vertices making up the border
      ref Vertex v0 = ref GetReference (mV);
      Add (ref v0, mVN) = new (mVN, p0, EVKind.Regular); Add (ref v0, mVN + 1) = new (mVN + 1, p1, EVKind.Regular);
      Add (ref v0, mVN + 2) = new (mVN + 2, p2, EVKind.Regular); Add (ref v0, mVN + 3) = new (mVN + 3, p3, EVKind.Regular);
      // Connect them up with two segments (left goes up, right goes down)
      ref Segment s0 = ref GetReference (mS);
      Add (ref s0, mSN + 0) = new (mSN + 0, ref v0, mVN, mVN + 1);
      Add (ref s0, mSN + 1) = new (mSN + 1, ref v0, mVN + 3, mVN + 2);
      // Add the root node
      mN[0] = new (0, EKind.Leaf, 0);
      mT[0] = new Tile (0, ref s0, p0.Y, p1.Y, mSN, mSN + 1, 0);
      // Note we don't bump up the mSN counter, we don't want to consider the two most recently
      // added boundary segments into the tessellation - they serve only to create the dummy
      // tile that acts as the root
      mVN += 4; mNN++; mTN++;
   }

   // This is called to insert a segment into the trapezoid map. 
   // It inserts the two endpoints of the seg (dividing the corresponding trapezoids horizontally
   // each time), and then slices all the trapezoids between the start and end vertically by the
   // segment line
   string InsertSeg (ref Segment seg) {
      // To insert top and bottom points, we could need 2 new tiles (and 2 new nodes)
      Grow (ref mT, mTN, 2); Grow (ref mN, mNN, 2);
      ref Vertex vBase = ref GetReference (mV);
      ref Vertex v0 = ref Add (ref vBase, seg.A), v1 = ref Add (ref vBase, seg.B);
      Check (v0.Pt.Y > v1.Pt.Y);

      int n0 = InsertPoint (ref v0);
      int n1 = InsertPoint (ref v1);
      int t0 = GetTile (n0, true);
      int t1 = GetTile (n1, false);
      return $"Tiles {t0} to {t1}";
   }

   // Returns the tile that is just below or just above the given Y split node.   
   int GetTile (int nNode, bool getBelow) {
      ref Node nBase = ref GetReference (mN);
      ref Node node = ref Add (ref nBase, nNode);
      Check (node.Kind == EKind.Y);
      nNode = getBelow ? node.First : node.Second; 

      for (; ; ) {
         ref Node ntmp = ref Add (ref nBase, nNode); 
         switch (ntmp.Kind) {
            case EKind.Leaf: return ntmp.Index;
            case EKind.Y: nNode = getBelow ? ntmp.Second : ntmp.First; break;
            default: throw new InvalidOperationException ();
         }
      }
   }

   int InsertPoint (ref Vertex v) {
      if (v.Node != -1) return v.Node;

      // Fetch the leaf pointing to the trapezoid that contains this vertex 
      ref Tile t0 = ref Locate (v.Pt, 0);
      Check (v.Pt.Y > t0.YMin && v.Pt.Y < t0.YMax);
      // We hold onto the tile t0's node. This is currently a leaf node, but will
      // shortly become a Y split node created by this vertex
      v.Node = t0.Node;

      // We're going to split t0 into two trapezoids (t0 and t1 along Y). 
      ref Tile t1 = ref t0.Split (this, EKind.Y, v.Id);

      // Update connections: t0 is on the bottom, t1 at the top
      // So now t1 connects to whatever used to be on t0's top
      ref Tile tBase = ref GetReference (mT);
      for (int i = 0; i < 2; i++) {
         int nTop = t1.Top[i] = t0.Top[i];
         if (nTop >= 0) {
            ref Tile above = ref Add (ref tBase, nTop);
            above.UpdateBottom (ref t0, ref t1);
         }
      }
      t0.Top[0] = t1.Id; t0.Top[1] = -1; t1.Bot[0] = t0.Id;
      return v.Node;
   }

   ref Tile Locate (Point2 pt, int root) {
      ref Node nBase = ref GetReference (mN);
      ref Node node = ref Add (ref nBase, root);
      ref Vertex v0 = ref GetReference (mV);
      ref Segment s0 = ref GetReference (mS);
      for (; ; ) {
         bool first; 
         switch (node.Kind) {
            case EKind.Y: first = pt.Y < Add (ref v0, node.Index).Pt.Y; break;
            case EKind.X: first = Add (ref s0, node.Index).IsLeft (pt); break;
            default: return ref mT[node.Index];
         }
         node = ref Add (ref nBase, first ? node.First : node.Second);
      }
   }

   // Computes (in mShuffle) a random permutation of the segments. This is
   // critical to achieve good performance from the Seidel algorithm
   void ShuffleSegs () {
      Grow (ref mShuffle, 0, mSN);
      ref int sh0 = ref GetReference (mShuffle);
      for (int i = 0; i < mSN; i++) Add (ref sh0, i) = i;
      for (int i = 0; i < mSN; i++) {
         int j = mR.Next (mSN);
         (Add (ref sh0, j), Add (ref sh0, i)) = (Add (ref sh0, i), Add (ref sh0, j));
      }
   }

   // Helpers ------------------------------------------------------------------
   static void Check (bool condition) {
      if (!condition) throw new InvalidOperationException ("Triangulator");
   }

   // Helper to grow an array (more optimized than Array.Resize, since it
   // copies only the 'used' elements, not all the elements currently in the array)
   void Grow<T> (ref T[] array, int used, int delta) {
      int size = array.Length, total = used + delta;
      while (size <= total) size *= 2;
      if (size > array.Length) {
         var final = new T[size];
         if (used > 0) Array.Copy (array, final, used);
         array = final;
      }
   }

   static void Unexpected () => Check (false);

   // Private data -------------------------------------------------------------
   Vertex[] mV = new Vertex[32];    // List of all vertices
   Segment[] mS = new Segment[32];  // List of all segments
   Node[] mN = new Node[32];        // Nodes making up the tree
   Tile[] mT = new Tile[32];        // Trapezoidal tiles covering the plane
   int mVN, mSN, mNN, mTN;          // Usage counts (Vertices, Segments, Nodes, Tiles)
   Rand mR = new (42);              // Used for random insertion of segments
   int[] mShuffle = new int[32];    // A permutation of the segments

   const double FINE = 1e-9;
}
