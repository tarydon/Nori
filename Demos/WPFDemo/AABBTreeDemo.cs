using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using Nori;
namespace WPFDemo;

class AABBTreeDemo : Scene3 {
   public AABBTreeDemo () {
      var zar = new ZipArchive (File.OpenRead ("N:/Demos/Data/cow.zip"));
      var ze = zar.GetEntry ("cow.obj")!;
      var zstm = new ZipReadStream (ze.Open (), ze.Length);
      var mesh = Mesh3.LoadObj (zstm.ReadAllLines ());
      mesh *= Matrix3.Rotation (EAxis.X, Lib.HalfPI) * Matrix3.Rotation (EAxis.Z, -Lib.HalfPI);
      var cmesh = CMeshBuilder.Build (mesh);

      mCMeshVN = new CMeshVN (cmesh);
      var meshVN = new MeshVN (mesh) {
         Shading = EShadeMode.Flat,
         Color = new Color4 (128, 128, 128)
      };
      Lib.Tracer = TraceVN.Print;
      Root = new GroupVN ([meshVN, mCMeshVN, TraceVN.It]);
      BgrdColor = Color4.Gray (64);
      Bound = mesh.Bound;
      Lib.Trace ("Right Click: Increase Level");
      Lib.Trace ("Shift+Right Click: Decrease Level");
   }

   CMeshVN mCMeshVN;
}

class CMeshVN : VNode {
   public CMeshVN (CMesh cm) {
      mCM = cm;
      mDisp = HW.MouseClicks.Where (a => a.IsPress && a.Button == EMouseButton.Right).Subscribe (OnMouse);
   }
   public readonly CMesh mCM;
   readonly IDisposable mDisp;

   public override void OnDetach () => mDisp.Dispose ();

   void OnMouse (MouseClickInfo mi) {
      if (mi.Modifier == EKeyModifier.Shift) { mLevel--; Redraw (); } else { mLevel++; Redraw (); }
   }

   public override void SetAttributes () {
      Lux.Color = Color4.White;
      Lux.LineWidth = 2f;
   }

   public override void Draw () {
      var boxes = mCM.EnumBoxes ().Where (a => a.Level == mLevel).ToList ();
      Lib.Trace ($"Level {mLevel}, {boxes.Count} boxes");
      List<Vec3F> pts = [];
      foreach (var (box0, level) in boxes) {
         var box = box0.InflatedF (1);
         var (x, y, z) = (box.X, box.Y, box.Z);
         Vec3F a = new (x.Min, y.Min, z.Min), b = new (x.Max, y.Min, z.Min);
         Vec3F c = new (x.Max, y.Max, z.Min), d = new (x.Min, y.Max, z.Min);
         Vec3F e = new (x.Min, y.Min, z.Max), f = new (x.Max, y.Min, z.Max);
         Vec3F g = new (x.Max, y.Max, z.Max), h = new (x.Min, y.Max, z.Max);
         pts.AddRange ([a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h]);
      }
      Lux.Lines (pts.AsSpan ());
   }
   int mLevel = 6;
}
