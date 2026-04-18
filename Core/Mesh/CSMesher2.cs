// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CSMesher2.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class CSMesher2 {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a CSMesher given the front and side views</summary>
   public CSMesher2 (IEnumerable<Poly> front, IEnumerable<Poly> side) {
      mFront.AddRange (front); mSide.AddRange (side);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Tessellation accuracy</summary>
   public ETess Tess = ETess.Medium;

   // Methods ------------------------------------------------------------------
   /// <summary>Builds the mesh</summary>
   public IEnumerable<string> IncBuild () {
      mFront.ForEach (a => AddSegs (a, true));
      mSide.ForEach (a => AddSegs (a, false));
      AddEvents ();     

      for (int i = 0; i < mNEvent; i++) {
         int n = mEvent[i].N;
         if (n > 0) {
            // Adding a new segment into the active list
            mActive.Add (n);
         } else {
            BuildDebugDwg (-n, true);
            yield return $"Leave: {-n} {mSeg[-n]}";
            ref CSeg seg = ref mSeg[-n];
            if (seg.IsHorizontal) ProcessHorzSeg (ref seg);
            else ProcessSeg (ref seg);
            BuildDebugMesh ();
            yield return $"Triangles: {mMesh.Triangle.Length / 3}";
            bool ok = mActive.Remove (-n); Lib.Check (ok, "Invalid event sorting");
         }
      }
   }
   List<int> mActive = [];
   List<Point3> mPts = [];

   // Implementation -----------------------------------------------------------
   // Add events for each entry and exit of the segment
   void AddEvents () {
      int cEv = 2 * (mNSeg - 1); 
      Lib.Grow (ref mEvent, 0, cEv);
      for (int i = 1; i < mNSeg; i++) {
         ref CSeg seg = ref mSeg[i];
         mEvent[mNEvent++] = new (i, seg.A.Y - Lib.Epsilon);
         mEvent[mNEvent++] = new (-i, seg.B.Y + Lib.Epsilon);
      }
      mEvent.AsSpan (0, cEv).Sort ();
   }

   // Add the segs related to a particular Poly
   void AddSegs (Poly poly, bool front) {
      mTmp.ClearFast ();
      poly.Discretize (mTmp, Tess);
      for (int i = 0; i < mTmp.Count; i++) mTmp[i] = mTmp[i].R3 ();
      Lib.Grow (ref mSeg, mNSeg, mTmp.Count); mTmp.Add (mTmp[0]); // Make mTmp circular
      for (int i = 0; i < mTmp.Count - 1; i++)
         mSeg[mNSeg++] = new CSeg (mTmp[i], mTmp[i + 1], front);
   }

   Dwg2 BuildDebugDwg (int nCurrent, bool leave) {
      mDwg = new ();
      var _ = mDwg.CurrentLayer;
      mDwg.Add (new Layer2 ("Alt", Color4.Red, ELineType.Continuous));
      foreach (var na in mActive) {
         ref CSeg seg = ref mSeg[na];
         Point2 pa = seg.A, pb = seg.B;
         if (na == nCurrent) mDwg.CurrentLayer = mDwg.Layers[1];
         else mDwg.CurrentLayer = mDwg.Layers[0];
         mDwg.Add (Poly.Line (pa, pb));
      }

      if (leave) {
         ref CSeg seg0 = ref mSeg[nCurrent];
         Bound2 b = mDwg.Bound.InflatedF (1.1);
         mDwg.Add (new Layer2 ("Dotted", Color4.Blue, ELineType.Dot));
         mDwg.CurrentLayer = mDwg.Layers[^1];
         mDwg.Add (Poly.Line (b.X.Min, seg0.A.Y, b.X.Max, seg0.A.Y));
         mDwg.Add (Poly.Line (b.X.Min, seg0.B.Y, b.X.Max, seg0.B.Y));
      }
      return mDwg;
   }

   Mesh3 BuildDebugMesh () {
      if (mPts.Count > 0) mMesh = new Mesh3Builder (mPts.AsSpan ()).Build ();
      return mMesh;
   }

   public (Dwg2, Mesh3) GetIncremental () => (mDwg, mMesh);
   Mesh3 mMesh = Mesh3.Extrude ([Poly.Rectangle (0, 0, 10, 10)], 10, Matrix3.Identity, ETess.Medium);
   Dwg2 mDwg = new ();

   void ProcessSeg (ref CSeg seg) {
      // We're going to process the Y-span occupied by this given segment
      double y0 = Math.Max (mZH, seg.A.Y), y1 = Math.Max (mZH, seg.B.Y);
      y0 += Lib.Epsilon; y1 -= Lib.Epsilon;     // Slightly tighten the bound to avoid one-point overlaps
      if (y1 <= y0) return; 

      // Then, take the range of active segments and split it into vertical zones based
      // on every endpoint-Y we find (since we've discretized all the coordinates to the nearest
      // 0.001, we can safely use doubles in a hashset to find unique endpoints
      mYUniqueSet.Clear (); 
      foreach (var n in mActive) {
         ref CSeg s = ref mSeg[n];
         if (s.IsHorizontal || s.A.Y > y1 || s.B.Y < y0) continue;
         mYUniqueSet.Add (s.A.Y); mYUniqueSet.Add (s.B.Y);
      }
      mYUniqueList.Clear (); mYUniqueList.AddRange (mYUniqueSet.Order ());

      // Now process each vertical span separately
      mZH = mYUniqueList[0];
      for (int k = 1; k < mYUniqueList.Count; k++) {
         mFN.Clear (); mSN.Clear ();
         if (mYUniqueList[k] > seg.B.Y + Lib.Epsilon) continue; 

         mZL = mZH; mZH = mYUniqueList[k]; mZMid = (mZL + mZH) / 2;
         y0 = mZL + Lib.Epsilon; y1 = mZH - Lib.Epsilon;
         foreach (var n in mActive) {
            ref CSeg s = ref mSeg[n];
            if (s.IsHorizontal || s.A.Y > y1 || s.B.Y < y0) continue;
            if (s.Front) mFN.Add (n); else mSN.Add (n);
         }
         mFN.Sort (CompareSegByX); mSN.Sort (CompareSegByX);

         // Take each pair of front segments, and each pair of side segments and try to 
         // process them. We want to find one segment that is 'left' (Reverse=true), and
         // one that is 'right' (Reverse=false) in successive positions. 
         for (int i = 1; i < mFN.Count; i++) {
            ref CSeg sf0 = ref mSeg[mFN[i - 1]], sf1 = ref mSeg[mFN[i]];
            if (!sf0.Reverse || sf1.Reverse) continue;
            for (int j = 1; j < mSN.Count; j++) {
               ref CSeg ss0 = ref mSeg[mSN[j - 1]], ss1 = ref mSeg[mSN[j]];
               if (!ss0.Reverse || ss1.Reverse) continue;
               // Get the points at the L(ow) and H(igh) positions
               double x0L = sf0.GetX (mZL), x1L = sf1.GetX (mZL);
               double y0L = ss0.GetX (mZL), y1L = ss1.GetX (mZL);
               double x0H = sf0.GetX (mZH), x1H = sf1.GetX (mZH);
               double y0H = ss0.GetX (mZH), y1H = ss1.GetX (mZH);
               Add (x0L, x1L, y0L, y0L, x0H, x1H, y0H, y0H);
               Add (x1L, x0L, y1L, y1L, x1H, x0H, y1H, y1H);
               Add (x0L, x0L, y1L, y0L, x0H, x0H, y1H, y0H);
               Add (x1L, x1L, y0L, y1L, x1H, x1H, y0H, y1H);
            }
         }
      }

      // Helper ............................................
      void Add (double xaL, double xbL, double yaL, double ybL, double xaH, double xbH, double yaH, double ybH) {
         Point3 p1 = new (xaL, yaL, mZL), p2 = new (xbL, ybL, mZL);
         Point3 p3 = new (xaH, yaH, mZH), p4 = new (xbH, ybH, mZH);
         mPts.Add (p1); mPts.Add (p2); mPts.Add (p4);
         mPts.Add (p1); mPts.Add (p4); mPts.Add (p3);
      }
   }
   List<double> mYUniqueList = [];
   HashSet<double> mYUniqueSet = [];
   List<int> mFN = [], mSN = [], mSubActive = [];
   double mZL, mZH = double.MinValue, mZMid;

   int CompareSegByX (int a, int b) {
      ref CSeg sa = ref mSeg[a], sb = ref mSeg[b];
      return sa.GetX (mZMid).CompareTo (sb.GetX (mZMid));
   }

   void ProcessHorzSeg (ref CSeg sa) {
      foreach (var n in mActive) {
         ref CSeg sb = ref mSeg[n];
         if (sb.Front == sa.Front || !sb.IsHorizontal) continue;
         double z = sa.A.Y; if (!z.EQ (sb.A.Y, 0.001)) continue;
         var (x0, x1) = sa.Reverse ? (sa.B.X, sa.A.X) : (sa.A.X, sa.B.X);
         // var (y0, y1) = sb.Reverse ? (sb.B.X, sb.A.X) : (sb.A.X, sb.B.X);
         var (y0, y1) = (sb.B.X, sb.A.X);
         if (sa.Front) Add (x0, x1, y0, y1, z);
         else Add (y0, y1, x0, x1, z);
      }

      // Helper ............................................
      void Add (double x0, double x1, double y0, double y1, double z) {
         mPts.Add (new (x0, y0, z)); mPts.Add (new (x1, y0, z)); mPts.Add (new (x1, y1, z));
         mPts.Add (new (x0, y0, z)); mPts.Add (new (x1, y1, z)); mPts.Add (new (x0, y1, z));
      }
   }

   // Nested types -------------------------------------------------------------
   // Represents a segment from the front or side views
   readonly struct CSeg {
      public CSeg (Point2 a, Point2 b, bool front) {
         bool flip = (a.Y == b.Y) ? a.X > b.X : a.Y > b.Y;
         (A, B) = flip ? (b, a) : (a, b);
         (Reverse, Front) = (flip, front);
      }

      public readonly Point2 A;        // Bottom point of segment
      public readonly Point2 B;        // Top point of segment
      public readonly bool Front;      // Is this from the front view (false = side view)
      public readonly bool Reverse;    // Does this segment go in reverse (to original Poly seg)
      public bool IsHorizontal => A.Y == B.Y;

      public double GetX (double y) => ((y - A.Y) / (B.Y - A.Y)).Along (A.X, B.X);
      public override string ToString () => $"{(Reverse ? '-' : '+')}{(Front ? 'F' : 'S')} {A} ... {B}";
   }

   // Represents a segment entering or leaving the active edge list
   // +ve values of N represent the entering of segment N, -ve values of N 
   // represent the leaving of segment -N (segment 0 is not used)
   readonly struct Event (int n, double y) : IComparable<Event> {
      public readonly int N = n;
      public readonly double Y = y;

      public int CompareTo (Event other) {
         if (Y == other.Y) return other.N - N;  // Enter events before leave events
         return Y.CompareTo (other.Y);
      }
   }

   // Private data -------------------------------------------------------------
   List<Poly> mFront = [];    // Set of front Poly
   List<Poly> mSide = [];     // Set of side Poly
   List<Point2> mTmp = [];    // Temporary working set
   CSeg[] mSeg = new CSeg[8]; int mNSeg = 1;    // mSeg[0] is not used
   Event[] mEvent = []; int mNEvent;
}
