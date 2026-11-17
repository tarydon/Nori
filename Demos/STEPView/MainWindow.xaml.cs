using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nori;
namespace STEPView;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lib.Tessellate = Tess2D.Process;
      InitializeComponent ();
      mContent.Child = WPFHost.Init (this, OnLuxReady);
      mOpen.Click += (s, e) => DoFileOpen ();
      mOpenFlux.Click += (s, e) => DoOpenInFlux ();
      mExit.Click += (s, e) => Close ();
      mList.ItemsSource = Directory.GetFiles (mRoot, "*.stp")
                                   .Select (a => new FileInfo (a))
                                   .OrderBy (a => a.Length)
                                   .Select (a => a.Name)
                                   .Take (100)
                                   .ToList ();
      mList.SelectionChanged += (s, e) => OnFileSelected ();
      mLegend.Text = """
         G : GOOD
         T : TESS-ERROR
         R : RENDER-ERROR
         N : NON-PLANAR-CONTOUR
         E : EMPTY

         W : Toggle wireframe
         Esc : Exit
         """;
      Title = "STEP-Viewer: " + mList.Items.Count + " files";
      Focus ();
   }
   string mRoot = "W:\\STEP";

   protected override void OnKeyDown (KeyEventArgs e) {
      switch (e.Key) {
         case Key.G: MoveTo ("GOOD"); break;
         case Key.T: MoveTo ("TESS-ERROR"); break;
         case Key.R: MoveTo ("RENDER-ERROR"); break;
         case Key.N: MoveTo ("NON-PLANAR-CONTOUR"); break;
         case Key.E: MoveTo ("EMPTY"); break;
         case Key.Escape: Close (); break;
         case Key.W: mWireframe = !mWireframe; Load (mFile, true);  break;
      }

      void MoveTo (string dir) {
         Directory.CreateDirectory ($"W:\\STEP\\{dir}");
         File.Move (mFile, $"W:\\STEP\\{dir}\\{Path.GetFileName (mFile)}");
         Lib.Trace ($"Moved {Path.GetFileName (mFile)} to {dir}");
         if (mList.SelectedIndex == mList.Items.Count - 1) Close ();
         mList.SelectedIndex += 1;
         mList.ScrollIntoView (mList.SelectedItem);
      }
   }
   bool mWireframe = false;

   void OnFileSelected () {
      try {
         Load ($"{mRoot}\\{mList.SelectedItem}");
      } catch (Exception e) {
         Lux.UIScene = new Scene3 (Color4.Gray (208), new Bound3 (Point3.Zero, new Point3 (1, 1, 1)), TraceVN.It);
         Lib.Trace (e);
      }
   }

   void DoFileOpen () {
      var ofd = new OpenFileDialog { Filter = "All Files|*.*" };
      if (ofd.ShowDialog () == true) Load (ofd.FileName);
   }

   void DoOpenInFlux () {
      Process.Start ("flux.exe", mFile);
   }

   void Load (string file, bool preserve = false) {
      if (!File.Exists (file)) {
         Lib.Trace ($"File missing: {file}");
         return;
      }
      var sw = new Stopwatch ();
      var model = new STEPReader (mFile = file).Load ();
      var loadTime = sw.ElapsedMilliseconds;

      int tris = 0;
      foreach (var es in model.Ents.OfType<E3Surface> ())
         tris += es.Mesh.Triangle.Length / 3;

      Vector2 pan = Vector2.Zero;
      double xr = 0, zr = 0, zoom = 0;
      if (preserve) {
         Scene3 sc = (Scene3)Lux.UIScene!;
         (xr, zr) = sc.Viewpoint; zoom = sc.ZoomFactor; pan = sc.PanVector;
      }
      Lux.UIScene = new STEPScene (model, mWireframe);
      if (preserve) {
         Scene3 sc = (Scene3)Lux.UIScene!;
         sc.Viewpoint = (xr, zr); sc.ZoomFactor = zoom; sc.PanVector = pan;
      }

      double diag = model.Bound.Diagonal.Round (0);
      if (tris == 0) throw new Exception ("Empty model");
      mStatus.Text = $"Load:{loadTime} ms, {tris} triangles, diagonal = {diag} mm";
   }
   string mFile = "";

   void OnLuxReady () {
      new SceneManipulator ();
      TraceVN.TextColor = Color4.Black;
      TraceVN.HoldTime = 15;
      Lib.Tracer = TraceVN.Print;
      mList.SelectedIndex = 0;
   }
}

// -------------------------------------------------------------------------------------------------
class STEPScene : Scene3 {
   public STEPScene (Model3 model, bool wireframe) {
      BgrdColor = Color4.Gray (208);
      Bound = model.Bound;
      List<VNode> set = [new Model3VN (model), TraceVN.It];
      if (wireframe) set.Add (new TessViewer (model));
      Root = new GroupVN (set);
   }

   public override void Picked (object obj) {
      if (obj is E3Plane ep) {
         var raw = CurlWriter.SaveToByteArray (obj);
         Lib.Trace (Encoding.UTF8.GetString (raw));
      }
   }
}

// TessViewer --------------------------------------------------------------------------------------
class TessViewer : VNode {
   public TessViewer (Model3 model) {
      foreach (var ec in model.Ents.OfType<E3Surface> ()) {
         var mesh = ec.Mesh;
         var nodes = mesh.Vertex;
         var tris = mesh.Triangle;
         for (int i = 0; i < tris.Length; i += 3) {
            for (int j = 0; j < 3; j++) {
               int k = (j + 1) % 3;
               int a = tris[i + j], b = tris[i + k];
               if (a < b) { mWire.Add (nodes[a].Pos); mWire.Add (nodes[b].Pos); }
            }
         }
      }
   }
   List<Vec3F> mWire = [];

   public override void SetAttributes () {
      Lux.Color = Color4.Gray (64);
      Lux.LineWidth = 1f;
   }

   public override void Draw () => Lux.Lines (mWire.AsSpan ());
}
