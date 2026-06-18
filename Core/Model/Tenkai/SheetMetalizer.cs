// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SheetMetalizer.cs
// ║║║║╬║╔╣║ SheetMetalizer : converts surface models into sheet-metal models
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class SheetMetalizer -----------------------------------------------------------------------
/// <summary>Converts a surface-model into a sheet-metal model that can be unfolded</summary>
/// This uses heuristics to convert a surface model to a sheet-metal model. The surface model
/// should be built with connectivity information, and should be a well-formed sheet metal model.
/// We support only models where the sheet thickness is uniform across the model (we cannot have
/// flanges or bends where the sheet-metal thickness is different than the one at the baseplane). 
public partial class SheetMetalizer {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a SheetMetalizer given a surface model to work with</summary>
   public SheetMetalizer (Model3 model) { Model = model; ShModel = new (); }

   // Properties ---------------------------------------------------------------
   /// <summary>Converts the surface model to a sheet-metal model</summary>
   public Result<Model3, EResult> Process () {
      Model.Ents.ForEach (a => a.IsTranslucent = true);
      if (ComputeBaseplane () is not SMFlatData tpBase) return EResult.CantFindBaseplane;
      if (Thickness is < 0.0999999 or > 25.4000001) return EResult.InvalidThickness;

      Queue<SMFlexData> todo2 = [];
      Queue<SMFlatData> todo1 = []; todo1.Enqueue (tpBase);
      while (todo1.Count + todo2.Count > 0) {
         while (todo1.TryDequeue (out SMFlatData? tp)) {
            tp.GatherNeighbors (todo2);
            ShModel.Ents.Add (tp.GetFlat ());
         }
         while (todo2.TryDequeue (out SMFlexData? tf)) {
            tf.GatherNeighbors (todo1);
            if (tf.GetFlex () is { } flex) {
               ShModel.Ents.Add (flex);
               ShModel.Ents.Add (new E3Marker (flex.CS, E3Marker.EKind.CS, 10));
            }
         }
      }
      return ShModel;
   }

   // Implementation -------------------------------------------------------------------------------
   // Helper used to mark the overlap-line between two surfaces with the 'IsOverlap' bit.
   // Note that the line-of-overlap is actually a pair of co-edges (one from each of the two adjacent
   // surfaces, we need to mark both of them as IsOverlap)
   internal void MarkOverlaps (E3Surface s1, E3Surface s2) {
      if (Model.GetSharedEdge (s1, s2) is not Line3 line1) { Lib.Suspicious (); return; }
      if (!Model.GetCoedge (line1, out _, out var line2)) { Lib.Suspicious (); return; }
      line1.IsOverlap = line2.IsOverlap = true;
   }

   // Starting step of the sheet-metalization - we pick a suitable baseplane. 
   // The 'baseplane' is a potential E3Flat that we will build from a pair of planes that are similar,
   // parallel and one sheet-metal thickness apart. Here are the various checks we need to do here.
   // 
   // 1. Pick the largest plane by area, we assume this is one face (front/back) of the final sheet-
   //    metal baseplane (E3Flat) we are going to make (we call this "plane0"). 
   // 2. Pick the other parallel plane of similar area, and that will be our 'pair' to create the 
   //    E3Flat. The following additional checks are needed since we may find multiple candidates
   //    for this pair (we call that "pair")
   // 3. Multiple candidates may emerge for pair, all parallel to plane0 and all with the same area. 
   // 4. First step: thickness elimation - we measure the parallel distance between plane0 and pair
   //    (the sheet metal thickness). If this is zero, we can't use this. Otherwise, we pick the one
   //    that is minimal (to avoid selecting two matching planes from two opposite sides of a box)
   // 5. There may be ties even with the step 4 filtering. To break this, find the pair that has the
   //    same 'projection' as viewed from their common shared normal. This is to avoid selecting the
   //    bottom from one flange and the top from another parallel flange on the other side
   // If all these checks pass, we measure and store the thickness.
   SMFlatData? ComputeBaseplane () {
      E3Plane? pair = null;
      double minDist = double.MaxValue;
      var planes = Model.Ents.OfType<E3Plane> ().OrderByDescending (a => a.OuterArea).ToList ();

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
      Point3 center = Model.Bound.Midpoint;
      if (pdef1.Dist (center) > pdef0.Dist (center))
         (plane0, pair, pdef0, pdef1) = (pair, plane0, pdef1, pdef0);
      return new (this, null, plane0, pdef0, pair);
   }

   // Helper used in the ComputeBasePlane routine above to implement the step 5 of the 
   // filtering for 'pair'. This is also used by some of the assistance classes like TFlat. 
   internal E3Plane PickBetterPair (E3Plane plane0, E3Plane planeA, E3Plane planeB) {
      Poly p0 = plane0.Contours[0].Flatten (plane0.FromXfm);
      Bound2 b0 = p0.GetBound ();
      Poly pa = planeA.Contours[0].Flatten (plane0.FromXfm), pb = planeB.Contours[0].Flatten (plane0.FromXfm);
      Bound2 ba = b0 + pa.GetBound (), bb = b0 + pb.GetBound ();
      return ba.Area < bb.Area ? planeA : planeB;
   }

   // Private data -------------------------------------------------------------
   internal HashSet<E3CSSurface> Used = [];
   internal static readonly double ETHICK = 0.01;
   internal static readonly double EAREA = 0.1;
   internal static readonly double ECOS = 1e-5;
   internal static readonly double EDIST = 0.0001;
   internal readonly Model3 Model;             // The input surface model
   internal readonly Model3 ShModel;           // The output sheet-metal model
   internal double Thickness;
}
#endregion
