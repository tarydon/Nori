// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3VN.cs
// ║║║║╬║╔╣║ Contains VNodes to render various types of Ent3 (3-D entities that can sit in a model)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Ent3VN -------------------------------------------------------------------------------
/// <summary>Base class for various entity VNs</summary>
public abstract class Ent3VN (Ent3 mEnt) : VNode (mEnt) {
   /// <summary>We set the entity color based on state: Normal / Selected / Colliding</summary>
   public override void SetAttributes () {
      Lux.Color = mEnt.IsColliding ? new Color4 (255, 64, 32) :
                  mEnt.IsSelected ? new Color4 (128, 192, 255) : Color4.White;
   }
}
#endregion

#region class E3CurveVN ----------------------------------------------------------------------------
/// <summary>Render an E3Curve entity (free-space curve)</summary>
public class E3CurveVN (E3Curve mCurve) : Ent3VN (mCurve) {
   public override void Draw () {
      List<Point3> pts = [];
      mCurve.Curve.Discretize (pts, ETess.Fine);
      pts.Add (mCurve.Curve.End);

      List<Vec3F> vec = [pts[0]];
      for (int i = 1; i < pts.Count; i++) { vec.Add (pts[i]); vec.Add (pts[i]); }
      vec.RemoveLast ();
      Lux.Lines (vec.AsSpan ());
   }
}
#endregion

#region class E3InsertVN ---------------------------------------------------------------------------
/// <summary>VNode to render an E3Insert</summary>
/// An E3Insert references a block and provides a transformation for it
[Used]
class E3InsertVN (E3Insert e3i) : Ent3VN (e3i) {
   // The only child for the E3Insert is the Block3VN. Since we call Block3VN.For that
   // constructs a shared Block3VN that is used by all the inserts (only the transform
   // distinguishes them from each other)
   public override VNode? GetChild (int n) => n == 0 ? Block3VN.Get (e3i.Block) : null;

   // The attribute we set pushes this Insert's Xfm on the draw stack.
   public override void SetAttributes () { base.SetAttributes (); Lux.Xfm = e3i.Xfm; }
}
#endregion

#region class E3MeshVN -----------------------------------------------------------------------------
/// <summary>Renders a E3Mesh</summary>
/// <param name="mMesh"></param>
public class E3MeshVN (E3Mesh mMesh) : Ent3VN (mMesh) {
   public override void Draw () => Lux.Mesh (mMesh.Mesh);
}
#endregion

#region class E3SurfaceVN --------------------------------------------------------------------------
/// <summary>Renders entities derived from E3Surface</summary>
/// We ask the E3Surface to compute and return a Mesh3 that we render here. Depending on 
/// the IsTranslucent / NoStencil bits of the entity, we choose different Mesh rendering modes.
public class E3SurfaceVN (E3Surface mSurface) : Ent3VN (mSurface) {
   public override void SetAttributes () {   // REMOVETHIS
      if (!mSurface.IsSelected) Lux.Color = mSurface.Color;
      else base.SetAttributes ();   
   }

   public override void Draw () {
      var mode = (mSurface.IsTranslucent, mSurface.NoStencil) switch {
         (true, true) => EShadeMode.GlassNoStencil,
         (true, false) => EShadeMode.Glass,
         (false, true) => EShadeMode.PhongNoStencil,
         _ => EShadeMode.Phong,
      };
      Lux.Mesh (mSurface.Mesh, mode);
   }
}
#endregion

#region class E3MarkerVN ---------------------------------------------------------------------------
/// <summary>VNode used to render E3Marker</summary>
public class E3MarkerVN (E3Marker marker) : VNode (marker) {
   public override void SetAttributes () =>
      (Lux.Color, Lux.LineWidth, Lux.ZLevel) = (marker.Color, 3, 1);

   public override void Draw () {
      if (marker.Kind != E3Marker.EKind.CS) throw new NotImplementedException (); 
      var (c, d, k) = (marker.CS, marker.Size, marker.Size / 8);
      var (o, z) = (c.Org, c.VecZ);
      List<Vec3F> pts = [];
      pts.AddM (o, o + c.VecX * d, o, o + c.VecY * d / 2,
                o + z * k, o + z * 2 * k, o + z * 3 * k, o + z * 4 * k);
      Lux.Lines (pts.AsSpan ());
   }
}
#endregion

#region class Model3VN -----------------------------------------------------------------------------
/// <summary>VNode for a Model3 container</summary>
public class Model3VN : VNode {
   public Model3VN (Model3 model) : base (model) => ChildSource = model.Ents;
}
#endregion

#region class Block3VN -----------------------------------------------------------------------------
/// <summary>Block3VN is a VNode for a Block3 (referenced by an E3Insert)</summary>
/// With this, multiple inserts using the same block will all reuse the same VN, just with
/// different transforms. This code simply draws all the entities in the block. When
/// it appears as the child VN of an E3InsertVN, that parent has already pushed a transform
/// on the stack so that this drawing all happens with the correct transform
class Block3VN (Block3 block) : VNode (block) {
   /// <summary>Gets the BlockVN corresponding to a given block</summary>
   public static Block3VN Get (Block3 block) {
      if (block.VNode is not Block3VN bvn)
         block.VNode = bvn = new Block3VN (block);
      return bvn;
   }

   // The children of the BlockVN are EntityVN, each one wrapping around one Entity in the block
   public override VNode? GetChild (int n) {
      var ent = block.Ents.SafeGet (n);
      return ent == null ? null : MakeFor (ent);
   }

   // Overrides  --------------------------------------------------------------
   public override void OnDetach () {
      base.OnDetach ();
      block.VNode = null; // reset the cached visual node
   }
}
#endregion
