// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
using System.Reactive.Linq;
namespace WPFDemo;
using static Lux;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();

      Lux.UIScene = new DemoScene2 ();
      Lux.Info.Subscribe (FrameDone);
      Lux.OnReady = () => HW.Keys.Where (a => a.State == EKeyState.Pressed).Subscribe (OnKey);
   }

   void OnKey (KeyInfo k) {
      switch (k.Key) {
         case EKey.Escape: 
            Close (); 
            break;
         // Use Left/Right to rotate 3D scenes about vertical axis
         case EKey.Left: case EKey.Right: 
            if (Lux.UIScene is Scene3 s3) {
               var (x, z) = s3.Viewpoint;
               z += k.Key == EKey.Left ? -1 : 1;
               s3.Viewpoint = (x, z);
            }
            break;
         // Use Up/Down to zoom 2D scenes in/out
         case EKey.Up: case EKey.Down:
            if (Lux.UIScene is Scene2 s2) {
               var b = s2.Bound;
               s2.Bound = b.InflatedF (k.Key == EKey.Up ? 0.99 : (1 / 0.99));
            }
            break;
         // Use '2' key to switch to a 2D scene
         case EKey.D2: 
            Lux.UIScene = new DemoScene2 (); 
            break;
         // Use '3' key to switch to a 3D scene
         case EKey.D3: 
            Lux.UIScene = new DemoScene3 (); 
            break;
      }
   }

   void FrameDone (Lux.Stats s) {
      Title = $"Frame {s.NFrame}, Pgms:{s.PgmChanges}, VAO:{s.VAOChanges}, Uniforms:{s.ApplyUniforms}, Draws:{s.DrawCalls}, Verts:{s.VertsDrawn}";
   }
}

// Demo 2D Scene -----------------------------------------------------------------------------------
class DemoScene2 : Scene2 {
   public DemoScene2 () => Bound = new Bound2 (0, 0, 100, 80);

   public override Color4 BgrdColor => new (80, 96, 128);

   public override void Draw () {
      PointSize = 36f; DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      LineWidth = 3f; DrawColor = Color4.Yellow;
      Lines ([new (10, 10), new (90, 10), new (90, 10), new (90, 40)]);
      Lines ([new (13, 13), new (93, 13), new (93, 13), new (93, 43)]);

      LineWidth = 6f; DrawColor = Color4.White;
      Beziers ([new (10, 10), new (10, 40), new (80, 20), new (80, 50)]);

      LineWidth = 12f; DrawColor = Color4.Blue;
      Lines ([new (90, 40), new (10, 10)]);

      PointSize = 12f; DrawColor = Color4.Green;
      Points ([new (95, 45), new (15, 15), new (95, 15)]);

      LineWidth = 3f; DrawColor = Color4.Yellow;
      Lines ([new (13, 43), new (93, 13)]);

      PointSize = 36f; DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      DrawColor = Color4.Cyan;
      Triangles ([new (30, 40), new (40, 40), new (40, 45)]);

      DrawColor = Color4.Cyan;
      Quads ([new (50, 40), new (60, 40), new (65, 45), new (50, 50)]);
   }
}

// Demo 3D Scene -----------------------------------------------------------------------------------
class DemoScene3 : Scene3 {
   public DemoScene3 () {
      string name = mNames[mnMesh++ % 3];
      mMesh = CMesh.LoadTMesh ($"n:/demos/data/{name}.tmesh");
      Bound = mMesh.Bound;
   }
   CMesh mMesh;
   static string[] mNames = ["part", "robot-1", "suzanne"];
   static int mnMesh;

   public override Color4 BgrdColor => new (128, 96, 80);

   public override void Draw () {
      DrawColor = new Color4 ((int)(0.75 * 256), (int)(0.6 * 256), (int)(0.22 * 256));
      Mesh (mMesh, 2);
   }
}
