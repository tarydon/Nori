using System.Windows;
using Nori;

namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();  // REMOVETHIS later
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new DemoScene ();
   }
}

class DemoScene : Scene3 {
   public DemoScene () {
      var dwg = DXFReader.Load (System.IO.Directory.GetFiles ("C:\\etc\\Fold", "*.dxf")[0]);
      var folder = new PaperFolder (dwg);
      mModel = folder.Process ();
      folder.Dump ("c:/etc/test.dxf");

      Bound = mModel.Bound;
      Root = new Model3VN (mModel);
      BgrdColor = new Color4 (80, 100, 120);
   }
   Model3 mModel;

   public override void Picked (object obj) {
      if (obj is Ent3 ent) {
         ent.IsSelected = true;
         if (HW.IsCtrlDown) mModel.Ents.Remove (ent);
      }
   }
}
