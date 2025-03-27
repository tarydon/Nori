namespace WPFDemo;
using Nori;

class MeshScene : Scene3 {
   public MeshScene () {
      mMesh = CMesh.LoadTMesh ($"{Lib.DevRoot}/TData/Geom/CMesh/part.tmesh");
      Bound = mMesh.Bound;
      Root = new MeshVN (mMesh);
   }
   CMesh mMesh;

   public override Color4 BgrdColor => Color4.Gray (96);
}

class MeshVN (CMesh mesh) : VNode {
   public override void SetAttributes () => Lux.Color = new Color4 (255, 255, 128);
   public override void Draw () => Lux.Mesh (mesh, EShadeMode.Phong);
}