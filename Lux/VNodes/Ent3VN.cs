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

#region class E3SurfaceVN --------------------------------------------------------------------------
/// <summary>Renders entities derived from E3Surface</summary>
/// We ask the E3Surface to compute and return a Mesh3 that we render here. Depending on 
/// the IsTranslucent / NoStencil bits of the entity, we choose different Mesh rendering modes.
public class E3SurfaceVN (E3Surface mSurface) : Ent3VN (mSurface) {
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
      var (c, d) = (marker.CS, marker.Size);
      List<Vec3F> pts = [];
      pts.AddM (c.Org, c.Org + c.VecX * d, c.Org, c.Org + c.VecY * d / 2);
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

[Obsolete ("Will be removed soon - tell Arvind if you need this")]   // Remove 260706
public class Curve3VN (Curve3 edge) : VNode (edge) {
   public override void Draw () {
      if (mPts.Count == 0) {
         List<Point3> pts = [];
         mEdge.Discretize (pts, ETess.Fine);
         mPts.Add (pts[0]);
         for (int i = 0; i < pts.Count - 1; i++) { mPts.Add (pts[i]); mPts.Add (pts[i]); }
         mPts.Add (pts[^1]);
      }
      Lux.Lines (mPts.AsSpan ());
   }
   readonly Curve3 mEdge = edge;
   List<Vec3F> mPts = [];
}

[Obsolete ("Will be removed soon - tell Arvind if you need this")]   // Remove 260706
public class E3ContourVN (E3Contour cp) : VNode (cp) {
   public override VNode? GetChild (int n) {
      if (n < mCurves.Count) return mCurves[n];
      else return null;
   }
   readonly List<Curve3VN> mCurves = [.. cp.Curves.Select (a => new Curve3VN (a))];
}
