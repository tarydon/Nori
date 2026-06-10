// ────── ╔╗
// ╔═╦╦═╦╦╬╣ PolyBuildEx.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class PolyBuild2 {
   public PolyBuild2 Begin (Point2 pt) {
      if (mBegan) Fatal ("Multiple Begin");
      mBegan = true; mFlags = 0;
      mPts.Clear (); mPts.Add (pt); mExtra.Clear (); 
      return this;
   }

   public PolyBuild2 Line (Point2 end) {
      CheckBegan ();
      mPts.Add (end); 
      return this; 
   }

   public PolyBuild2 Arc (Point2 end, Point2 center, bool ccw) {
      CheckBegan ();
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

   public PolyBuild2 TagLastOverlap () {
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
   bool mBegan;
}
