using Nori;
using System.Diagnostics;
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
      mesh *= Matrix3.Scaling (5 / mesh.Bound.Diagonal); // Scale to normalize

      Lib.Tracer = TraceVN.Print;
      TraceVN.It.Clear ();
      OBBTree tree = new (mesh);
      mTreeVN = new OBBTreeVN (tree, true);
      var meshVN = new MeshVN (mesh) {
         Shading = EShadeMode.Phong,
         Color = new Color4 (128, 128, 128)
      };
      List<VNode> vnodes = [];
      var root = new GroupVN ([meshVN, TraceVN.It]);
      Root = root; AddNodes ();
      BgrdColor = Color4.Gray (64);
      Bound = mesh.Bound;
      Viewpoint = new (-65, 135);
      HW.Keys.Where (a => a.IsPress () && a.Key == EKey.R).Subscribe (_ => AddNodes ());

      void AddNodes () {
         vnodes.ForEach (root.Remove); vnodes.Clear ();
         Random R = new ();
         var cen = mesh.Bound.Midpoint;
         Mesh3[] objs = [..Enumerable.Repeat (0, 50).Select (_ => Mesh3.Sphere (cen + V (), 0.05 + 0.1 * R.NextDouble (), 0.01))];
         Stopwatch sw = Stopwatch.StartNew ();
         OBBTree[] trees = [..objs.Select (o => new OBBTree (o))];
         sw.Stop (); var tsBuild = sw.Elapsed; 
         bool [] collisions = new bool[trees.Length];
         sw.Restart ();
         for (int i = 0; i < trees.Length; i++)
            collisions[i] = Collides (trees[i]);
         sw.Stop (); 
         Lib.Trace ($"{trees.Length} objects, {collisions.Count (x => x)} collide. BVH: {tsBuild.TotalMilliseconds:F0} ms, Collision: {sw.Elapsed.TotalMilliseconds:F1} ms");
         Lib.Trace ("Press 'R' key to run again");
         vnodes.AddRange (objs.Select ((o, n) => new BallVN (o, collisions[n])));
         //vnodes.AddRange (objs.Select ((o, n) => new OBBTreeVN (trees[n])));
         vnodes.ForEach (root.Add);

         Vector3 V () => new (Scale () * R.NextDouble (), Scale () * R.NextDouble (), Scale () * R.NextDouble ());
         double Scale () => R.NextDouble () switch { < 0.5 => -1.5, _ => 1.5 };
      }

      bool Collides (OBBTree treeB) {
         return OBBCollider.It.Check (tree, CoordSystem.World, treeB, CoordSystem.World, true);
         //foreach (var obbA in tree.EnumBoxes (10)) {
         //   foreach (var obbB in treeB.EnumBoxes (10)) {
         //      if (Collision.Check (obbA, obbB))
         //         return true;
         //   }
         //}
         //return false;
      }
   }

   OBBTreeVN mTreeVN;
}

// A colliding ball view node. This is used to show random spheres that
// may or may not collide with the main mesh.
class BallVN (Mesh3 mesh, bool collides) : VNode {
   readonly bool Collides = collides; 
   readonly Mesh3 Mesh = mesh;
   public override void SetAttributes () => Lux.Color = Collides ? new Color4 (255, 128, 128) : new Color4 (128, 255, 128);
   public override void Draw () => Lux.Mesh (Mesh, EShadeMode.Phong);
}

// This VNode displays one level of the OBB hierarchy (by drawing boxes).
// This VNode also connects to the keyboard handler.
class OBBTreeVN (OBBTree tree, bool trace = false) : VNode {
   // Overrides ----------------------------------------------------------------
   public override void Draw () {
      List<OBB> boxes = [..mTree.EnumBoxes (mLevel)];
      Trace ($"Level {mLevel}, {boxes.Count} boxes. Volume: {boxes.Sum (b => b.Volume):F3}, Area: {boxes.Sum (b => b.Area):F3}");
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

   public override void OnAttach () {
      Trace ("Right Click: Increase Level");
      Trace ("Shift+Right Click: Decrease Level");
      mDisp = HW.MouseClicks
         .Where (a => a.IsPress && a.Button == EMouseButton.Right)
         .Subscribe (OnMouse);
   }

   public override void OnDetach () => mDisp?.Dispose ();

   void OnMouse (MouseClickInfo mi) {
      mLevel += (mi.Modifier == EKeyModifier.Shift) ? -1 : 1;
      Redraw ();
   }

   public override void SetAttributes () => (Lux.Color, Lux.LineWidth) = (Color4.White, 2f);

   void Trace (string msg) {
      if (!mTrace) return;
      Lib.Trace (msg);
   }
   bool mTrace = trace;

   // Private data -------------------------------------------------------------
   int mLevel = 2;
   readonly OBBTree mTree = tree;
   IDisposable? mDisp;
}

class MeshVN (Mesh3 mesh) : VNode {
   public override void SetAttributes () { Lux.Color = Color; Lux.LineWidth = 2f; }
   public override void Draw () => Lux.Mesh (mesh, Shading);

   public Color4 Color = new (255, 255, 128);
   public EShadeMode Shading = EShadeMode.Phong;
}
