// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window of WPF demo application (various scenes implemented)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using System.Windows;
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

   void LeafDemo (object s, RoutedEventArgs e) => Display (new LeafDemoScene ());
   void LineFontDemo (object s, RoutedEventArgs e) => Display (new LineFontScene ());
   void TrueTypeDemo (object s, RoutedEventArgs e) => Display (new TrueTypeScene ());
   void MeshDemo (object s, RoutedEventArgs e) => Display (new MeshScene ());
   void TessDemo (object s, RoutedEventArgs e) => Display (new MeshScene (true));
   void BooleanDemo (object s, RoutedEventArgs e) => Display (new BooleanScene ());
   void DwgDemo (object s, RoutedEventArgs e) => Display (new DwgScene ());
   void RobotDemo (object s, RoutedEventArgs e) => Display (new RobotScene ());
   void STPDemo (object s, RoutedEventArgs e) => Display (new STPScene ());
   void StreamDemo (object s, RoutedEventArgs e) => Display (new StreamDemoScene ());
   void AABBTreeDemo (object s, RoutedEventArgs e) => Display (new AABBTreeDemo ());
   void MinSphereDemo (object s, RoutedEventArgs e) => Display (new MinSphereScene ());
   void T3XReaderDemo (object s, RoutedEventArgs e) => Display (new T3XDemoScene ());
   void SliceMeshDemo (object s, RoutedEventArgs e) => Display (new IntMeshPlaneScene ());
   void ConvexHullDemo (object sender, RoutedEventArgs e) => Display (new ConvexHullScene ());
   void CollisionDemo (object s, RoutedEventArgs e) => Display (new CollisionScene ());

   void Display (Scene scene) {
      mSettings.Children.Clear ();
      Lux.UIScene = scene;
      if (scene is RobotScene rs) rs.CreateUI (mSettings.Children);
      if (scene is STPScene or T3XDemoScene) Lux.BackFacesPink = true;
   }
}
