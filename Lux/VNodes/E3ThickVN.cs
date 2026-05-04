// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3ThickVN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E3ThickVN ----------------------------------------------------------------------------
/// <summary>VNode used to render E3Thick (flats / flexes)</summary>
public class E3ThickVN (E3Thick thick) : VNode (thick) {
   public override void SetAttributes () => Lux.LineWidth = 3; // REMOVETHIS
   public override void Draw () => Lux.Mesh (thick.Mesh);
}
#endregion

#region class E3MarkerVN ---------------------------------------------------------------------------
/// <summary>VNode used to render E3Marker</summary>
public class E3MarkerVN (E3Marker marker) : VNode (marker) {
   public override void SetAttributes () =>
      (Lux.Color, Lux.LineWidth) = (Color4.Yellow, 4);

   public override void Draw () {
      var (c, d) = (marker.CS, marker.Length);
      List<Vec3F> pts = [];
      pts.AddM (c.Org, c.Org + c.VecX * d, c.Org, c.Org + c.VecY * d / 2);
      Lux.Lines (pts.AsSpan ());
   }
}
#endregion

#region class BPoseVNodeVN -------------------------------------------------------------------------
/// <summary>Viewnode used to render a BPoseNodeVN</summary>
public class BPoseNodeVN (BendPose.Node node) : VNode (node) {
   // Overrides ----------------------------------------------------------------
   public override void SetAttributes ()
      => (Lux.Xfm, Lux.LineWidth) = (mNode.Xfm, 3);

   public override void Draw () {
      var (ent, lie) = (mNode.Ent, mNode.Lie);
      if (ent is E3Flex flex) {
         // If we are drawing the flex at lie 0 or lie 1, we use a retained-mode buffer, since
         // it's likely that rendering will be reused for many frames. If we're drawing the flex
         // at any intermediate lie, we can use a streamed rendering (since that rendered mesh will
         // get disposed of very soon). 
         Streaming = !(lie.IsZero () || lie.EQ (1));
         Lux.Mesh (flex.BuildMesh (lie));
      } else {
         // For a plane, we can just draw the mesh (once, not streaming)
         Lux.Mesh (ent.Mesh);
      }
   }

   // Private data -------------------------------------------------------------
   readonly BendPose.Node mNode = node;
}
#endregion
