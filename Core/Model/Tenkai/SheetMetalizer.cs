// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SheetMetalizer.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public partial class SheetMetalizer {
   const double ETHICK = 0.01;
   const double EAREA = 0.1;
   const double ECOS = 1e-5;

   public SheetMetalizer (Model3 model) { mModel = model; mShModel = new (); }

   public Model3 Process () {
      mModel.Ents.ForEach (a => a.IsTranslucent = true);
      if (ComputeBaseplane () is not TFlat tpBase) return mShModel;

      Queue<TFlex> todo2 = [];
      Queue<TFlat> todo1 = []; todo1.Enqueue (tpBase);
      while (todo1.Count + todo2.Count > 0) {
         while (todo1.TryDequeue (out TFlat? tp)) {
            tp.GatherNeighbors (todo2);
            mShModel.Ents.Add (tp.GetFlat ());
         }
         while (todo2.TryDequeue (out TFlex? tf)) {
            tf.GatherNeighbors (todo1);
            if (tf.GetFlex () is { } flex) mShModel.Ents.Add (flex);
         }
      }
      return mShModel;
   }

   void MarkOverlaps (E3Surface s1, E3Surface s2) {
      if (mModel.GetSharedEdge (s1, s2) is not Line3 line1) {
         Lib.Trace ("Strange 1");
         return;
      }
      line1.IsOverlap = true;
      if (mModel.GetCoedge (line1, out _, out var line2))
         line2.IsOverlap = true;
      else
         Lib.Trace ("Strange 2");
   }

   TFlat? ComputeBaseplane () {
      E3Plane? pair = null;
      double minDist = double.MaxValue;
      var planes = mModel.Ents.OfType<E3Plane> ().OrderByDescending (a => a.OuterArea).ToList ();

      E3Plane plane0 = planes[0];
      var (area0, pdef0) = (plane0.OuterArea, new PlaneDef (plane0.CS));
      for (int i = 1; i < planes.Count; i++) {
         E3Plane plane1 = planes[i]; double area1 = plane1.OuterArea;
         if (!area0.EQ (area1, EAREA)) break;
         double cos = plane0.CS.VecZ.CosineToAlreadyNormalized (plane1.CS.VecZ);
         if (!cos.EQ (-1, ECOS)) continue;

         // We found a parallel plane, see if this could be a pair for this plane
         double dist = pdef0.Dist (plane1.CS.Org);
         if (dist < ETHICK) continue;       // This is coplanar, cannot be the pair
         if (dist.EQ (minDist, ETHICK)) 
            pair = PickBetterPair (plane0, pair!, plane1);
         else 
            if (dist < minDist) (pair, minDist) = (plane1, dist);
      }
      if (pair == null) return null;
      Thickness = minDist;

      // Between plane0 and plane1, pick the one that is further away from the
      // center point of the model as the 'base' plane
      var pdef1 = new PlaneDef (pair.CS);
      Point3 center = mModel.Bound.Midpoint;
      if (pdef1.Dist (center) > pdef0.Dist (center))
         (plane0, pair, pdef0, pdef1) = (pair, plane0, pdef1, pdef0);
      return new (this, null, plane0, pdef0, pair);
   }

   E3Plane PickBetterPair (E3Plane plane0, E3Plane planeA, E3Plane planeB) {
      throw new NotImplementedException (); 
      Poly p0 = plane0.Contours[0].Flatten (plane0.FromXfm);
      Bound2 b0 = p0.GetBound ();
      Poly pa = planeA.Contours[0].Flatten (plane0.FromXfm), pb = planeB.Contours[0].Flatten (plane0.FromXfm);
      Bound2 ba = b0 + pa.GetBound (), bb = b0 + pb.GetBound ();
      return ba.Area < bb.Area ? planeA : planeB;
   }

   // Private data -------------------------------------------------------------
   readonly Model3 mModel;             // The input surface model
   readonly Model3 mShModel;           // The output sheet-metal model
   HashSet<E3CSSurface> mUsed = [];
   double Thickness;
}

// Helper class for ModelThickener - represents a 'thick plane' (an E3Plane
// with its pair)
public partial class SheetMetalizer {
}
