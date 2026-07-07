// ────── ╔╗
// ╔═╦╦═╦╦╬╣ PolyBuildEx.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

// Eventually, PolyBuildEx will replace PolyBuilder. 
// The key difference is:
// - PolyBuilder uses Line() to indicate a line _starting_ at a given point
// - PolyBuild uses Line() to indicate a line _ending_ at a given point
// More documentation will follow
public class PolyBuild {
   public PolyBuild Begin (Point2 pt, bool cleanup = false) {
      if (mBegan) Fatal ("Multiple Begin");
      mBegan = true; mCleanup = cleanup; mFlags = 0;
      mPts.Clear (); mPts.Add (pt); mExtra.Clear (); 
      return this;
   }

   public PolyBuild Line (Point2 end) {
      CheckBegan ();
      if (mCleanup) {
         int n = mPts.Count;
         Point2 prev = mPts[n - 1];
         // Check if the point we're trying to add is a duplicate
         if (end.EQ (prev)) return this;
         if (n >= 2) { 
            Point2 start = mPts[n - 2], snap = prev.SnappedToLine (start, end);
            if (snap.DistToSq (prev) < Lib.EpsilonSq) {
               double lie = snap.GetLieOn (start, end);
               if (lie is >= -Lib.Epsilon and <= (1 + Lib.Epsilon)) {
                  if (mExtra.Count <= n - 2 || mExtra[n - 2].CanMerge) 
                     mPts.RemoveLast (); 
               }
            }
         }
      }
      mPts.Add (end); 
      return this;
   }

   public PolyBuild Arc (Point2 end, Point2 center, bool ccw) {
      CheckBegan ();
      if (mCleanup && end.EQ (mPts[^1])) return this; 
      while (mExtra.Count < mPts.Count - 1) mExtra.Add (default);
      mPts.Add (end); mExtra.Add (new Poly.ArcInfo (center, ccw ? Poly.EFlags.CCW : Poly.EFlags.CW));
      mFlags |= Poly.EFlags.HasArcs;
      return this; 
   }

   public Poly End (bool close) {
      CheckBegan ();
      if (mPts.Count < 2) Fatal ("Too few points");
      if (close) {
         if (mPts[0].EQ (mPts[^1])) mPts.RemoveLast ();
         if (mPts.Count < 2) Fatal ("Too few points");
         mFlags |= Poly.EFlags.Closed;
      }
      mBegan = false;
      return new Poly ([.. mPts], [.. mExtra], mFlags);
   }

   public PolyBuild TagLastOverlap () {
      CheckBegan ();
      if (mPts.Count < 2) Fatal ("Too few points");
      while (mExtra.Count < mPts.Count - 1) mExtra.Add (default);
      Lib.Check (mExtra.Count == mPts.Count - 1);  // REMOVETHIS
      var last = mExtra[^1];
      mExtra[^1] = new Poly.ArcInfo (last.Center, last.Flags | Poly.EFlags.Overlap);
      mFlags |= Poly.EFlags.HasOverlaps;
      return this; 
   }

   void CheckBegan () {
      if (!mBegan) Fatal ("Expected Begin(Point2)");
   }

   [DoesNotReturn]
   void Fatal (string s) => throw new InvalidOperationException (s);

   readonly List<Point2> mPts = [];
   readonly List<Poly.ArcInfo> mExtra = [];
   Poly.EFlags mFlags;
   bool mBegan, mCleanup;
}
