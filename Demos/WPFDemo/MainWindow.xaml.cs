// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window of WPF demo application (various scenes implemented)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using System.Windows.Controls;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      mContent.Child = WPFHost.Init (this, OnLuxReady);
   }

   void OnLuxReady () {
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
   }

   void LeafDemo (object s, RoutedEventArgs e) => Display (s, new LeafDemoScene ());
   void LineFontDemo (object s, RoutedEventArgs e) => Display (s, new LineFontScene ());
   void TrueTypeDemo (object s, RoutedEventArgs e) => Display (s, new TrueTypeScene ());
   void TessDemo (object s, RoutedEventArgs e) => Display (s, new MeshScene ());
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
   void TwoViewMesh (object s, RoutedEventArgs e) => Display (s, new TwoViewMeshDemo ());
   void E3ThickDemo (object s, RoutedEventArgs e) => Display (s, new E3ThickDemo ());

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
