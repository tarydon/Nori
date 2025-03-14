// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();

      Lux.UIScene = new MeshScene ();
      Lux.OnReady = (() => new SceneManipulator ());
   }
}

// class LTypeScene --------------------------------------------------------------------------------
// Demo scene for various line-types
class MeshScene : Scene3 {
   public MeshScene () {
      mMesh1 = CMesh.LoadTMesh ("N:/Demos/Data/part.tmesh");
      Bound = mMesh1.Bound;
      mMesh2 = mMesh1.Translated (new (0, 0, -Bound.Z.Length * 1.5));
      Bound += mMesh2.Bound;
   }
   CMesh mMesh1, mMesh2;

   public override Color4 BgrdColor => Color4.Gray (64);

   public override void Draw () {
      Lux.DrawColor = Color4.Red;
      Lux.Mesh (mMesh1, EShadeMode.Gourad);
      Lux.DrawColor = Color4.Green;
      Lux.Mesh (mMesh2, EShadeMode.Glass);
   }
}
