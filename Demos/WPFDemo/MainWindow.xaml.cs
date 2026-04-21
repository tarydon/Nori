// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window of WPF demo application (various scenes implemented)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Drawing;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();  // REMOVETHIS later
      InitializeComponent ();
      mContent.Child = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
   }

   void LeafDemo (object s, RoutedEventArgs e) => Display (s, new LeafDemoScene ());
   void LineFontDemo (object s, RoutedEventArgs e) => Display (s, new LineFontScene ());
   void TrueTypeDemo (object s, RoutedEventArgs e) => Display (s, new TrueTypeScene ());
   void MeshDemo (object s, RoutedEventArgs e) => Display (s, new MeshScene ());
   void TessDemo (object s, RoutedEventArgs e) => Display (s, new MeshScene (true));
   void BooleanDemo (object s, RoutedEventArgs e) => Display (s, new BooleanScene ());
   void DwgDemo (object s, RoutedEventArgs e) => Display (s, new DwgScene ());
   void RobotDemo (object s, RoutedEventArgs e) => Display (s, new RobotScene ());
   void STPDemo (object s, RoutedEventArgs e) => Display (s, new STPScene ());
   void StreamDemo (object s, RoutedEventArgs e) => Display (s, new StreamDemoScene ());
   void MinSphereDemo (object s, RoutedEventArgs e) => Display (s, new MinSphereScene ());
   void T3XReaderDemo (object s, RoutedEventArgs e) => Display (s, new T3XDemoScene ());
   void SliceMeshDemo (object s, RoutedEventArgs e) => Display (s, new IntMeshPlaneScene ());
   void ConvexHullDemo (object s, RoutedEventArgs e) => Display (s, new ConvexHullScene ());
   void BuildOBBDemo (object s, RoutedEventArgs e) => Display (s, new BuildOBBScene ());
   void CollisionDemo (object s, RoutedEventArgs e) => Display (s, new OBBCrashScene ());
   void PaperFolderDemo (object s, RoutedEventArgs e) => Display (s, new PaperFolderScene ());
   void SubScene (object s, RoutedEventArgs e) => Display (s, new SubSceneDemo ());
   void CSMesher (object s, RoutedEventArgs e) => Display (s, new CSMesherDemo ());

   void Display (object s, Scene scene) {
      if (s is Button b) {
         mPrevButton?.Background = mPrevBrush;
         mPrevButton = b; mPrevBrush = b.Background;
         b.Background = System.Windows.Media.Brushes.LightBlue;
      }
      mSettings.Children.Clear (); TraceVN.It.Clear (); 
      Lux.UIScene = scene;
      if (scene is ISceneWithUI sc) sc.CreateUI (mSettings.Children);
      if (scene is STPScene or T3XDemoScene) Lux.BackFacesPink = true;
   }
   Button? mPrevButton;
   System.Windows.Media.Brush? mPrevBrush;
}

interface ISceneWithUI {
   void CreateUI (UIElementCollection panel);
}
