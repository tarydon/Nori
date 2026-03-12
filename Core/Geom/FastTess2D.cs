// ────── ╔╗
// ╔═╦╦═╦╦╬╣ FastTess2D.cs
// ║║║║╬║╔╣║ Implements a Tessellator that can handle non-intersecting simple polygons
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using JetBrains.Annotations;
namespace Nori;

#region class FastTess2D ---------------------------------------------------------------------------
/// <summary>Implements a performant tessellator that can handle non-intersecting simple polygons</summary>
/// This can handle only non-intersecting polygons, including polygons with holes. It is designed
/// for the common use-case of tessellating parametric surfaces where the contours are non-intersecting,
/// and it is easy to determine which are the outer contours, and which are the holes. 
public partial class FastTess2D : IBorrowable<FastTess2D> {
   // Properties ---------------------------------------------------------------
   /// <summary>Sets the rotation bias angle to avoid horizontal lines (don't set this to any round number of degrees!)</summary>
   /// Normally, you never need to set this - it exists more as a debugging / testing aid
   public double BiasAngle {
      get => mBiasAngle;
      set { if (mBiasAngle != value) (mSin, mCos) = Math.SinCos (mBiasAngle = value); }
   }

   /// <summary>The total set of points (obtained by discretizing the polys)</summary>
   /// This is the set of points into which the Tris array indexes. 
   /// Caution: Don't hold onto this while you are adding polys - the list may grow, and
   /// the span may become stale. 
   public List<Point2> Pts => mInput;
   readonly List<Point2> mInput = [];

   /// <summary>The discretization tolerance</summary>
   public ETolerance Tolerance {
      set {
         if (value == mETol) return;
         switch (mETol = value) {
            case ETolerance.Fine: mTolerance = Lib.FineTess; mAngTolerance = Lib.FineTessAngle; break;
            case ETolerance.Coarse: mTolerance = Lib.CoarseTess; mAngTolerance = Lib.CoarseTessAngle; break;
            default: throw new BadCaseException (value);
         }
      }
   }
   ETolerance mETol;

   /// <summary>The set of integers making up the triangles</summary>
   /// These integers, taken 3 at a time, point into the Pts array
   public List<int> Tris => mTris;
   readonly List<int> mTris = [];

   // Methods ------------------------------------------------------------------
   /// <summary>This is used to borrow a Triangulator for use</summary>
   /// The usage is like this:
   /// <code>
   /// using var tess = FastTess2D.Borrow ();
   /// tess.AddPoly (...); tess.AddPoly (...); tess.AddPoly (...);
   /// tess.Process ();
   /// var pts = tess.Pts;          // Set of points gathered from all the poly
   /// var triangles = tess.Tris;   // Integers combining those pts into triangles (take 3 at a time)
   /// </code>
   ///
   /// Triangulator.Borrow returns a triangulator (from a pool of triangulators that is maintained),
   /// and also an IDisposable that must be disposed to release the triangulator back into the pool.
   /// So make sure to put the return value into a using statement so this happens automatically.
   [MustDisposeResource]
   public static FastTess2D Borrow () {
      var tess = BorrowPool<FastTess2D>.Borrow ();
      tess.Reset ();
      return tess;
   }

   /// <summary>Adds a contour for tessellation</summary>
   /// Returns the number of points added into the tessellation for this contour
   public int AddPoly (Poly poly, bool hole) {
      // First, if we need to reverse the order of points, or to discretize a Poly
      // with curves, make a copy
      int start = mInput.Count;
      poly.Discretize (mInput, mTolerance, mAngTolerance);
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
      return n; 
   }

   /// <summary>Process is called to actually perform the tessellation</summary>
   public void Process () {
      ShuffleSegs ();
      InsertBorder ();
      for (int i = 0; i < mSN; i++) {
         ref Segment seg = ref mS[mShuffle[i]];
         InsertEndpoints (ref seg);
         SliceTiles (ref seg);
      }
      AddDiagonals (); 
      foreach (var n in mValleyTiles) ExtractTriangles (n);
   }

   // Implementation -----------------------------------------------------------
   FastTess2D () { }

   // This adds diagonals to partition the tiles into a set of monotone polygons
   // that can then be easily triangulated. We walk through the non-hole tiles, and 
   // add a diagonal where needed. A diagonal is needed when the tile has either a 
   // top vertex, or a bottom vertex (or both) of type HSlice. Suppose there is a HSLice vertex
   // at the top. It is then a 'reflex' vertex that needs to be connected to a corner (or another
   // reflex vertex) to split the stack into two monotones. 
   void AddDiagonals () {
      for (int i = mTN_ - 1; i > 0; i--) {
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
         int nNewTile = SliceTiles (ref seg);
         ref Tile t1 = ref mT[nNewTile];
         if (t0.VBot != 0 && t0.EBot == EChain.Valley) mValleyTiles.Add (t0.Id);
         if (t1.VBot != 0 && t1.EBot == EChain.Valley) mValleyTiles.Add (t1.Id);
      }
   }

   // Allocates a new tile ID
   int AllocTile () {
      if (mFreeTile.Count > 0) return mFreeTile.Pop ();
      Grow (ref mT, mTN_, 1); return mTN_++;
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
   readonly Stack<(int Id, Point2 Pt, bool Left)> mStack = [];

   // Returns an 'adjacent' tile touching a vertex, through which the vOther
   // vertex can be reached
   int GetAdjacentTile (ref Vertex v, ref Vertex vOther) {
      // Pick a tile from either TL,TR or from BL,BR
      var (L, R) = vOther.Pt.Y > v.Pt.Y ? (v.TL, v.TR) : (v.BL, v.BR);
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
   readonly List<int> mLefts = [], mRights = []; 

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
      int nNew = AllocTile ();
      mN[0] = new (0, ENode.Leaf, nNew);
      mT[nNew] = new Tile (nNew, mS, p0.Y, p1.Y, mSN, mSN + 1, 0);
      // Note we don't bump up the mSN counter, we don't want to consider the two most recently
      // added boundary segments into the tessellation - they serve only to create the dummy
      // tile that acts as the root
      mVN += 4; mNN++;
   }

   // This is called to insert a segment into the trapezoid map. 
   // It inserts the two endpoints of the seg (dividing the corresponding trapezoids horizontally
   // each time), and then slices all the trapezoids between the start and end vertically by the
   // segment line. After inserting the two endpoints, this also gathers all the tiles that 
   // would be cut by the segment and adds them to the mLefts array
   void InsertEndpoints (ref Segment seg) {
      // To insert top and bottom points, we could need 2 new tiles (and 4 new nodes)
      Grow (ref mT, mTN_, 2); Grow (ref mN, mNN, 4);
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
      mFreeTile.Push (t0.Id); t0.Id = 0;
   }

   // Reset is be called at the beginning initialize the Triangulator before adding contours
   void Reset () {
      mBound = new ();
      mInput.Clear (); mTris.Clear ();
      mDiagTiles.Clear (); mValleyTiles.Clear (); mFreeTile.Clear ();
      mR = new Rand (42); Tolerance = ETolerance.Coarse; BiasAngle = 0.1642;
      mSN = mNN = 0; mTN_ = mVN = 1;
   }

   // Rotate a point through the bias angle
   Point2 Rotate (Point2 pt) 
      => new (pt.X * mCos - pt.Y * mSin, pt.X * mSin + pt.Y * mCos);

   // Computes (in mShuffle) a random permutation of the segments. This is
   // critical to achieve good performance from the Seidel algorithm
   void ShuffleSegs () {
      Grow (ref mShuffle, 0, mSN);
      for (int i = 0; i < mSN; i++) mShuffle[i] = i;
      // This is a simple Fisher-Yates shuffle:
      for (int i = mSN - 1; i >= 0; i--) {
         int j = mR.Next (i + 1);
         (mShuffle[i], mShuffle[j]) = (mShuffle[j], mShuffle[i]);
      }
   }

   // We gathered (in mChain) a list of tiles that need to be sliced by the given segment.
   // This returns the last tile sliced.
   int SliceTiles (ref Segment seg) {
      mRights.Clear ();
      int n = mLefts.Count;
      Grow (ref mT, mTN_, n); Grow (ref mN, mNN, n * 2);
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
      return mRights[^1];
   }

   // Helpers ------------------------------------------------------------------
   static partial void Check (bool condition);
   // static partial void Check (bool condition) {
   //    if (!condition) throw new InvalidOperationException ("Triangulator");
   // }

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

   // IBorrowable<T> implementation --------------------------------------------
   static FastTess2D IBorrowable<FastTess2D>.Make () => new ();

   static ref FastTess2D? IBorrowable<FastTess2D>.Next (FastTess2D item) => ref item.mNext;
   FastTess2D? mNext;

   void IDisposable.Dispose () => BorrowPool<FastTess2D>.Return (this);

   // Private data -------------------------------------------------------------
   Vertex[] mV = new Vertex[32];          // List of all vertices
   Segment[] mS = new Segment[32];        // List of all segments
   Node[] mN = new Node[32];              // Nodes making up the tree
   Tile[] mT = new Tile[32];              // Trapezoidal tiles covering the plane
   int mVN, mSN, mNN, mTN_;               // Usage counts (Vertices, Segments, Nodes, Tiles)
   Rand mR = new (42);                    // Used for random insertion of segments
   int[] mShuffle = new int[32];          // A permutation of the segments
   readonly List<int> mDiagTiles = [];    // Tiles where diagonals need to be drawn
   readonly List<int> mValleyTiles = [];  // Valley tiles, from which we start monotone polygons
   readonly Stack<int> mFreeTile = [];    // Tiles that are free for reuse
   Bound2 mBound;                         // Bound of poly added so far (in rotated coordinates)
   double mBiasAngle, mSin, mCos;
   double mTolerance, mAngTolerance;
   const double FINE = 1e-9;
}
#endregion
