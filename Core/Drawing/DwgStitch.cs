// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgStitch.cs
// ║║║║╬║╔╣║ Implements the DwgStitcher class (used to connect 
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DwgStitcher --------------------------------------------------------------------------
public class DwgStitcher {
   public DwgStitcher (Dwg2 dwg, double threshold = 1e-3) {
      mDwg = dwg;
      mEnds = new (new PointComparer (mThreshold = threshold));
   }

   public void Process () {
      // Get the list of open polylines, sorted by layer, and move all the other
      // entities into the 'done' list (we don't need to process them at all
      List<E2Poly> ents = [];
      foreach (var ent in mDwg.Ents.OrderBy (a => a.Layer.Name)) {
         if (ent is E2Poly e2p) {
            if (e2p.Poly.TryCleanup (out var tmp)) {
               if (tmp.Count < 1) continue; // Skip "empty" Poly!
               e2p = e2p.With (tmp); mStitched = true;
            }
            if (e2p.Poly.IsOpen) ents.Add (e2p);
            else mDone.Add (ent);
         } else
            mDone.Add (ent);
      }

      Layer2? layer = null;
      foreach (var ent in ents) {
         if (ent.Layer != layer) { AddRemaining (); layer = ent.Layer; }

         // If the poly can already be closed here, just close it and continue
         Poly poly = ent.Poly;
         if (TryClose (ent, poly)) { mStitched = true; continue; }

         // 'final' is this candidate poly with possibly other open fragments attached
         // to either of its ends (if nothing gets attached to it, then final will just
         // be the same as poly).
         Poly final = poly;
         for (int end = 0; end < 2; end++) {
            // Pick each endpoint of 'final' and see if there is an (already seen) fragment
            // that can attach to it
            var pt = end == 0 ? poly.A : poly.B;
            if (mEnds.TryGetValue (pt, out E2Poly? other)) {
               if (final.TryAppend (other.Poly, out var tmp, mThreshold)) {
                  // If so, remove this endpoint from the list of free-floating ends, and
                  // if the newly joined result is now self-closing, we are done.
                  RemoveEnds (other);
                  mStitched = true;
                  if (TryClose (ent, final = tmp.Clean ())) goto Done;
               } else
                  throw new Exception ("DwgStitcher: Coding error");
            }
         }

         // If we reach this point, we couldn't self-close ent ent we started with.
         // There are 2 sub cases:
         if (final == poly) AddEnds (ent);   // A: We couldn't add any fragment to this ent at all
         else if (final.IsOpen) AddEnds (ent.With (final));
         Done: { }
      }
      // Add the remaining entities (unclosed ones)
      AddRemaining ();
      if (!mStitched) return;
      mDwg.Ents.Clear (); mDwg.Add (mDone);
   }
   bool mStitched = false;

   // Implementation -----------------------------------------------------------
   void AddEnds (E2Poly ent) {
      mEnds[ent.Poly.A] = ent; mEnds[ent.Poly.B] = ent;
   }

   void AddRemaining () { // Cleanup current pass in preparation for the next pass/layer
      List<E2Poly?> set = [.. mEnds.Values.Distinct ()];
      for (int i = 1; i < set.Count; i++) {
         var pi = set[i]; if (pi == null) continue;
         for (int j = 0; j < i; j++) {
            var pj = set[j]; if (pj == null) continue;
            if (pi.Poly.TryAppend (pj.Poly, out var pres, mThreshold)) {
               set[i] = set[j] = null;
               if (pres.A.EQ (pres.B, mThreshold))
                  mDone.Add (pi.With (pres.Close (mThreshold).Clean ()));
               else
                  set.Add (pi.With (pres));
               mStitched = true;
               break;
            }
         }
      }
      mDone.AddRange (set.NonNull ());
      mEnds.Clear ();
   }

   void RemoveEnds (E2Poly ent) {
      mEnds.Remove (ent.Poly.A); mEnds.Remove (ent.Poly.B);
   }

   bool TryClose (E2Poly template, Poly poly) {
      if (poly.A.EQ (poly.B, mThreshold)) {
         mDone.Add (template.With (poly.Close (mThreshold).Clean ()));
         return true;
      }
      return false;
   }

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;
   readonly double mThreshold;
   readonly Dictionary<Point2, E2Poly> mEnds;
   readonly List<Ent2> mDone = [];
}
#endregion

#region class PointComparer ------------------------------------------------------------------------
class PointComparer (double threshold) : IEqualityComparer<Point2> {
   public bool Equals (Point2 a, Point2 b)
      => a.X.Round (threshold) == b.X.Round (threshold)
      && a.Y.Round (threshold) == b.Y.Round (threshold);

   public int GetHashCode (Point2 a)
      => HashCode.Combine (a.X.Round (threshold), a.Y.Round (threshold));

   public static readonly PointComparer Epsilon = new (1e-6);
}
#endregion
