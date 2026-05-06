// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3ThickVN.cs
// ║║║║╬║╔╣║ Contains VNodes to render
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E3ThickVN ----------------------------------------------------------------------------
/// <summary>VNode used to render E3Thick (flats / flexes)</summary>
public class E3ThickVN (E3Thick thick) : VNode (thick) {
   public override void SetAttributes () => Lux.LineWidth = 3; // REMOVETHIS
   public override void Draw () => Lux.Mesh (thick.Mesh);
}
#endregion

#region class BPoseNodeVN --------------------------------------------------------------------------
/// <summary>Viewnode used to render a BendPose.Node</summary>
public class BPoseNodeVN (BendPose.Node node) : VNode (node) {
   // Overrides ----------------------------------------------------------------
   public override void SetAttributes ()
      => Lux.Xfm = mNode.Xfm;

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
