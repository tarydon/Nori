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
                                   .Take (51)
                                   .Select (a => a.Name)
                                   .ToList ();
      mList.SelectionChanged += (s, e) => OnFileSelected ();
      mLegend.Text = """
         D : DONE
         U : UNIT
         P : NURB-PLANE
         C : NURB-CYLINDER
         S : ASSY
         M : MISC

         A : Toggle selection
         W : Toggle wireframe
         Del : Delete surface
         Esc : Exit
         """;
      Title = "STEP-Viewer: " + mList.Items.Count + " files";
      Focus ();
   }
   string mRoot = "W:\\STEP";

   protected override void OnKeyDown (KeyEventArgs e) {
      switch (e.Key) {
         case Key.D: MoveTo ("DONE"); break;
         case Key.U: MoveTo ("UNIT"); break;
         case Key.P: MoveTo ("NURB-PLANE"); break;
         case Key.C: MoveTo ("NURB-CYLINDER"); break;
         case Key.S: MoveTo ("ASSY"); break;
         case Key.M: MoveTo ("MISC"); break;

         case Key.A: (Lux.UIScene as STEPScene)?.ToggleSelection (); break;
         case Key.Escape: Close (); break;
         case Key.Delete: (Lux.UIScene as STEPScene)?.DeleteSelected ();
            break;
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
      TraceVN.It.Clear ();
      Lib.Whiteboard["FILENAME"] = Path.GetFileNameWithoutExtension (file);
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

      var diag = model.Bound.DiagVector;
      mStatus.Text = $"Load:{loadTime} ms, {tris} triangles, Bound: <{diag.X.Round(1)} x {diag.Y.Round (1)} x {diag.Z.Round (1)}>";

      var file1 = $"c:/etc/po/{Path.GetFileNameWithoutExtension (file)}.txt";
      var desc1 = new DescribeModel (file, model).Process ();
      File.WriteAllText (file1, desc1);
      var file0 = $"W:\\STEP\\NOTES\\{Path.GetFileNameWithoutExtension (file)}.txt";
      var desc0 = File.ReadAllText (file0);
      if (!Same (desc1, desc0)) {
         Lux.UIScene.BgrdColor = new (255, 208, 208);
         Process.Start ("winmergeu.exe", $"{file0} {file1}").WaitForInputIdle ();
         this.Activate ();
      }
   }
   string mFile = "";

   static bool Same (string d1, string d2) {
      if (d1 == d2) return true;
      var w1 = d1.Split ('\n'); var w2 = d2.Split ('\n');
      if (w1.Length != w2.Length) return false;
      for (int i = 0; i < w1.Length; i++) {
         if (w1[i] == w2[i]) continue;
         string s1 = w1[i].Trim (), s2 = w2[i].Trim ();
         if (s1.StartsWith ("BOUND:") && s2.StartsWith ("BOUND:")) {
            int[] a = [.. s1[6..].Split ('x').Select (int.Parse)];
            int[] b = [.. s2[6..].Split ('x').Select (int.Parse)];
            bool same = true;
            for (int j = 0; j < 3; j++)
               if (Math.Abs (a[j] - b[j]) > 1) same = false;
            if (same) continue;
         }
         return false;
      }
      return true;
   }

   void OnLuxReady () {
      new SceneManipulator ();
      TraceVN.TextColor = Color4.Black;
      TraceVN.HoldTime = 20;
      Lib.Tracer = TraceVN.Print;
      mList.SelectedIndex = 0;
   }
}

// -------------------------------------------------------------------------------------------------
class STEPScene : Scene3 {
   public STEPScene (Model3 model, bool wireframe) {
      BgrdColor = Color4.Gray (208);
      Bound = (mModel = model).Bound;
      List<VNode> set = [new Model3VN (model), TraceVN.It];
      if (wireframe) set.Add (new TessViewer (model));
      Root = new GroupVN (set);
   }

   public void ToggleSelection () {
      mSelected.Clear ();
      foreach (var ent in mModel.Ents) {
         if (ent.IsSelected) ent.IsSelected = false;
         else { ent.IsSelected = true; mSelected.Add (ent); }
      }
   }

   public void DeleteSelected () {
      foreach (var ent in mSelected) mModel.Ents.Remove (ent);
      mSelected.Clear ();
      Bound = mModel.Bound; ZoomExtents ();
   }

   public override void Picked (object obj) {
      base.Picked (obj);
      if (obj is E3Surface ent) {
         var raw = CurlWriter.SaveToByteArray (obj);
         var str = Encoding.UTF8.GetString (raw);

         StringBuilder sb = new ();
         foreach (var con in ent.Contours) {
            sb.AppendLine ("Contour");
            foreach (var edge in con.Curves) {
               sb.Append ($"  #{edge.PairId} {edge.GetType ().Name} {S (edge.Start)}  ..  {S (edge.End)}");
               if (edge is Arc3 arc) sb.Append ($" Span: {arc.AngSpan.R2D ().Round (0)}\u00b0");
               sb.AppendLine ();
            }
         }
         if (ent is E3NurbsSurface nurb) {
            int u = nurb.UCtl, v = nurb.VCtl;
            sb.AppendLine ($"Control: {u} x {v}");
         }
         var str2 = sb.ToString ();
         Lib.Trace (str2);


         File.WriteAllText ("c:/etc/entity.curl", str);

         mSelected.ForEach (a => a.IsSelected = false); mSelected.Clear ();
         ent.IsSelected = true; mSelected.Add (ent);
      }

      string S (Point3 p) 
         => $"({Math.Round (p.X, 1)},{Math.Round (p.Y, 1)},{Math.Round (p.Z, 1)})";
   }

   readonly Model3 mModel;
   readonly List<Ent3> mSelected = [];
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
