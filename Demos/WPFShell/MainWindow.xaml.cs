using Nori;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Windows;

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
      Lux.UIScene = new OBBTreeDemo ();
   }
}

// This implements a demo scene for showing a BVH (bounding volume hierarchy) made up of
// AABBs (axis-aligned bounding boxes). We load a mesh from an OBJ file, and then create
// a BVH that drills down to the level of individual triangles with the 
class OBBTreeDemo : Scene3 {
   public OBBTreeDemo () {
      var mesh = Mesh3.LoadTMesh ("N:/Demos/Data/suzanne.tmesh");
      Lib.Tracer = TraceVN.Print;
      TraceVN.It.Clear ();
      OBBTree tree = new (mesh);
      mTreeVN = new OBBTreeVN (tree);
      var meshVN = new MeshVN (mesh) {
         Shading = EShadeMode.Flat,
         Color = new Color4 (128, 128, 128)
      };
      Root = new GroupVN ([meshVN, mTreeVN, TraceVN.It]);
      BgrdColor = Color4.Gray (64);
      Bound = mesh.Bound;
      Viewpoint = new (-65, 90);
      Lib.Trace ("Right Click: Increase Level");
      Lib.Trace ("Shift+Right Click: Decrease Level");
   }

   OBBTreeVN mTreeVN;
}

// This VNode displays one level of the OBB hierarchy (by drawing boxes).
// This VNode also connects to the keyboard handler.
class OBBTreeVN (OBBTree tree) : VNode {
   // Overrides ----------------------------------------------------------------
   public override void Draw () {
      List<OBB> boxes = [..mTree.EnumBoxes (mLevel)];
      Lib.Trace ($"Level {mLevel}, {boxes.Count} boxes. Volume: {boxes.Sum (b => b.Volume):F3}, Area: {boxes.Sum (b => b.Area):F3}");
      List<Vec3F> pts = [];
      foreach (var box in boxes) {
         var (x, y, z) = (box.X * box.Extent.X, box.Y * box.Extent.Y, box.Z * box.Extent.Z);
         Vec3F a = box.Center - x - y - z, b = box.Center - x - y + z;
         Vec3F c = box.Center - x + y + z, d = box.Center - x + y - z;
         Vec3F e = box.Center + x - y - z, f = box.Center + x - y + z;
         Vec3F g = box.Center + x + y + z, h = box.Center + x + y - z;
         pts.AddRange ([a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h]);
      }
      Lux.Lines (pts.AsSpan ());
   }

   public override void OnAttach ()
      => mDisp = HW.MouseClicks
                   .Where (a => a.IsPress && a.Button == EMouseButton.Right)
                   .Subscribe (OnMouse);

   public override void OnDetach () => mDisp?.Dispose ();

   void OnMouse (MouseClickInfo mi) {
      mLevel += (mi.Modifier == EKeyModifier.Shift) ? -1 : 1;
      Redraw ();
   }

   public override void SetAttributes ()
      => (Lux.Color, Lux.LineWidth) = (Color4.White, 2f);

   // Private data -------------------------------------------------------------
   int mLevel = 3;
   readonly OBBTree mTree = tree;
   IDisposable? mDisp;
}

class MeshVN (Mesh3 mesh) : VNode {
   public override void SetAttributes () { Lux.Color = Color; Lux.LineWidth = 2f; }
   public override void Draw () => Lux.Mesh (mesh, Shading);

   public Color4 Color = new (255, 255, 128);
   public EShadeMode Shading = EShadeMode.Phong;
}
