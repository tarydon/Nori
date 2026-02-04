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
      Lib.Tracer = TraceVN.Print;
      new SceneManipulator ();
      Lux.UIScene = new OBBScene ();
   }
}

class OBBScene : Scene3 {
   public OBBScene () {
      var model = new T3XReader ("N:/Demos/Data/5X-024-Blank.t3x").Load ();
      model.Ents.ForEach (a => a.IsTranslucent = true);
      Bound = model.Bound;
      Root = new GroupVN ([new Model3VN (model), TraceVN.It, mObbVN, mObb2VN]);
   }
   OBBDisplay mObbVN = new (new ());
   OBB2Display mObb2VN = new (new ());

   public override void Picked (object obj) {
      if (obj is E3Surface surf) {
         var pts = surf.Mesh.Vertex.Select (a => (Point3)a.Pos).ToList ();
         Lib.Trace ($"#{surf.Id}, {pts.Count} points");
         var obb = OBB.From (pts.AsSpan ());
         mObbVN.Obb = obb;

         var pts2 = surf.Mesh.Vertex.Select (a => a.Pos).ToList ();
         var obb2 = OBB2.FromPCA (pts2.AsSpan ());
         mObb2VN.Obb = obb2;
      }
   }
}

class OBB2Display : VNode {
   public OBB2Display (OBB2 obb) : base (obb) { mObb = obb; }

   public OBB2 Obb {
      get => mObb;
      set { mObb = value; Redraw (); }
   }
   OBB2 mObb;

   public override void SetAttributes () => Lux.Color = Color4.Yellow;

   public override void Draw () {
      Point3f C = mObb.Cen;
      Vector3f x = mObb.X * mObb.Ext.X;
      Vector3f y = mObb.Y * mObb.Ext.Y;
      Vector3f z = mObb.Z * mObb.Ext.Z;
      Point3f a = C - x - y - z, b = C + x - y - z, c = C + x + y - z, d = C - x + y - z;
      Point3f e = C - x - y + z, f = C + x - y + z, g = C + x + y + z, h = C - x + y + z;
      List<Point3f> p = [a, b, b, c, c, d, d, a, e, f, f, g, g, h, a, e, b, f, c, g, d, h];
      Lux.Lines (p.Select (a => (Vec3F)a).ToList ().AsSpan ());
   }
}

class OBBDisplay : VNode {
   public OBBDisplay (OBB obb) : base (obb) { mObb = obb; }

   public OBB Obb { 
      get => mObb;
      set { mObb = value; Redraw (); }
   }
   OBB mObb;

   public override void SetAttributes () => Lux.Color = Color4.Blue;

   public override void Draw () {
      Point3 C = mObb.Center;
      Vector3 x = mObb.CS.VecX * mObb.Extent.X;
      Vector3 y = mObb.CS.VecY * mObb.Extent.Y;
      Vector3 z = mObb.CS.VecZ * mObb.Extent.Z;
      Point3 a = C - x - y - z, b = C + x - y - z, c = C + x + y - z, d = C - x + y - z;
      Point3 e = C - x - y + z, f = C + x - y + z, g = C + x + y + z, h = C - x + y + z;
      List<Point3> p = [a, b, b, c, c, d, d, a, e, f, f, g, g, h, a, e, b, f, c, g, d, h];
      Lux.Lines (p.Select (a => (Vec3F)a).ToList ().AsSpan ());
   }
}
