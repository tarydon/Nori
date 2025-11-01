// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgStitch.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

class DwgStitcher {
   public DwgStitcher (Dwg2 dwg, double threshold = 1e-3) {
      mDwg = dwg;
      mComp1 = new (mThreshold = threshold, 0);
      mEnds1 = new (mComp1);
   }

   public void Process () {
      // Get the list of open polylines, sorted by layer, and move all the other
      // entities into the 'done' list (we don't need to process them at all
      List<E2Poly> ents = [];
      foreach (var ent in mDwg.Ents.OrderBy (a => a.Layer.Name)) {
         if (ent is E2Poly e2p) {
            if (e2p.Poly.TryCleanup (out var tmp)) e2p = e2p.With (tmp);
            if (e2p.Poly.IsOpen) ents.Add (e2p);
            else mDone.Add (ent);
         } else
            mDone.Add (ent);
      }

      Layer2? layer = null;
      for (int i = 0; i < ents.Count; i++) {
         var ent = ents[i];
         if (ent.Layer != layer) { AddRemaining (); layer = ent.Layer; }

         // If the poly can already be closed here, just close it and continue
         Poly poly = ent.Poly;
         if (TryClose (ent, poly)) continue;

         // 'final' is this candidate poly with possibly other open fragments attached
         // to either of its ends (if nothing gets attached to it, then final will just
         // be the same as poly).
         Poly final = poly;
         for (int end = 0; end < 2; end++) {
            // Pick each endpoint of 'final' and see if there is an (already seen) fragment
            // that can attach to it
            var pt = end == 0 ? poly.A : poly.B;
            if (mEnds1.TryGetValue (pt, out E2Poly? other)) {
               if (final.TryAppend (other.Poly, out var tmp, mThreshold)) {
                  // If so, remove this endpoint from the list of free-floating ends, and
                  // if the newly joined result is now self-closing, we are done.
                  RemoveEnds (other);
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
      mDwg.Ents.Clear (); mDwg.Add (mDone);
   }

   // Implementation -----------------------------------------------------------
   void AddEnds (E2Poly ent) {
      mEnds1[ent.Poly.A] = ent; mEnds1[ent.Poly.B] = ent;
   }

   void AddRemaining () {
      List<E2Poly?> set = [.. mEnds1.Values.Distinct ()];
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
               break;
            }
         }
      }
      mDone.AddRange (set.NonNull ());
      mEnds1.Clear ();
   }

   void RemoveEnds (E2Poly ent) {
      mEnds1.Remove (ent.Poly.A); mEnds1.Remove (ent.Poly.B);
   }

   bool TryClose (E2Poly template , Poly poly) {
      if (poly.A.EQ (poly.B, mThreshold)) {
         mDone.Add (template.With (poly.Close (mThreshold).Clean ()));
         return true;
      }
      return false;
   }

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;
   readonly double mThreshold;
   readonly Dictionary<Point2, E2Poly> mEnds1;
   readonly PointComparer mComp1;
   readonly List<Ent2> mDone = [];
}

class PointComparer (double threshold, double offset) : IEqualityComparer<Point2> {
   public bool Equals (Point2 a, Point2 b)
      => (a.X + offset).Round (threshold) == (b.X + offset).Round (threshold)
      && (a.Y + offset).Round (threshold) == (b.Y + offset).Round (threshold);

   public int GetHashCode (Point2 a)
      => HashCode.Combine ((a.X + offset).Round (threshold), (a.Y + offset).Round (threshold));
}
