// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Unfolder.cs
// ║║║║╬║╔╣║ Implements an Unfolder for sheet metal models
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Unfolder -----------------------------------------------------------------------------
/// <summary>Unfolds a sheet metal model into a drawing</summary>
public class Unfolder {
   // Constructor --------------------------------------------------------------
   /// <summary>Initializes an unfolder with a sheet metal model</summary>
   public Unfolder (Model3 model) => mModel = model;
   readonly Model3 mModel;

   // Methods ------------------------------------------------------------------
   /// <summary>Unfolds the model into a drawing (or returns an EResult if there are any problems)</summary>
   public Result<Dwg2, EResult> Process () {
      BendPose pose = new (mModel);
      if (!pose.Valid) return EResult.NotSheetMetalModel;

      // Set up the bend-pose to pose the model full flat. However, even in this flat configuration,
      // the flattened model is not necessarily aligned with the XY plane (which is what we need). So
      // compute a root transform to rotate this flattened model and align it with the XY plane. 
      pose.SetLie (0);
      Dwg2 dwg = new ();
      var plane = (E3Flat)pose.Nodes.First ().Ent;
      var xfmRoot = Matrix3.From (plane.CS);
      var b = pose.GetBound (onlyPlanes:true) * xfmRoot;
      if (b.Y.Length > b.X.Length + Lib.Delta) xfmRoot *= Matrix3.Rotation (EAxis.Z, Lib.HalfPI);
      xfmRoot *= new Matrix3 (1, 0, 0, 0, -1, 0, 0, 0, 1, 0, 0, 0);

      // Now we can walk through each of the E3Flat and E3Flex entities, and project them onto the
      // XY plane. All the 'overlap' lines between adjacent E3Flat and E3Flex have already been 
      // tagged with the IsOverlap bit, so we can simply omit them. Then, we will have a collection
      // of fragments that will neatly stitch together into closed Polys with a DwgStitcher operation
      List<double> ends = []; double yMid = 0; 
      Span<Point2> buffer = stackalloc Point2[2];
      foreach (var node in pose.Nodes) {
         ends.Clear (); 
         E3Thick ent = node.Ent;
         var xfm = ent.ToXfm * node.Xfm * xfmRoot;
         foreach (var shape in ent.Shape) {
            AddPoly (dwg, shape, xfm);
            if (ent is E3Flex flex) {
               // If this is a flex, compute the bend-line and add it. There may be multiple 
               // trimming curves for the Flex; some of these might be holes, but some of them could
               // also be a Flex with two or more hard-bound bends. So we find all intersections of
               // the 'center-line' with these trimming curves and add them into 'ends'
               yMid = flex.Spine.FlatWidth / 2;
               Point2 pa = new (0, yMid), pb = new (100, yMid); 
               foreach (var seg in shape.Segs) 
                  foreach (var pt in seg.Intersect (pa, pb, buffer, true)) ends.Add (pt.X);
            }
         }
         if (ent is E3Flex flex1) { 
            // Sort the bend-line endpoints so they appear in order and remove duplicates (possible to
            // get when the trimming curves have nodes exactly at the centerline). Then, we can make an
            // E2Bendline with these points
            ends.Sort ();
            var spine = flex1.Spine;
            for (int i = ends.Count - 1; i > 1; i--)
               if (ends[i - 1].EQ (ends[i])) ends.RemoveAt (i);
            var pts = ends.Select (x => new Point2 (x, yMid));
            dwg.Ents.Add (new E2Bendline (dwg, pts.Select (a => (Point2)(a * xfm)), spine.Angle * (spine.Upward ? -1 : 1), spine.Radius, spine.KFactor, flex1.Thickness));
         }
      }

      // Finally, run a DwgStitcher to stitch together all the open poly
      new DwgStitcher (dwg).Process ();
      return dwg;
   }

   // Implementation -----------------------------------------------------------
   // Adds a Poly into the drawing after projecting it into XY space with the given Xfm.
   // If the Poly has no overlaps 
   void AddPoly (Dwg2 dwg, Poly poly, Matrix3 xfm) {
      if (!poly.HasOverlaps) dwg.Add (poly * xfm);
      else {
         foreach (var seg in poly.Segs) {
            if (seg.IsOverlap) continue;
            dwg.Add (seg.ToPoly () * xfm);
         }
      }
   }
}
#endregion
