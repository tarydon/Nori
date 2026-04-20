using System.Windows.Controls;
using System.Windows;
using Nori;
using System.Diagnostics;
namespace WPFDemo;

class CSMesherDemo : Scene3, ISceneWithUI {
   public CSMesherDemo () {
      Lib.Tracer = TraceVN.Print;
      TraceVN.TextColor = Color4.Yellow; TraceVN.HoldTime = 12;
   }

   public void CreateUI (UIElementCollection panel) {
      var thick = new Thickness (6, 0, 6, 6);
      panel.Add (new TextBlock { Text = "Model", Margin = new Thickness (7, 6, 6, 0), FontWeight = FontWeights.Bold });
      mModelLB.ItemsSource = mFiles; mModelLB.Margin = new Thickness (6, 0, 6, 6); mModelLB.SelectedIndex = 1;
      mModelLB.SelectionChanged += (s, e) => Redo ();
      panel.Add (mModelLB);

      panel.Add (new TextBlock { Text = "Tessellation", Margin = new Thickness (7, 6, 6, 0), FontWeight = FontWeights.Bold });
      mTessLB.ItemsSource = mTesses; mTessLB.Margin = thick; mTessLB.SelectedIndex = 3;
      mTessLB.SelectionChanged += (s, e) => Redo ();
      panel.Add (mTessLB);
      mWireCB.Margin = thick;
      mWireCB.Checked += (s, e) => Redo (); mWireCB.Unchecked += (s, e) => Redo ();
      panel.Add (mWireCB);
      Redo ();

      Lux.AddSubScene (mScene2, new (0.8, 0.01, 0.99, 0.2));
   }
   ListBox mModelLB = new (), mTessLB = new ();
   CheckBox mWireCB = new () { Content = "Wireframe" };
   Scene2 mScene2 = new () { BgrdColor = Color4.Gray (216) };

   void Redo () {
      var dwg = DXFReader.Load (mDir + mFiles[mModelLB.SelectedIndex] + ".dxf");
      var sPoly = dwg.Ents.OfType<E2Poly> ().Where (a => a.LayerName == "SIDE").Select (a => a.Poly).ToList ();
      var sPt = dwg.Ents.OfType<E2Point> ().Single (a => a.LayerName == "SIDE").Pt;
      var fPoly = dwg.Ents.OfType<E2Poly> ().Where (a => a.LayerName == "FRONT").Select (a => a.Poly).ToList ();
      var fPt = dwg.Ents.OfType<E2Point> ().Single (a => a.LayerName == "FRONT").Pt;
      sPoly = [.. sPoly.Select (a => a * Matrix2.Translation (-sPt.X, -sPt.Y))];
      fPoly = [.. fPoly.Select (a => a * Matrix2.Translation (-fPt.X, -fPt.Y))];
      int n = fPoly.MaxIndexBy (a => a.GetBound ().Area);
      for (int i = 0; i < fPoly.Count; i++)
         if (i != n) fPoly[i] = fPoly[i].Reversed ();

      var sw = Stopwatch.StartNew ();
      var mesher = new CSMesher (fPoly, sPoly);
      mesher.Tess = Enum.Parse<ETess> ((string)mTessLB.SelectedItem);
      var mesh = mesher.Build ();
      sw.Stop ();
      Lib.Trace ($"{mesh.Triangle.Length / 3} triangles, {BlockTimer.FmtTime (sw)}");
      if (mWireCB.IsChecked == true) mesh = mesh.Wireframed ();      

      Bound = mesh.Bound;
      Root = new GroupVN ([new Mesh3VN (mesh) { Color = Color4.White }, TraceVN.It]);
   }
   string mDir = "C:\\etc\\Demo1\\";
   string[] mFiles = ["Simplex", "LeftHorn", "GaugeTool", "HoleTool", "Chess"];
   string[] mTesses = ["VeryCoarse", "Coarse", "Medium", "Fine", "VeryFine"];
}
