using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;
namespace Nori;

partial class Triangulator {
   // Methods ------------------------------------------------------------------
   /// <summary>Adds a contour to the triangulator</summary>
   /// The pts list making up the contour should wind CCW if this is an outer contour,
   /// and should wind CW if this is a hole. In addition, the 'hole' parameter should also
   /// be set true or false appropriately. 
   public void AddContour (ReadOnlySpan<Point2> pts, bool hole) {
      int n = pts.Length;

      // Add the points into the mV array
      int vStart = mVN;
      Grow (ref mV, mVN, n);
      Point2 prev = pts[n - 1], pt = pts[0];
      for (int i = 0; i < n; i++) {
         Point2 next = pts[(i + 1) % n];
         double dy0 = prev.Y - pt.Y, dy1 = next.Y - pt.Y;

         EVertex kind = EVertex.Regular;
         if (dy0 > 0 && dy1 > 0) kind = EVertex.Valley;
         else if (dy0 < 0 && dy1 < 0) kind = EVertex.Mountain;
         mV[mVN] = new (mVN, pt, kind);
         mVN++; mBound += pt;
         prev = pt; pt = next;
      }

      // Now, add the segments into the mS array
      Grow (ref mS, mSN, n);
      ref Vertex vBase = ref GetReference (mV);
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         mS[mSN] = new (mSN, ref vBase, i + vStart, j + vStart);
         mSN++;
      }
   }

   /// <summary>Reset should be called to initialize the Triangulator before adding contours</summary>
   public void Reset () {
      mBound = new ();
      mSN = mNN = 0; mTN = mVN = 1;
      if (Lib.Testing) mR = new (42);
   }
   Bound2 mBound;

   /// <summary>Returns a debug drawing showing the current state of the triangulation</summary>
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
      for (int i = 1; i < mTN; i++) {
         ref Tile t = ref mT[i];
         Point2 pos = new (0.25.Along (t.LMin, t.RMin), t.YMin);
         string text = $"{t.Id}"; if (t.Hole) text += "*";
         if (t.VTop > 0) text += $" T{t.VTop}";
         if (t.VBot > 0) text += $" B{t.VBot}";
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, pos, size, 0, 0, 1, ETextAlign.BotCenter));
      }
      for (int i = 1; i < mVN - 4; i++) {
         ref Vertex v = ref mV[i];
         string text = $"{v.Kind.ToString ()[0]}{v.Id} T:{v.Tile[0]},{v.Tile[1]}";
         text = v.Id.ToString ();
         var align = v.Kind switch { EVertex.Mountain => ETextAlign.BotCenter, EVertex.Valley => ETextAlign.TopCenter, _ => ETextAlign.MidLeft };
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, v.Pt, size, 0, 0, 1, align));
      }

      dwg.Add (new Layer2 ("LINKS", Color4.Blue, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      for (int i = 1; i < mTN; i++) {
         ref Tile t = ref mT[i];
         for (int j = 0; j < 2; j++) {
            if (t.Top[j] > 0) AddArrow (GetCommon (ref mT[t.Top[j]], ref t), true, size);
            if (t.Bot[j] > 0) AddArrow (GetCommon (ref t, ref mT[t.Bot[j]]), false, size);
         }
      }
      return dwg;

      // Helpers ...........................................
      void AddArrow (Point2 p, bool up, double size) {
         if (p.IsNil) return;
         double d = size * 0.5;
         Poly poly = Poly.Lines (Point2.List (0, -size, 0, size, d / 2, size - d, -d / 2, size - d, 0, size), false);
         if (!up) poly *= Matrix2.VMirror;
         poly *= Matrix2.Translation (p.X, p.Y);
         dwg.Add (poly);
      }

      Point2 GetCommon (ref Tile t0, ref Tile t1) {
         Check (t0.YMin.EQ (t1.YMax));
         double x0 = mS[t0.Left].GetX (t0.YMin), x1 = mS[t0.Right].GetX (t0.YMin);
         Bound1 b0 = new (x0, x1);
         x0 = mS[t1.Left].GetX (t0.YMin); x1 = mS[t1.Right].GetX (t0.YMin);
         Bound1 b1 = new (x0, x1);
         double x = (b0 * b1).Mid;
         return new (x, t0.YMin);
      }
   }

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
   }

   // Implementation -----------------------------------------------------------
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
      // To insert top and bottom points, we could need 2 new tiles (and 2 new nodes)
      Grow (ref mT, mTN, 2); Grow (ref mN, mNN, 2);
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
         // For a regular vertex, we store the tile below in Tile[0] and 
         // the one above in Tile[1]. One of these we will return right now (based on vOther), 
         // and the other will get returned on a later call to InsertVertex (and we know it will
         // be the other one, since the two neighbors will be on opposite sides of the horizontal 
         // line at v.Pt.Y). 
         case EVertex.Regular: v.Tile[0] = t0.Id; v.Tile[1] = t1.Id; break;
         // For a mountain or valley vertex, we store only the tile below (for mountain), or the
         // tile above (for valley) and return that. 
         case EVertex.Mountain: v.Tile[0] = t0.Id; break;
         default: v.Tile[0] = t1.Id; break;
      }

      // Update connections: t0 is on the bottom, t1 at the top
      // So now t1 connects to whatever used to be on t0's top
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
      for (int i = 0; i < n; i++) mLayers[i].Connect (ref tBase, i == n - 1); 
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

   const double FINE = 1e-9;
   const bool Verify = true;
}
