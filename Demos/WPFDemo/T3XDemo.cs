using Nori;
namespace WPFDemo;

class T3XDemoScene : Scene3 {
   public T3XDemoScene () {
      var blank = new T3XReader ("N:/Demos/Data/5x-043-blank.t3x").Load ();
      var part = mModel = new T3XReader ("N:/Demos/Data/5x-043.t3x").Load ();
      foreach (var ent in blank.Ents) ent.IsTranslucent = true;
      TraceVN.It.Clear ();
      Lib.Tracer = TraceVN.Print;
      BgrdColor = new Color4 (80, 84, 88);
      Bound = blank.Bound;
      Root = new GroupVN ([new Model3VN (blank), new Model3VN (part), TraceVN.It, mHairs]);
   }
   NormalVN mHairs = new ();
   Model3? mModel;

   public override void Picked (object obj) {
      if (!HW.IsShiftDown)
         mModel!.Ents.ForEach (a => a.IsSelected = false);
      if (obj is E3Surface ent) {
         Lib.Trace ($"Picked: {ent.GetType ().Name} #{ent.Id}");
         ent.IsSelected = true;
         mHairs.Surface = ent;
      }
   }
}

class NormalVN : VNode {
   public NormalVN () { }

   public E3Surface? Surface {
      get => mSurface;
      set { mSurface = value; Redraw (); }
   }
   E3Surface? mSurface;

   public override void SetAttributes () {
      Lux.Color = Color4.Yellow;
      Lux.PointSize = 8f;
   }

   public override void Draw () {
      if (Surface != null) {
         List<Vec3F> pts = [];
         List<Vec3F> bots = [];
         var mesh = Surface.Mesh;
         var nodes = mesh.Vertex; var tris = mesh.Triangle;
         for (int i = 0; i < tris.Length; i += 3) {
            Mesh3.Node na = nodes[tris[i]], nb = nodes[tris[i + 1]], nc = nodes[tris[i + 2]];
            Point3 mid = ((Point3)na.Pos + (Point3)nb.Pos + (Point3)nc.Pos) * (1 / 3.0);
            Vector3 vec = ((Vector3)na.Vec + (Vector3)nb.Vec + ((Vector3)nc.Vec)).Normalized ();
            pts.Add (mid); pts.Add (mid + vec * 3);
            bots.Add (mid);
         }
         Lux.Lines (pts.AsSpan ());
         Lux.Points (bots.AsSpan ());
      }
   }
}
