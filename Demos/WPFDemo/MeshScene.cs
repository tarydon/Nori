// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MeshScene.cs
// ║║║║╬║╔╣║ Demo that uses Lux.Mesh to draw a 3D mesh, with stencil lines
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;

class MeshScene : Scene3 {
   public MeshScene () {
      Root = new MeshVN (mMesh = CMesh.LoadTMesh ($"{Lib.DevRoot}/TData/Geom/CMesh/part.tmesh"));
      BgrdColor = Color4.Gray (96);
      Bound = mMesh.Bound;
   }
   CMesh mMesh;
}

class MeshVN (CMesh mesh) : VNode {
   public override void SetAttributes () => Lux.Color = new Color4 (255, 255, 128);
   public override void Draw () => Lux.Mesh (mesh, EShadeMode.Phong);
}
