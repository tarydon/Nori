// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3VN.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public abstract class Ent3VN (Ent3 mEnt) : VNode (mEnt) {
   public override void SetAttributes () {
      Lux.Color = mEnt.IsSelected ? new Color4 (128, 192, 255) : Color4.White;
   }
}

public class E3SurfaceVN (E3Surface mSurface) : Ent3VN (mSurface) {
   public override void Draw () 
      => Lux.Mesh (mSurface.Mesh, mSurface.IsTranslucent ? EShadeMode.GlassNoStencil : EShadeMode.Phong);
}

public class Curve3VN (Edge3 edge) : VNode (edge) {
   public override void Draw () {
      if (mPts.Count == 0) {
         List<Point3> pts = [];
         mEdge.Discretize (pts, Lib.FineTess, 0.5411);
         mPts.Add ((Vec3F)pts[0]);
         for (int i = 0; i < pts.Count - 1; i++) { mPts.Add ((Vec3F)pts[i]); mPts.Add ((Vec3F)pts[i]); }
         mPts.Add ((Vec3F)pts[^1]);
      }
      Lux.Lines (mPts.AsSpan ());
   }
   readonly Edge3 mEdge = edge;
   List<Vec3F> mPts = [];
}
