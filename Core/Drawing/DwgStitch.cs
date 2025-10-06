namespace Nori;

class DwgStitcher {
   public DwgStitcher (Dwg2 dwg, double threshold = 1e-3) {
      mDwg = dwg;
      mComp = new (mThreshold = threshold);
      mEnds = new Dictionary<Point2, E2Poly> (mComp);
   }

   public void Process () {
      // Get the list of open polylines, sorted by layer, and move all the other
      // entities into the 'done' list (we don't need to process them at all)
      var ents = mDwg.Ents.OfType<E2Poly> ()
                     .Where (a => a.Poly.IsOpen)
                     .OrderBy (a => a.Layer)
                     .ToList ();
      mDone.AddRange (mDwg.Ents.Except (ents));

      Layer2? layer = null;
      for (int i = 0; i < ents.Count; i++) {
         var ent = ents[i];
         if (ent.Layer != layer) { layer = ent.Layer; mEnds.Clear (); }

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
            if (mEnds.TryGetValue (pt, out E2Poly? other)) {
               if (final.TryAppend (other.Poly, out var tmp, mThreshold)) {
                  // If so, remove this endpoint from the list of free-floating ends, and
                  // if the newly joined result is now self-closing, we are done.
                  mEnds.Remove (pt);
                  if (TryClose (ent, final = tmp)) break;
               } else
                  throw new Exception ("DwgStitcher: Coding error");
            }

            // If we reach this point, we couldn't self-close ent ent we started with.
            // There are 2 sub cases:
            if (final == poly) AddEnds (ent);   // A: We couldn't add any fragment to this ent at all
            else AddEnds (ent.With (final));    // B: We added some fragments to this, but it remains open
         }
      }
      // Add the remaining entities (unclosed ones)
      mDone.AddRange (mEnds.Values.Distinct ());
      mDwg.Ents.Clear (); mDwg.Add (mDone);
   }

   // Implementation -----------------------------------------------------------
   void AddEnds (E2Poly ent) {
      mEnds.Add (ent.Poly.A, ent);
      mEnds.Add (ent.Poly.B, ent);
   }

   bool TryClose (E2Poly template , Poly poly) {
      if (mComp.Equals (poly.A, poly.B)) {
         mDone.Add (template.With (poly.Closed ()));
         return true;
      }
      return false;
   }

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;
   readonly double mThreshold;
   readonly Dictionary<Point2, E2Poly> mEnds;
   readonly PointComparer mComp;
   readonly List<Ent2> mDone = [];
}

class PointComparer (double threshold) : IEqualityComparer<Point2> {
   public bool Equals (Point2 a, Point2 b)
      => a.X.Round (threshold) == b.X.Round (threshold)
      && a.Y.Round (threshold) == b.Y.Round (threshold);

   public int GetHashCode (Point2 a)
      => HashCode.Combine (a.X.Round (threshold), a.Y.Round (threshold));
}