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

class E3CurveVN (E3Curve mCurve) : Ent3VN (mCurve) {
   public override void Draw () {
      List<Point3> pts = [];
      mCurve.Curve.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
      pts.Add (mCurve.Curve.End);

      List<Vec3F> vec = [pts[0]];
      for (int i = 1; i < pts.Count; i++) { vec.Add (pts[i]); vec.Add (pts[i]); }
      vec.RemoveLast ();
      Lux.Lines (vec.AsSpan ());
   }
}

public class Curve3VN (Curve3 edge) : VNode (edge) {
   public override void Draw () {
      if (mPts.Count == 0) {
         List<Point3> pts = [];
         mEdge.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
         mPts.Add (pts[0]);
         for (int i = 0; i < pts.Count - 1; i++) { mPts.Add (pts[i]); mPts.Add (pts[i]); }
         mPts.Add (pts[^1]);
      }
      Lux.Lines (mPts.AsSpan ());
   }
   readonly Curve3 mEdge = edge;
   List<Vec3F> mPts = [];
}
