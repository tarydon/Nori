using System.Windows.Controls;
using System.Windows;
using Nori;
namespace WPFDemo;

class CSMesherDemo : Scene3, ISceneWithUI {
   public CSMesherDemo () {
      var dwg = DXFReader.Load (mDir + mFiles[1] + ".dxf");
      var sPoly = dwg.Ents.OfType<E2Poly> ().Where (a => a.LayerName == "SIDE").Select (a => a.Poly).ToList ();
      var sPt = dwg.Ents.OfType<E2Point> ().Single (a => a.LayerName == "SIDE").Pt;
      var fPoly = dwg.Ents.OfType<E2Poly> ().Where (a => a.LayerName == "FRONT").Select (a => a.Poly).ToList ();
      var fPt = dwg.Ents.OfType<E2Point> ().Single (a => a.LayerName == "FRONT").Pt;
      sPoly = [.. sPoly.Select (a => a * Matrix2.Translation (-sPt.X, -sPt.Y))];
      fPoly = [.. fPoly.Select (a => a * Matrix2.Translation (-fPt.X, -fPt.Y))];
      int n = fPoly.MaxIndexBy (a => a.GetBound ().Area);
      for (int i = 0; i < fPoly.Count; i++)
         if (i != n) fPoly[i] = fPoly[i].Reversed ();
      var mesher = new CSMesher (fPoly, sPoly);
      var mesh = mesher.Build ();

      Bound = mesh.Bound;
      Root = new Mesh3VN (mesh) { Color = Color4.White };
   }

   string mDir = "C:\\etc\\Demo1\\";
   string[] mFiles = ["LeftHorn", "GaugeTool", "Chess"];
   string[] mTesses = ["VeryCoarse", "Coarse", "Medium", "Fine", "VeryFine"];

   public void CreateUI (UIElementCollection panel) {
      var lb1 = new ListBox { ItemsSource = mFiles, SelectedIndex = 0, Margin = new Thickness (6) };
      panel.Add (lb1);
      var lb2 = new ListBox { ItemsSource = mTesses, SelectedIndex = 2, Margin = new Thickness (6, 0, 6, 6) };
      panel.Add (lb2);
   }
}
