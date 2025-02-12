// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
using System.Windows.Input;
namespace WPFDemo;
using static Lux;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();
      Lux.DrawScene = Draw3DScene;
      Lux.Info.Subscribe (FrameDone);
      KeyDown += OnKey;
   }

   void OnKey (object sender, KeyEventArgs e) {
      if (e.Key == Key.Escape) Close ();
      if (e.Key == Key.Left) { zRot -= 1; Lux.Redraw (); }
      if (e.Key == Key.Right) { zRot += 1; Lux.Redraw (); }
      if (e.Key == Key.Up) { xRot -= 1; Lux.Redraw (); }
      if (e.Key == Key.Down) { xRot += 1; Lux.Redraw (); }
      if (e.Key == Key.S) { nShader = (nShader + 1) % 3; Lux.Redraw (); }
   }
   int xRot = -60, zRot = 135, nShader = 0;

   void Draw3DScene ((int X, int Y) viewport) {
      if (mMesh == null) {
         // robot-1.tmesh, part.tmesh, suzanne.tmesh
         mMesh ??= CMesh.LoadTMesh ("n:/demos/data/part.tmesh");

         List<Point3> pts = [];
         for (int i = 0; i < mMesh.Triangle.Length; i++) {
            var pos = mMesh.Vertex[mMesh.Triangle[i]].Pos;
            pts.Add (new Point3 (pos.X, pos.Y, pos.Z));
         }
         mMesh = new CMeshBuilder (pts.AsSpan ()).Build ();
      }

      var bound = mMesh.Bound; var mid = bound.Midpoint;
      var viewpoint = Quaternion.FromAxisRotations (xRot.D2R (), 0, zRot.D2R ());
      var worldXfm = Matrix3.Translation (-mid.X, -mid.Y, -mid.Z) * Matrix3.Rotation (viewpoint);

      double aspect = viewport.X / (double)viewport.Y, radius = bound.Diagonal / 2, dx = radius, dy = radius;
      if (aspect > 1) dx = aspect * dy; else dy = dx / aspect; 
      Bound3 frustum = new (-dx, -dy, -radius, dx, dy, radius);
      var projectionXfm = Matrix3.Orthographic (frustum);
      var xfm = worldXfm * projectionXfm;
      Xfm = (Mat4F)xfm;
      NormalXfm = Xfm.ExtractRotation ();

      // DrawColor = new Color4 ((int)(0.55 * 256), (int)(0.21 * 256), (int)(0.06 * 256));
      DrawColor = new Color4 ((int)(0.75 * 256), (int)(0.6 * 256), (int)(0.22 * 256));
      Mesh (mMesh, nShader);
   }
   CMesh? mMesh;

   void Draw2DScene ((int X, int Y) viewport) {
      Xfm = (Mat4F)Matrix3.Map (new Bound2 (0, 0, 100, 80), viewport);
      
      PointSize = 36f;
      DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      LineWidth = 3f;
      DrawColor = Color4.Yellow;
      Lines ([new (10, 10), new (90, 10), new (90, 10), new (90, 40)]);
      Lines ([new (13, 13), new (93, 13), new (93, 13), new (93, 43)]);

      LineWidth = 6f;
      DrawColor = Color4.White;
      Beziers ([new (10, 10), new (10, 40), new (80, 20), new (80, 50)]);

      LineWidth = 12f;
      DrawColor = Color4.Blue;
      Lines ([new (90, 40), new (10, 10)]);

      PointSize = 12f;
      DrawColor = Color4.Green;
      Points ([new (95, 45), new (15, 15), new (95, 15)]);

      LineWidth = 3f;
      DrawColor = Color4.Yellow;
      Lines ([new (13, 43), new (93, 13)]);

      PointSize = 36f;
      DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      DrawColor = Color4.Cyan;
      Triangles ([new (30, 40), new (40, 40), new (40, 45)]);

      DrawColor = Color4.Cyan;
      Quads ([new (50, 40), new (60, 40), new (65, 45), new (50, 50)]);
   }

   void FrameDone (Lux.Stats s) {
      Title = $"Frame {s.NFrame}, Pgms:{s.PgmChanges}, VAO:{s.VAOChanges}, Uniforms:{s.ApplyUniforms}, Draws:{s.DrawCalls}, Verts:{s.VertsDrawn}";
   }
}