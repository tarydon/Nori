namespace Nori;

public class CSMesher3 {
   // Constructors -------------------------------------------------------------
   public CSMesher3 (IEnumerable<Poly> front, IEnumerable<Poly> side) {
      mFront = [.. front]; mSide = [.. side];
   }
   List<Poly> mFront, mSide;

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Tessellation accuracy
   /// </summary>
   public ETess Tess = ETess.Medium;

   // Methods ------------------------------------------------------------------
   public IEnumerable<string> IncBuild () {
      Discretize ();
      for (int i = 0; i < mFront.Count; i++) AddSegs (i, true);
      for (int i = 0; i < mSide.Count; i++) AddSegs (i + mFront.Count, false);
      int[] sorted = [.. Enumerable.Range (1, mNSeg - 1)]; sorted.Sort (SegSorter);

      int slice = -1;
      for (int i = 0, max = sorted.Length - 1; i <= max; i++) {
         int n = sorted[i];
         if (mSeg[n].Slice != slice) yield return $"Slice {slice}";
         ref CSeg seg = ref mSeg[n];
         if (seg.Slice != slice) {
            Build ();
            mFN.Clear (); mSN.Clear (); slice = seg.Slice;
         }
         if (seg.Horz) continue;
         (seg.Front ? mFN : mSN).Add (n);
      }
      yield return $"Final slice";
      Build ();
      yield return "Done";
   }
   List<int> mFN = [], mSN = [];

   public (Dwg2, Mesh3) GetIncResult () {
      Dwg2 dwg = new ();
      dwg.Add (new Layer2 ("FRONT", Color4.Black, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var n in mFN) {
         ref CSeg seg = ref mSeg[n];
         dwg.Add (Poly.Line ((Point2)seg.A, (Point2)seg.B));
      }
      dwg.Add (new Layer2 ("SIDE", Color4.Red, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var n in mSN) {
         ref CSeg seg = ref mSeg[n];
         dwg.Add (Poly.Line ((Point2)seg.A, (Point2)seg.B));
      }

      Mesh3 mesh;
      if (mPts.Count == 0)
         mesh = Mesh3.Extrude ([Poly.Rectangle (0, 0, 10, 5)], 2.5, Matrix3.Identity, ETess.Coarse);
      else
         mesh = new Mesh3Builder (mPts.AsSpan ()).Build ();
      return (dwg, mesh);
   }

   // Implementation -----------------------------------------------------------
   void AddSegs (int n, bool front) {
      int a = mSplits[n], b = mSplits[n + 1] - 1;
      Point2f pb = mNodes[a]; int nyb = mYDict[pb.Y];
      for (int i = a + 1; i <= b; i++) {
         Point2f pa = pb; int nya = nyb;
         pb = mNodes[i]; nyb = mYDict[pb.Y];
         if (nyb < nya - 1) {
            Point2f prev = pa;
            Lib.Grow (ref mSeg, mNSeg, nya - nyb);
            for (int ny = nya - 1; ny >= nyb; ny--) {
               float y = mYList[ny], lie = y.GetLieOn (pa.Y, pb.Y);
               float x = lie.Along (pa.X, pb.X); Point2f pt = new (x, y);
               mSeg[mNSeg++] = new CSeg (prev, pt, front, ny + 1, ny);
               prev = pt;
            }
         } else if (nyb > nya + 1) {
            Point2f prev = pa;
            Lib.Grow (ref mSeg, mNSeg, nyb - nya);
            for (int ny = nya + 1; ny <= nyb; ny++) {
               float y = mYList[ny], lie = y.GetLieOn (pa.Y, pb.Y);
               float x = lie.Along (pa.X, pb.X); Point2f pt = new (x, y);
               mSeg[mNSeg++] = new CSeg (prev, pt, front, ny - 1, ny);
               prev = pt;
            }
         } else {
            Lib.Grow (ref mSeg, mNSeg, 1);
            mSeg[mNSeg++] = new CSeg (pa, pb, front, nya, nyb);
         }
      }
   }

   void Build () {
      Check (!mFN.Count.IsOdd ());
      double zLow = 0, zHigh = 0;
      for (int i = 0; i < mFN.Count; i += 2) {
         ref CSeg sf0 = ref mSeg[mFN[i]], sf1 = ref mSeg[mFN[i + 1]];
         zLow = sf0.A.Y; zHigh = sf0.B.Y;
         Check (sf0.Reverse && !sf1.Reverse);
         for (int j = 0; j < mSN.Count; j += 2) {
            ref CSeg ss0 = ref mSeg[mSN[j]], ss1 = ref mSeg[mSN[j + 1]];
            Check (ss0.Reverse && !ss1.Reverse);

            // Get the points at the L(ow) and H(igh) positions
            double x0L = sf0.A.X, x1L = sf1.A.X, y0L = ss0.A.X, y1L = ss1.A.X;
            double x0H = sf0.B.X, x1H = sf1.B.X, y0H = ss0.B.X, y1H = ss1.B.X;
            Add (x0L, x1L, y0L, y0L, x0H, x1H, y0H, y0H);
            Add (x1L, x0L, y1L, y1L, x1H, x0H, y1H, y1H);
            Add (x0L, x0L, y1L, y0L, x0H, x0H, y1H, y0H);
            Add (x1L, x1L, y0L, y1L, x1H, x1H, y0H, y1H);
         }
      }

      // Helper ............................................
      void Add (double xaL, double xbL, double yaL, double ybL, double xaH, double xbH, double yaH, double ybH) {
         Point3 p1 = new (xaL, yaL, zLow), p2 = new (xbL, ybL, zLow);
         Point3 p3 = new (xaH, yaH, zHigh), p4 = new (xbH, ybH, zHigh);
         mPts.Add (p1); mPts.Add (p2); mPts.Add (p4);
         mPts.Add (p1); mPts.Add (p4); mPts.Add (p3);
      }
   }
   List<Point3> mPts = [];

   void Check (bool condition) {
      if (!condition) throw new InvalidOperationException ();
   }

   void Discretize () {
      // First, find all the unique values of Y among all the discretized poly
      List<Point2> pts = [];
      HashSet<float> yUnique = [];
      foreach (var poly in mFront.Concat (mSide)) {
         int start = mNodes.Count;
         pts.ClearFast (); poly.Discretize (pts, Tess);
         foreach (var pt in pts) {
            Point2f ptf = new (pt.X.R3 (), pt.Y.R3 ()); 
            yUnique.Add (ptf.Y); mNodes.Add (ptf); 
         }
         mNodes.Add (mNodes[start]);
         mSplits.Add (mNodes.Count);
      }
      foreach (var y in yUnique.Order ()) { 
         mYDict.Add (y, mYList.Count); mYList.Add (y); 
      }
   }
   List<Point2f> mNodes = [];    // All the nodes for all the polys, discretized
   List<int> mSplits = [0];      // Splits those nodes into unique polys
   List<float> mYList = [];               // List of unique Y values
   Dictionary<float, int> mYDict = [];    // Map of Y values into unique indices

   int SegSorter (int a, int b) {
      ref CSeg sa = ref mSeg[a], sb = ref mSeg[b];
      int n = sa.Slice - sb.Slice; if (n != 0) return n;
      return sa.XMid.CompareTo (sb.XMid);
   }

   // Nested types -------------------------------------------------------------
   readonly struct CSeg {
      public CSeg (Point2f a, Point2f b, bool front, int aslice, int bslice) {
         bool flip = (a.Y == b.Y) ? a.X > b.X : a.Y > b.Y;
         (A, B, Slice) = flip ? (b, a, bslice) : (a, b, aslice);
         (Reverse, Front, XMid) = (flip, front, a.X + b.X);
      }

      public readonly Point2f A;       // Bottom point of segment
      public readonly Point2f B;       // Top point of segment
      public readonly int Slice;       // Which vertical slice does this seg belong to
      public readonly bool Front;      // Is this from the front view (false = side view)
      public readonly bool Reverse;    // Does this segment go in reverse (to original Poly seg)
      public readonly float XMid;      // X-midpoint of this seg (used for sorting in active edge list)
      public readonly bool Horz => A.Y == B.Y;

      public override string ToString () => $"{(Reverse ? '-' : '+')}{(Front ? 'F' : 'S')} {A} ... {B} | {Slice}";
   }

   // Private data -------------------------------------------------------------
   CSeg[] mSeg = new CSeg[8];    // Array of all the CSeg
   int mNSeg = 1;                // How many of those are used?
}
