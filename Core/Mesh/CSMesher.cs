// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CSMesher.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Math;

#region class CSMesher -----------------------------------------------------------------------------
/// <summary>This class is used to create a mesh from 2 cross-sections (front & side views)</summary>
public class CSMesher {
   // Constructor --------------------------------------------------------------
   public CSMesher (IEnumerable<Poly> front, IEnumerable<Poly> side) {
      mNSeg = 1; 
      AddSegs (front, true); AddSegs (side, false);

      // Now, add the events (segment enter, segment leave). +ve integers are segments
      // entering, and -ve integers are segments leaving. 
      int cEv = 2 * (mNSeg - 1); mNEv = 0; 
      Lib.Grow (ref mEvent, 0, cEv);
      for (int i = 1; i < mNSeg; i++) {
         ref CSeg seg = ref mSeg[i];
         mEvent[mNEv++] = new (i, seg.A.Y - Lib.Epsilon);
         mEvent[mNEv++] = new (-i, seg.B.Y + Lib.Epsilon);
      }
       mEvent.AsSpan (0, cEv).Sort ();      

      // Helper ............................................
      void AddSegs (IEnumerable<Poly> polys, bool fview) {
         foreach (var p0 in polys) {
            mTmp.ClearFast ();
            p0.Discretize (mTmp, ETess.Medium);
            for (int i = 0; i < mTmp.Count; i++) mTmp[i] = mTmp[i].R3 ();
            var b = new Bound2 (mTmp);
            for (int i = 1; i < mTmp.Count; i++) {
               if (mTmp[i].Y == mTmp[i - 1].Y) {
                  // Avoid horizontal segments except at top or bottom of the bound.
                  if (mTmp[i].Y.EQ (b.Y.Min) || mTmp[i].Y.EQ (b.Y.Max)) continue;
                  // In other situations move one of the two ends up by 0.1 mm. 
                  if (mTmp[i - 1].Y < mTmp[(i + 1) % mTmp.Count].Y)
                     mTmp[i] = mTmp[i].Moved (0, 0.1);
                  else
                     mTmp[i] = mTmp[i].Moved (0, -0.1);
               }
            }
            Lib.Grow (ref mSeg, mNSeg, mTmp.Count); mTmp.Add (mTmp[0]);
            for (int i = 0; i < mTmp.Count - 1; i++)
               mSeg[mNSeg++] = new CSeg (mTmp[i], mTmp[i + 1], fview);
         }
      }
   }
   List<Point2> mTmp = [];
   CSeg[] mSeg = new CSeg[8]; int mNSeg;        // mSeg[0] is not used
   Event[] mEvent = []; int mNEv;

   public IEnumerable<string> IncBuild () {
      int max = 0;
      for (int i = 0; i < mNEv; i++) {
         int n = mEvent[i].N;
         if (n > 0) {
            // Adding a new segment into the active list
            mActive.Add (n); max = Max (max, mActive.Count);
         } else {
            mDwg = new ();
            var _ = mDwg.CurrentLayer;
            mDwg.Add (new Layer2 ("Alt", Color4.Red, ELineType.Continuous));
            foreach (var na in mActive) {
               ref CSeg seg = ref mSeg[na];
               Point2 pa = seg.A, pb = seg.B;
               if (na == -n) mDwg.CurrentLayer = mDwg.Layers[1];
               else mDwg.CurrentLayer = mDwg.Layers[0];
               mDwg.Add (Poly.Line (pa, pb));
            }
            yield return $"Prep: {mSeg[-n]}";
            // Removing an existing segment from the active list
            ProcessSeg (-n);
            bool ok = mActive.Remove (-n); Lib.Check (ok, "Invalid event sorting");
            yield return $"Triangles: {Mesh.Triangle.Length / 3}";
         }
      }
   }

   public Mesh3 Mesh => new Mesh3Builder (mPts.AsSpan ()).Build ();
   public Dwg2 Dwg => mDwg;
   Dwg2 mDwg = new ();

   public Mesh3 Build () {
      int max = 0; 
      for (int i = 0; i < mNEv; i++) {
         int n = mEvent[i].N;
         if (n > 0) {
            // Adding a new segment into the active list
            mActive.Add (n); max = Max (max, mActive.Count);
         } else {
            var dwg = new Dwg2 ();     // REMOVETHIS
            var _ = dwg.CurrentLayer;
            dwg.Add (new Layer2 ("Alt", Color4.Red, ELineType.Continuous));
            foreach (var na in mActive) {
               ref CSeg seg = ref mSeg[na];
               Point2 pa = seg.A, pb = seg.B;
               if (na == -n) dwg.CurrentLayer = dwg.Layers[1];
               else dwg.CurrentLayer = dwg.Layers[0];
               dwg.Add (Poly.Line (pa, pb));
            }
            // Removing an existing segment from the active list
            ProcessSeg (-n);
            bool ok = mActive.Remove (-n); Lib.Check (ok, "Invalid event sorting");
            DXFWriter.Save (dwg, "c:/etc/test.dxf");
         }         
      }
      return new Mesh3Builder (mPts.AsSpan ()).Build ();
   }
   List<int> mActive = [];

   // Implementation -----------------------------------------------------------
   void ProcessSeg (int n) {
      // This is called just before a segment is removed from the active list, and we
      // use this time to create all the planes that this segment contributes to.
      ref CSeg seg = ref mSeg[n];
      if (seg.IsHorizontal) {
         foreach (var a in mActive) {
            ref CSeg s2 = ref mSeg[a];
            if (s2.Front == seg.Front || !s2.IsHorizontal) continue;
            if (seg.A.Y.EQ (s2.A.Y, 0.001)) AddHorizontalFace (ref seg, ref s2);
         }
         return;
      }

      // First, gather all the 'other view' segments that this intersects with, and 
      mOverlaps.Clear ();
      foreach (var a in mActive) {
         ref CSeg s2 = ref mSeg[a];
         if (s2.Front == seg.Front || !seg.YOverlap (ref s2) || seg.IsHorizontal) continue;
         mOverlaps.Add (a);
      }
      double yMax = seg.B.Y;
      if (mOverlaps.Count >= 2) {
         mOverlaps.Sort ((a, b) => mSeg[a].GetX (yMax).CompareTo (mSeg[b].GetX (yMax)));
         for (int i = 1; i < mOverlaps.Count; i++) {
            ref CSeg left = ref mSeg[mOverlaps[i - 1]]; if (!left.Reverse) continue; 
            ref CSeg right = ref mSeg[mOverlaps[i]]; if (right.Reverse) continue;
            AddFace (ref seg, ref left, ref right);
         }
      }

      // Then, for each of the 'other view' segments currently active, gather the overlaps,
      // and process the pairs that this segment participates in
      foreach (var aOther in mActive) {
         ref CSeg segOther = ref mSeg[aOther];
         if (segOther.Front == seg.Front) continue;
         mOverlaps.Clear ();
         foreach (var a in mActive) {
            ref CSeg segHere = ref mSeg[a];
            if (segHere.Front != seg.Front || segHere.IsHorizontal) continue; 
            if (segHere.YOverlap (ref seg) && segHere.YOverlap (ref segOther))
               mOverlaps.Add (a);
         }
         if (mOverlaps.Count < 2) continue; 
         mOverlaps.Sort ((a, b) => mSeg[a].GetX (yMax).CompareTo (mSeg[b].GetX (yMax)));
         for (int i = 0; i < mOverlaps.Count; i++) {
            int nref = mOverlaps[i]; if (nref != n) continue;
            ref CSeg sref = ref mSeg[nref];
            if (sref.Reverse) {
               if (i == mOverlaps.Count - 1) break;
               ref CSeg snext = ref mSeg[mOverlaps[i + 1]];
               if (!snext.Reverse) AddFace (ref segOther, ref sref, ref snext);
            } else {
               if (i == 0) break;
               ref CSeg sprev = ref mSeg[mOverlaps[i - 1]];
               if (sprev.Reverse) AddFace (ref segOther, ref sprev, ref sref);
            }
            break;
         }
      }
   }
   List<int> mOverlaps = [];

   void AddHorizontalFace (ref CSeg s1, ref CSeg s2) {
      double z = s1.A.Y;
      if (s1.Front) {
         Point3 pa = new (s1.A.X, s2.A.X, z), pb = new (s1.B.X, s2.A.X, z);
         Point3 pc = new (s1.A.X, s2.B.X, z), pd = new (s1.B.X, s2.B.X, z);
         mPts.AddM (pa, pb, pd, pa, pd, pc);
      } else {
         Point3 pa = new (s2.A.X, s1.A.X, z), pb = new (s1.A.X, s1.B.X, z);
         Point3 pc = new (s2.B.X, s1.A.X, z), pd = new (s2.B.X, s1.B.X, z);
         mPts.AddM (pa, pd, pb, pa, pc, pd);
      }
   }

   void AddFace (ref CSeg seg, ref CSeg L, ref CSeg R) {
      double z0 = Max (seg.A.Y, Max (L.A.Y, R.A.Y)), z1 = Min (seg.B.Y, Min (L.B.Y, R.B.Y));
      if (z0 >= z1 - Lib.Epsilon) return;

      double x0 = seg.GetX (z0), x1 = seg.GetX (z1);
      double yL0 = L.GetX (z0), yL1 = L.GetX (z1);
      double yR0 = R.GetX (z0), yR1 = R.GetX (z1);
      if (seg.Front) {
         Point3 pa = new (x0, yL0, z0), pb = new (x0, yR0, z0);
         Point3 pc = new (x1, yL1, z1), pd = new (x1, yR1, z1);
         if (seg.Reverse) mPts.AddM (pa, pd, pb, pa, pc, pd);
         else mPts.AddM (pa, pb, pd, pa, pd, pc);
      } else {
         Point3 pa = new (yL0, x0, z0), pb = new (yR0, x0, z0);
         Point3 pc = new (yL1, x1, z1), pd = new (yR1, x1, z1);
         if (seg.Reverse) mPts.AddM (pa, pb, pd, pa, pd, pc);
         else mPts.AddM (pa, pd, pb, pa, pc, pd);
      }
   }
   List<Point3> mPts = [];

   // Nested types -------------------------------------------------------------
   readonly struct CSeg {
      // Constructor -------------------------------------------------
      public CSeg (Point2 a, Point2 b, bool front) {
         bool flip;
         if (a.Y == b.Y) flip = a.X > b.X;
         else flip = a.Y > b.Y;
         (Reverse, Front) = (flip, front);
         (A, B) = flip ? (b, a) : (a, b);
      }

      // Properties --------------------------------------------------
      public readonly Point2 A;        // Bottom point of segment
      public readonly Point2 B;        // Top point of segment
      public readonly bool Reverse;    // Does this segment go in 'reverse' (to original Poly.Seg)
      public readonly bool Front;      // Is this from the front view

      public bool IsHorizontal => A.Y == B.Y;

      // Methods -----------------------------------------------------
      public bool YOverlap (ref CSeg other) {
         if (other.A.Y >= B.Y - Lib.Epsilon) return false;
         if (A.Y >= other.B.Y - Lib.Epsilon) return false;
         return true;
      }

      public double GetX (double y) {
         double lie = (y - A.Y) / (B.Y - A.Y);
         return lie.Along (A.X, B.X);
      }

      public override string ToString ()
         => $"{(Reverse ? '-' : '+')}{(Front ? 'F' : 'S')} {A} ... {B}";
   }

   readonly struct Event (int n, double y) : IComparable<Event> {
      public readonly int N = n;
      public readonly double Y = y;

      // Methods -----------------------------------------------------
      public int CompareTo (Event other) {
         if (Y == other.Y) return other.N - N;  // Enter events before leave events
         return Y.CompareTo (other.Y);
      }
   }
}
#endregion
