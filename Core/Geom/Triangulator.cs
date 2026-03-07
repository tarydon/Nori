// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangulator.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public partial class Triangulator {
   // Properties ---------------------------------------------------------------
   /// <summary>List of all the points gathered from all the input Poly</summary>
   public ReadOnlySpan<Point2> Pts => mInput.AsSpan ();
   List<Point2> mInput = [];

   /// <summary>Indices pointing into the Pts array - taken 3 at a time, these are the output triangles</summary>
   public ReadOnlySpan<int> Tris => mTris.AsSpan ();
   List<int> mTris = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a contour for tessellation</summary>
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
      for (int i = 0; i < n; i++, mSN++) {
         int j = (i + 1) % n;
         mS[mSN] = new Segment (mSN, mV, i + vStart, j + vStart);
      }
   }

   /// <summary>Reset should be called to initialize the Triangulator before adding contours</summary>
   public void Reset (int seed = 42, double rotAngle = 0.1624) {
      mBound = new (); mMerged = mAddedDiagonals = false;
      mInput.Clear (); mTris.Clear (); 
      mDiagTiles.Clear (); mValleyTiles.Clear (); 
      mSN = mNN = 0; mTN = mVN = 1;
      if (mBiasAngle != rotAngle) (mSin, mCos) = Math.SinCos (mBiasAngle = rotAngle);
      mR = new ((uint)seed);
   }

   public Bound2 Bound => mBound;
   Bound2 mBound;

   /// <summary>Process is called to actually perform the tessellation</summary>
   public void Process () {
      ShuffleSegs ();
      InsertBorder ();
      for (int i = 0; i < mSN; i++) {
         ref Segment seg = ref mS[mShuffle[i]];
         InsertEndpoints (ref seg);
         SliceTiles (ref seg);
      }
      mMerged = true; 
      AddDiagonals (); mAddedDiagonals = true; 
      foreach (var n in mValleyTiles) ExtractTriangles (n);
   }
   bool mMerged, mAddedDiagonals;

   // Implementation -----------------------------------------------------------
   // This adds diagonals to partition the tiles into a set of monotone polygons
   // that can then be easily triangulated. We walk through the non-hole tiles, and 
   // add a diagonal where needed. A diagonal is needed when the tile has either a 
   // top vertex, or a bottom vertex (or both) of type HSlice. Suppose there is a HSLice vertex
   // at the top. It is then a 'reflex' vertex that needs to be connected to a corner (or another
   // reflex vertex) to split the stack into two monotones. 
   void AddDiagonals () {
      for (int i = mTN - 1; i > 0; i--) {
         ref Tile t = ref mT[i];
         if (t.Id == 0 || t.Hole) continue;
         if (t.VBot != 0 && t.EBot == EChain.HSlice || t.VTop != 0 && t.ETop == EChain.HSlice) mDiagTiles.Add (t.Id);
         if (t.VBot != 0 && t.EBot == EChain.Valley) mValleyTiles.Add (t.Id);
      }
      Grow (ref mS, mSN, mDiagTiles.Count);
      foreach (var n in mDiagTiles) {
         ref Tile t0 = ref mT[n]; if (t0.Id == 0) continue;
         mS[mSN] = new (mSN, mV, t0.VTop, t0.VBot, true);
         ref Segment seg = ref mS[mSN]; mSN++;
         mLefts.Clear (); mLefts.Add (n);
         SliceTiles (ref seg);
         ref Tile t1 = ref mT[mTN - 1];
         if (t0.VBot != 0 && t0.EBot == EChain.Valley) mValleyTiles.Add (t0.Id);
         if (t1.VBot != 0 && t1.EBot == EChain.Valley) mValleyTiles.Add (t1.Id);
      }
   }

   // Given a monotone polygon, extracts the triangles from it using DeBerg's algorithm.
   // The indices of the triangles are added into the mTriangles output array. Note that the indices
   // here are offset by 1 from the mV[] array (since mV[0] is not used). These indices in mTriangles
   // point into the points in the mInput array (which are the original input points, unrotated)
   void ExtractTriangles (int n) {
      mStack.Clear ();
      ref Tile t = ref mT[n];
      if (t.Id == 0) return; else t.Id = 0; 
      mStack.Push ((t.VBot, mV[t.VBot].Pt, true));
      (int, Point2, bool) vPrev = (0, Point2.Nil, false);
      for (; ; ) {
         if (t.VTop != 0) {
            Point2 pt = mV[t.VTop].Pt;
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
                     var (Id, Pt, _) = mStack.Peek ();
                     if (Pt.LeftOf (pt, v0.Pt) == left) {
                        // We can add a triangle
                        if (v0.Left) AddTri (t.VTop, v0.Id, Id);
                        else AddTri (t.VTop, Id, v0.Id);
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
         var (a, b) = t.GetTop (mV); Check (b == 0);
         t = ref mT[a];
      }
      if (mStack.Count > 2) throw new NotImplementedException ();

      // Helper ............................................
      void AddTri (int a, int b, int c) {
         mTris.Add (a - 1); mTris.Add (b - 1); mTris.Add (c - 1);
      }
   }
   Stack<(int Id, Point2 Pt, bool Left)> mStack = [];

   // Returns an 'adjacent' tile touching a vertex, through which the vOther
   // vertex can be reached
   int GetAdjacentTile (ref Vertex v, ref Vertex vOther) {
      // Pick a tile from either TL,TR or from BL,BR
      var (L, R) = (vOther.Pt.Y > v.Pt.Y) ? (v.TL, v.TR) : (v.BL, v.BR);
      if (L == R) return L;
      ref Tile tLeft = ref mT[L];
      ref Segment sRight = ref mS[tLeft.Right];
      return sRight.IsLeft (vOther.Pt) ? L : R;
   }

   // Gather a vertical stack of tiles starting with t0 (at the top) and ending with 
   // t1 (at the bottom), that are being cut by the given segment seg. Results are 
   // returned in mChain (which may contain 1, 2 or more tile indices). 
   void GatherTiles (int t0, int t1, ref Segment seg) {
      mLefts.Clear ();
      mLefts.Add (t0); if (t1 == t0) return;    // Trivial case with only one tile
      while (t0 != t1) {
         ref Tile tile = ref mT[t0];
         var (a, b) = tile.GetBottom (mV);
         if (a == t1 || b == t1) { mLefts.Add (t1); return; }
         if (b > 0) { 
            ref Vertex vb = ref mV[tile.VBot];
            if (seg.IsLeft (vb.Pt)) a = b; 
         }
         mLefts.Add (t0 = a); 
      }
   }
   List<int> mLefts = [], mRights = [];

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
      mV[mVN] = new (mVN, p0); mV[mVN + 1] = new (mVN + 1, p1);
      mV[mVN + 2] = new (mVN + 2, p2); mV[mVN + 3] = new (mVN + 3, p3);
      // Connect them up with two segments (left goes up, right goes down)
      mS[mSN + 0] = new (mSN + 0, mV, mVN, mVN + 1);
      mS[mSN + 1] = new (mSN + 1, mV, mVN + 3, mVN + 2);
      // Add the root node, and the base tile covering the entire rectangular field
      mN[0] = new (0, ENode.Leaf, mTN);
      mT[mTN] = new Tile (mTN, mS, p0.Y, p1.Y, mSN, mSN + 1, 0);
      // Note we don't bump up the mSN counter, we don't want to consider the two most recently
      // added boundary segments into the tessellation - they serve only to create the dummy
      // tile that acts as the root
      mVN += 4; mNN++; mTN++;
   }

   // This is called to insert a segment into the trapezoid map. 
   // It inserts the two endpoints of the seg (dividing the corresponding trapezoids horizontally
   // each time), and then slices all the trapezoids between the start and end vertically by the
   // segment line. After inserting the two endpoints, this also gathers all the tiles that 
   // would be cut by the segment and adds them to the mLefts array
   void InsertEndpoints (ref Segment seg) {
      // To insert top and bottom points, we could need 2 new tiles (and 4 new nodes)
      Grow (ref mT, mTN, 2); Grow (ref mN, mNN, 4);
      ref Vertex v0 = ref mV[seg.A], v1 = ref mV[seg.B];
      if (!v0.Inserted) InsertVertex (ref v0);
      if (!v1.Inserted) InsertVertex (ref v1);
      int t0 = GetAdjacentTile (ref v0, ref v1), t1 = GetAdjacentTile (ref v1, ref v0);
      GatherTiles (t0, t1, ref seg);
   }

   // If the given Vertex has not yet been inserted into the DAG, this inserts
   // it (by slicing a tile horizontally at v.Y). It returns the adjacent tile 
   // through which one could reach the other vertex vOther. 
   void InsertVertex (ref Vertex v) {
      v.Inserted = true;
      // Fetch the leaf pointing to the trapezoid that contains this vertex 
      ref Tile t0 = ref Locate (v.Pt);
      double y = v.Pt.Y;
      if (y.EQ (t0.YMin, FINE) || y.EQ (t0.YMax, FINE)) throw new Exception ("Horizontal segment in Triangulator");
      Check (y > t0.YMin && y < t0.YMax);
      // We're going to split t0 into two trapezoids (t0 and t1 along Y). 
      t0.SplitY (this, ref v);
   }

   // Given a point in space, returns the tile that contains it
   ref Tile Locate (Point2 pt) {
      ref Node node = ref mN[0], prev = ref node;
      for (; ; ) {
         bool first; 
         switch (node.Kind) {
            case ENode.Y: first = pt.Y < mV[node.Index].Pt.Y; break;
            case ENode.X: first = mS[node.Index].IsLeft (pt); break;
            case ENode.Leaf: return ref mT[node.Index];
            case ENode.Redirect: 
               if (prev.First == node.Id) prev.First = node.First;
               else if (prev.Second == node.Id) prev.Second = node.First;
               first = true; 
               break;
            default: throw new InvalidOperationException ();
         }
         prev = ref node; 
         node = ref mN[first ? node.First : node.Second];
      }
   }

   // Merges together two tiles t0 & t1. We keep t1 (the bottom tile), and kill t0
   void MergeTiles (ref Tile t0, ref Tile t1) {
      Check (t0.YMin.EQ (t1.YMax));
      Check (t0.Left == t1.Left && t0.Right == t1.Right);
      t1.YMax = t0.YMax; t1.VTop = t0.VTop; t1.ETop = t0.ETop;
      if (t1.ETop != EChain.Mountain) {
         ref Vertex vt = ref mV[t0.VTop];
         vt.ReplaceBottom (t0.Id, t1.Id);
      }
      // The node that used to point to t0 should be redirected to point to t1
      // A simple O(1) way to do this is to insert a 'redirect' node instead
      ref Node n0 = ref mN[t0.Node];
      Check (n0.Index == t0.Id);
      n0.Kind = ENode.Redirect; n0.First = t1.Node;
      t0.Id = 0;
   }

   // Rotate a point through the bias angle
   Point2 Rotate (Point2 pt) 
      => new (pt.X * mCos - pt.Y * mSin, pt.X * mSin + pt.Y * mCos);

   // Computes (in mShuffle) a random permutation of the segments. This is
   // critical to achieve good performance from the Seidel algorithm
   void ShuffleSegs () {
      Grow (ref mShuffle, 0, mSN);
      for (int i = 0; i < mSN; i++) mShuffle[i] = i;
      for (int i = 0; i < mSN; i++) {
         int j = mR.Next (mSN);
         (mShuffle[i], mShuffle[j]) = (mShuffle[j], mShuffle[i]);
      }
   }

   // We gathered (in mChain) a list of tiles that need to be sliced by the given segment
   void SliceTiles (ref Segment seg) {
      mRights.Clear ();
      int n = mLefts.Count;
      Grow (ref mT, mTN, n); Grow (ref mN, mNN, n * 2);
      // First, split every tile in the mChain list. This tile remains as the left tile, 
      // and we create a new right tile, both separated by the Segment in between them. 
      // For each layer of tiles that we create, create a Layer object to hold the details
      // of the tiles in this layer, along with that tile's UP / DOWN connected neighbors
      for (int i = 0; i < n; i++) {
         ref Tile left = ref mT[mLefts[i]];
         ref Tile right = ref left.SplitX (this, seg.Id);
         mRights.Add (right.Id);
         if (left.VTop == 0) MergeTiles (ref mT[mLefts[i - 1]], ref left);
         if (right.VTop == 0) MergeTiles (ref mT[mRights[i - 1]], ref right);
      }
   }

   // Helpers ------------------------------------------------------------------
   static partial void Check (bool condition);
   //static partial void Check (bool condition) {
   //   if (!condition) throw new InvalidOperationException ("Triangulator");
   //}

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

   static void Unexpected () 
      => throw new InvalidOperationException ("Triangulator.Unexpected");

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
}
