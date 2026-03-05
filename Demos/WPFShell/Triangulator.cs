using System.Windows.Documents;
using System.Windows.Markup.Localizer;
using System.Windows.Media;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;
namespace Nori;

partial class Triangulator {
   // Properties ---------------------------------------------------------------
   public double BiasAngle => mBiasAngle;

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Adds a contour for tessellation
   /// </summary>
   public void AddPoly (Poly poly, bool hole) {
      // First, if we need to reverse the order of points, or to discretize a Poly
      // with curves, make a copy
      int start = mInput.Count;
      poly.Discretize (mInput, Lib.CoarseTess, Lib.CoarseTessAngle);
      if (poly.GetWinding () == Poly.EWinding.CW ^ hole) mInput.Reverse (start, mInput.Count - start);
      ReadOnlySpan<Point2> pts = mInput.AsSpan ()[start..];

      // Now, add the contour into the mV array, and create segments from this in
      // the mS array
      int n = pts.Length, vStart = mVN;
      Grow (ref mV, mVN, n);
      Point2 prev = Rotate (pts[n - 1]), pt = Rotate (pts[0]);
      for (int i = 0; i < n; i++, mVN++) {
         Point2 next = Rotate (pts[(i + 1) % n]);
         double dy0 = prev.Y - pt.Y, dy1 = next.Y - pt.Y;

         EVertex kind = EVertex.Regular;
         if (dy0 > 0 && dy1 > 0) kind = EVertex.Valley;
         else if (dy0 < 0 && dy1 < 0) kind = EVertex.Mountain;
         mV[mVN] = new Vertex (mVN, pt, kind);
         mBound += pt; prev = pt; pt = next;
      }

      // Now, add the segments corresponding to this newly added contour
      Grow (ref mS, mSN, n);
      ref Vertex vBase = ref GetReference (mV);
      for (int i = 0; i < n; i++, mSN++) {
         int j = (i + 1) % n;
         mS[mSN] = new Segment (mSN, ref vBase, i + vStart, j + vStart);
      }
   }
   List<Point2> mInput = [];

   /// <summary>Reset should be called to initialize the Triangulator before adding contours</summary>
   public void Reset (int seed = 42, double rotAngle = 0.0812) {
      mBound = new (); mMerged = false;
      mInput.Clear (); mTriangles.Clear (); 
      mDiagTiles.Clear (); mValleyTiles.Clear (); 
      mSN = mNN = 0; mTN = mVN = 1;
      if (mBiasAngle != rotAngle) (mSin, mCos) = Math.SinCos (mBiasAngle = rotAngle);
      mR = new ((uint)seed);
   }
   Bound2 mBound;

   /// <summary>Process is called to actually perform the tessellation</summary>
   public IEnumerable<string> Process () {
      ShuffleSegs ();
      InsertBorder ();
      yield return "Inserted border";

      for (int i = 0; i < mSN; i++) {
         {
            ref Segment s0 = ref GetReference (mS);
            ref int sh0 = ref GetReference (mShuffle);
            ref Segment seg = ref Add (ref s0, Add (ref sh0, i));
            yield return $"About to insert {seg}";
         }
         {
            ref Segment s0 = ref GetReference (mS);
            ref int sh0 = ref GetReference (mShuffle);
            ref Segment seg = ref Add (ref s0, Add (ref sh0, i));
            string s = InsertSeg (ref seg);
            yield return $"Inserted seg {seg} :: {s}";
         }
         {
            ref Segment s0 = ref GetReference (mS);
            ref int sh0 = ref GetReference (mShuffle);
            ref Segment seg = ref Add (ref s0, Add (ref sh0, i));
            SliceTiles (ref seg);
            yield return $"Sliced tiles";
         }
      }
      yield return "Ready to merge";
      MergeTiles ();
      yield return $"Merged. Valleys:{mValleyTiles.ToCSV ()} Diags:{mDiagTiles.ToCSV ()}";
      AddDiagonals ();
      yield return $"Added. Valleys:{mValleyTiles.ToCSV ()}";
      foreach (var n in mValleyTiles) {
         yield return ExtractTriangles (n);
      }
   }
   bool mMerged;

   string ExtractTriangles (int n) {
      mStack.Clear (); 
      ref Tile tBase = ref GetReference (mT);
      ref Tile t = ref Add (ref tBase, n); if (t.Id == 0) return "";
      t.Id = 0; 
      ref Vertex vBase = ref GetReference (mV);
      mStack.Push ((t.VBot, Add (ref vBase, t.VBot).Pt, true));
      (int, Point2, bool) vPrev = (0, Point2.Nil, false);
      for (; ; ) {
         if (t.VTop != 0) {
            Point2 pt = Add (ref vBase, t.VTop).Pt;
            bool left = t.ETop == EChain.Left;
            if (mStack.Count < 2) {
               vPrev = (t.VTop, pt, left);
               mStack.Push ((t.VTop, pt, left));
            } else {
               var v0 = mStack.Pop ();
               if (v0.Left == left) {
                  // The newly seen vertex is on the same chain as the set of reflex
                  // vertices on the stack already
                  while (mStack.Count > 0) {
                     var v1 = mStack.Peek ();
                     if (v1.Pt.LeftOf (pt, v0.Pt) == left) {
                        // We can add a triangle
                        if (v0.Left) AddTri (t.VTop, v0.Id, v1.Id);
                        else AddTri (t.VTop, v1.Id, v0.Id);
                        if (mStack.Count == 1) break;
                        else v0 = mStack.Pop ();
                     } else {
                        mStack.Push (v0);
                        break;
                     }
                  }
               } else {
                  // Newly seen vertex is on the opposite side
                  while (mStack.Count > 0) {
                     var v1 = mStack.Pop ();
                     if (v0.Left) AddTri (t.VTop, v0.Id, v1.Id);
                     else AddTri (t.VTop, v1.Id, v0.Id);
                     v0 = v1;
                  }
                  mStack.Push (vPrev);
               }
               vPrev = (t.VTop, pt, left); mStack.Push (vPrev);
            }
            if (t.ETop == EChain.Mountain) break;
         }
         Check (t.Top[0] != 0 && t.Top[1] == 0);
         t = ref Add (ref tBase, t.Top[0]);
      }
      if (mStack.Count > 2) throw new NotImplementedException (); 
      return "---";

      void AddTri (int a, int b, int c) {
         Lib.Trace ($"Tri: {a} {b} {c}");
         mTriangles.Add (a - 1); mTriangles.Add (b - 1); mTriangles.Add (c - 1);
      }
   }
   Stack<(int Id, Point2 Pt, bool Left)> mStack = [];
   List<int> mTriangles = [];

   // Implementation -----------------------------------------------------------
   public Triangulator () => (mSin, mCos) = Math.SinCos (mBiasAngle = 0);

   // This adds diagonals to partition the tiles into a set of monotone polygons
   // that can then be easily triangulated. We walk through the non-hole tiles, and 
   // add a diagonal where needed. A diagonal is needed when the tile has either a 
   // top vertex, or a bottom vertex (or both) of type HSlice. Suppose there is a HSLice vertex
   // at the top. It is then a 'reflex' vertex that needs to be connected to a corner (or another
   // reflex vertex) to split the stack into two monotones. 
   void AddDiagonals () {
      Grow (ref mS, mSN, mDiagTiles.Count);
      ref Segment sBase = ref GetReference (mS);
      ref Vertex vBase = ref GetReference (mV);
      foreach (var n in mDiagTiles) {
         ref Tile tBase = ref GetReference (mT);
         ref Tile t = ref Add (ref tBase, n); if (t.Id == 0) continue;
         mS[mSN] = new (mSN, ref vBase, t.VTop, t.VBot, true);
         ref Segment seg = ref Add (ref sBase, mSN); mSN++;
         mChain.Clear (); mChain.Add (n);
         SliceTiles (ref seg);
         ref Tile t1 = ref Add (ref tBase, mTN - 1);
         if (t.VBot != 0 && t.EBot == EChain.Valley) mValleyTiles.Add (t.Id);
         if (t1.VBot != 0 && t1.EBot == EChain.Valley) mValleyTiles.Add (t1.Id);
      }
   }

   // Returns an 'adjacent' tile touching a vertex, through which the vOther
   // vertex can be reached
   int GetAdjacentTile (ref Vertex v, ref Vertex vOther) {
      // We've already inserted this vertex, so try and return the tile through
      // which we might reach vOther. 
      if (v.Kind == EVertex.Regular) {
         // If it's a regular vertex, Tile[0] is the tile below, and Tile[1] is the
         // tile above. Just return one or the other depending on the position of
         // vOther relative to v. 
         return vOther.Pt.Y > v.Pt.Y ? v.Tile[1] : v.Tile[0];
      } else {
         // It's a mountain or valley vertex, and the two tiles we've stored here
         // are both below (if mountain) or both above (if valley). Pick the tile through
         // which the vOther can be reached. Note that at this point, Tile[0] is the LEFT
         // tile and Tile[1] is the RIGHT tile.
         ref Tile tBase = ref GetReference (mT);
         if (v.Tile[1] == 0) return v.Tile[0];
         ref Tile tLeft = ref Add (ref tBase, v.Tile[0]);
         if (Verify) {
            ref Tile tRight = ref Add (ref tBase, v.Tile[1]);
            Check (tLeft.Right == tRight.Left);
         }
         ref Segment seg = ref Add (ref GetReference (mS), tLeft.Right);
         return seg.IsLeft (vOther.Pt) ? v.Tile[0] : v.Tile[1];
      }
   }

   // Gather a vertical stack of tiles starting with t0 (at the top) and ending with 
   // t1 (at the bottom), that are being cut by the given segment seg. Results are 
   // returned in mChain (which may contain 1, 2 or more tile indices). 
   void GatherTiles (int t0, int t1, ref Segment seg) {
      mChain.Clear ();
      // Trivial case: only one tile
      mChain.Add (t0); if (t1 == t0) return;
      ref Tile tBase = ref GetReference (mT);
      ref Tile tile0 = ref Add (ref tBase, t0);
      // Simple case: only two tiles
      if (tile0.Bot[0] == t1 || tile0.Bot[1] == t1) { mChain.Add (t1); return; }

      // Here, handle the more general case where we have to navigate downwards
      // following one of the BotA/BotB links each time. Sometimes, only one BotA is set, and this 
      // is trivial. Otherwise, we have to examine both the below tiles to see which one
      // the given segment passes through
      ref Segment sBase = ref GetReference (mS);
      while (t0 != t1) {
         ref Tile tile = ref Add (ref tBase, t0);
         if (tile.Bot[1] == 0) t0 = tile.Bot[0];
         else {
            // This node has both Bot[0] and Bot[1] set, figure out which one contains
            // the segment in question
            ref Tile tBot0 = ref Add (ref tBase, tile.Bot[0]), tBot1 = ref Add (ref tBase, tile.Bot[1]);
            double yTest = (Math.Max (tBot0.YMin, tBot1.YMin) + tile.YMin) / 2;
            double x = seg.GetX (yTest);
            double xL = Add (ref sBase, tBot0.Right).GetX (yTest);
            if (x < xL) t0 = tile.Bot[0];
            else {
               Check (x > Add (ref sBase, tBot1.Left).GetX (yTest)); // REMOVETHIS
               t0 = tile.Bot[1];
            }
         }
         mChain.Add (t0);
      }
   }
   List<int> mChain = [];

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
      ref Vertex vBase = ref GetReference (mV);
      Add (ref vBase, mVN) = new (mVN, p0); Add (ref vBase, mVN + 1) = new (mVN + 1, p1);
      Add (ref vBase, mVN + 2) = new (mVN + 2, p2); Add (ref vBase, mVN + 3) = new (mVN + 3, p3);
      // Connect them up with two segments (left goes up, right goes down)
      ref Segment sBase = ref GetReference (mS);
      Add (ref sBase, mSN + 0) = new (mSN + 0, ref vBase, mVN, mVN + 1);
      Add (ref sBase, mSN + 1) = new (mSN + 1, ref vBase, mVN + 3, mVN + 2);
      // Add the root node, and the base tile covering the entire rectangular field
      mN[0] = new (0, ENode.Leaf, mTN);
      mT[mTN] = new Tile (mTN, ref sBase, p0.Y, p1.Y, mSN, mSN + 1, 0);
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
      // To insert top and bottom points, we could need 2 new tiles (and 4 new nodes)
      Grow (ref mT, mTN, 2); Grow (ref mN, mNN, 4);
      ref Vertex vBase = ref GetReference (mV);
      ref Vertex v0 = ref Add (ref vBase, seg.A), v1 = ref Add (ref vBase, seg.B);
      Check (v0.Pt.Y > v1.Pt.Y);

      InsertVertex (ref v0); InsertVertex (ref v1);
      int t0 = GetAdjacentTile (ref v0, ref v1), t1 = GetAdjacentTile (ref v1, ref v0);
      GatherTiles (t0, t1, ref seg);
      return $"Tiles: {mChain.ToCSV ()}";
   }

   // If the given Vertex has not yet been inserted into the DAG, this inserts
   // it (by slicing a tile horizontally at v.Y). It returns the adjacent tile 
   // through which one could reach the other vertex vOther. 
   void InsertVertex (ref Vertex v) {
      if (v.Inserted) return; 
      v.Inserted = true;

      // Fetch the leaf pointing to the trapezoid that contains this vertex 
      ref Tile t0 = ref Locate (v.Pt);
      Check (v.Pt.Y > t0.YMin && v.Pt.Y < t0.YMax);
      // We're going to split t0 into two trapezoids (t0 and t1 along Y). 
      ref Tile t1 = ref t0.Split (this, ENode.Y, v.Id);
      switch (v.Kind) {
         // For a regular vertex, we store the tile below in Tile[0], and the one above in 
         // Tile[1], since both will get Split later by Segments
         case EVertex.Regular: v.Tile[0] = t0.Id; v.Tile[1] = t1.Id; break;
         // For a mountain or valley vertex, we store only the tile below (for mountain), or the
         // tile above (for valley), since that is the only tile that will get split later (twice)
         case EVertex.Mountain: v.Tile[0] = t0.Id; break; 
         default: v.Tile[0] = t1.Id; break;               
      }

      // Update connections: whoever used to be t0's above neighbor now becomes t1's neighbor
      ref Tile tBase = ref GetReference (mT);
      for (int i = 0; i < 2; i++) {
         int nTop = t1.Top[i] = t0.Top[i];
         if (nTop > 0) {
            ref Tile above = ref Add (ref tBase, nTop);
            above.UpdateBottom (t0.Id, ref t1);
         }
      }
      t0.Top[0] = t1.Id; t0.Top[1] = 0; t1.Bot[0] = t0.Id;
   }

   // Given a point in space, returns the tile that contains it
   ref Tile Locate (Point2 pt) {
      ref Node nBase = ref GetReference (mN);
      ref Vertex vBase = ref GetReference (mV);
      ref Segment sBase = ref GetReference (mS);
      ref Tile tBase = ref GetReference (mT);
      ref Node node = ref Add (ref nBase, 0);
      for (; ; ) {
         bool first; 
         switch (node.Kind) {
            case ENode.Y: first = pt.Y < Add (ref vBase, node.Index).Pt.Y; break;
            case ENode.X: first = Add (ref sBase, node.Index).IsLeft (pt); break;
            default: return ref Add (ref tBase, node.Index);
         }
         node = ref Add (ref nBase, first ? node.First : node.Second);
      }
   }

   // This removes hole tiles, and vertically merges together tiles that share the same
   // Left and Right edges
   void MergeTiles () {
      ref Tile tBase = ref GetReference (mT);
      for (int i = 1; i < mTN; i++) {
         ref Tile t = ref Add (ref tBase, i);
         if (t.Hole || t.Id == 0) { t.Id = 0; continue; }
         if (t.VTop == 0) {
            // Basically, if t.VTop is zero, it means this tile has no vertex points on either
            // the top left or top right (and that means it's 'continuous' with the tile above,
            // and can be merged with it)
            Check (t.Top[0] != 0 && t.Top[1] == 0);
            ref Tile t1 = ref Add (ref tBase, t.Top[0]);
            Check (!t1.Hole);
            t.YMax = t1.YMax; t.LMax = t1.LMax; t.RMax = t1.RMax;
            t.VTop = t1.VTop; t.ETop = t1.ETop;
            for (int j = 0; j < 2; j++) {
               if ((t.Top[j] = t1.Top[j]) == 0) continue;
               ref Tile tAbove = ref Add (ref tBase, t.Top[j]);
               tAbove.UpdateBottom (t1.Id, ref t);
            }
            t1.Id = 0;
            i--;
         } else {
            if (t.VBot != 0 && t.EBot == EChain.Valley) mValleyTiles.Add (t.Id);
            if (t.VBot != 0 && t.EBot == EChain.HSlice || t.VTop != 0 && t.ETop == EChain.HSlice) mDiagTiles.Add (t.Id);
         }
      }
      mMerged = true;
   }

   // Rotate a point through the bias angle
   Point2 Rotate (Point2 pt) 
      => new (pt.X * mCos - pt.Y * mSin, pt.X * mSin + pt.Y * mCos);

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

   // We gathered (in mChain) a list of tiles that need to be sliced by the given segment
   void SliceTiles (ref Segment seg) {
      int n = mChain.Count;
      Grow (ref mT, mTN, n); Grow (ref mN, mNN, n * 2);
      while (mLayers.Count < n) mLayers.Add (new ());
      ref Tile tBase = ref GetReference (mT);
      // First, split every tile in the mChain list. This tile remains as the left tile, 
      // and we create a new right tile, both separated by the Segment in between them. 
      // For each layer of tiles that we create, create a Layer object to hold the details
      // of the tiles in this layer, along with that tile's UP / DOWN connected neighbors
      for (int i = 0; i < n; i++) {
         ref Tile left = ref Add (ref tBase, mChain[i]);
         ref Tile right = ref left.Split (this, ENode.X, seg.Id);
         mLayers[i].Init (ref left, ref right);
      }

      // Now adjust the layers by adding in the newly create 'right' tiles into the
      // appropriate Above/Below lists
      for (int i = 1; i < n; i++) mLayers[i - 1].AddRights (mLayers[i]);
      // Finally, connect up the layers
      for (int i = 0; i < n; i++) mLayers[i].Connect (ref tBase); 
   }
   List<Layer> mLayers = [];

   // Helpers ------------------------------------------------------------------
   static void Check (bool condition) {
      if (!condition) 
         throw new InvalidOperationException ("Triangulator");
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
   List<int> mDiagTiles = [];       // Tiles where diagonals need to be drawn
   List<int> mValleyTiles = [];     // Valley tiles, from which we start monotone polygons
   double mBiasAngle, mSin, mCos;

   const double FINE = 1e-9;
   const bool Verify = true;
}
