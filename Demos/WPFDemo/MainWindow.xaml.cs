// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window of WPF demo application (various scenes implemented)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      mContent.Child = Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      new SceneManipulator ();
   }

   void LeafDemo (object sender, RoutedEventArgs e) => Display (new LeafDemoScene ());
   void LineFontDemo (object sender, RoutedEventArgs e) => Display (new LineFontScene ());
   void TrueTypeDemo (object sender, RoutedEventArgs e) => Display (new TrueTypeScene ());
   void MeshDemo (object sender, RoutedEventArgs e) => Display (new MeshScene ());
   void TessDemo (object sender, RoutedEventArgs e) => Display (new MeshScene (true));
   void BooleanDemo (object sender, RoutedEventArgs e) => Display (new BooleanScene ());
   void DwgDemo (object sender, RoutedEventArgs e) => Display (new DwgScene ());
   void RobotDemo (object sender, RoutedEventArgs e) => Display (new RobotScene ());
   void STPDemo (object sender, RoutedEventArgs e) => Display (new STPScene ());
   void StreamDemo (object sender, RoutedEventArgs e) => Display (new StreamDemoScene ());
   void CMeshDemo (object sender, RoutedEventArgs e) => Display (new CMeshBuildDemo ());

   void Display (Scene scene) {
      mSettings.Children.Clear ();
      Lux.UIScene = scene;
      if (scene is RobotScene rs) rs.CreateUI (mSettings.Children);
      if (scene is STPScene) Lux.BackFacesPink = true; 
   }
}

class CMeshBuildDemo : Scene3 {
   public CMeshBuildDemo () {
      // var mesh = Mesh3.LoadObj ("C:/Etc/Cow.obj");
      var mesh = Mesh3.LoadTMesh ("N:/TData/Geom/Mesh3/part.tmesh");
      var sw = Stopwatch.StartNew ();
      var cmesh = CMeshBuilder.Build (mesh);
      sw.Stop ();
      Lib.Trace ($"Build: {(int)sw.ElapsedMilliseconds} ms");

      mCMeshVN = new CMeshVN (cmesh);
      var meshVN = new MeshVN (mesh) { 
         Shading = EShadeMode.Glass, 
         Color = new Color4 (128, 128, 128)
      };
      Lib.Tracer = TraceVN.Print;
      Root = new GroupVN ([meshVN, mCMeshVN, TraceVN.It]);
      BgrdColor = Color4.Gray (64);
      Bound = mesh.Bound;
   }

   CMeshVN mCMeshVN;
}

class CMeshVN : VNode {
   public CMeshVN (CMesh cm) {
      mCM = cm;
      mDisp = HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey);
   }
   IDisposable mDisp;
   public readonly CMesh mCM;

   public override void OnDetach () => mDisp.Dispose ();

   void OnKey (KeyInfo key) {
      if (key.Key == EKey.Q) { mLevel++; Redraw (); }
      if (key.Key == EKey.Z && mLevel > 0) { mLevel--; Redraw (); } 
   }

   public override void SetAttributes () {
      Lux.Color = Color4.White;
      Lux.LineWidth = 2f;
   }

   public override void Draw () {
      var boxes = mCM.EnumBoxes ().Where (a => a.Level == mLevel).ToList ();
      Lib.Trace ($"Level {mLevel}, {boxes.Count} boxes");
      List<Vec3F> pts = [];
      foreach (var (box0, level) in boxes) {
         var box = box0.InflatedF (1);
         var (x, y, z) = (box.X, box.Y, box.Z);
         Vec3F a = new (x.Min, y.Min, z.Min), b = new (x.Max, y.Min, z.Min);
         Vec3F c = new (x.Max, y.Max, z.Min), d = new (x.Min, y.Max, z.Min);
         Vec3F e = new (x.Min, y.Min, z.Max), f = new (x.Max, y.Min, z.Max);
         Vec3F g = new (x.Max, y.Max, z.Max), h = new (x.Min, y.Max, z.Max);
         pts.AddRange ([a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h]);
      }
      Lux.Lines (pts.AsSpan ());
   }
   int mLevel = 0;
}