using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Nori;
namespace WPFDemo;

class UnfolderDemo : Scene2 {
   public UnfolderDemo () {
      Lib.Tessellate = Tess2D.Process;
      BgrdColor = new Color4 (64, 68, 72);
      Root = new UBaseVN ();
      Files = Directory.GetFiles ($"{Lib.DevRoot}/TData/STEP", "S*.stp");
      mDisp = Hub.Keyboard.Keys.Where (a => a.IsPress (EKey.Space)).Subscribe (NextFile);
   }
   IDisposable mDisp;

   public override void Detached () => mDisp.Dispose ();

   void NextFile (KeyInfo key) {
      NFile = (NFile + 1) % Files.Length;
      FileChanged.OnNext (Unit.Default);
   }

   public static readonly Subject<Unit> FileChanged = new ();

   public static string[] Files = [];
   public static int NFile;
}

class UBaseVN : VNode {
   public UBaseVN () => Streaming = true;

   public override void Draw () {
      if (!mCreated) CreateSubscenes ();
      else {
         List<Vec2F> a = [];
         foreach (var scene in Lux.SubScenes) {
            var r = scene.Rect;
            for (int i = 1; i < 7; i++) {
               int x0 = r.Left - i, x1 = r.Right + i - 1, y0 = r.Top - i, y1 = r.Bottom + i;
               Add (x0, y0); Add (x1, y0, 2); Add (x1, y1, 2); Add (x0, y1, 2); Add (x0, y0);
            }
         }
         Lux.Color = new Color4 (144, 148, 152);

         // Helpers .......................................
         void Add (int x, int y, int n = 1) { for (int i = 0; i < n; i++) a.Add (new (x, y)); }
      }
   }
   bool mCreated;

   void CreateSubscenes () {
      if (!mCreated) {
         mCreated = true;
         var size = Lux.PanelSize;
         double xGutter = 12.0 / size.X, yGutter = 12.0 / size.Y, xMid = 0.5, yMid = 0.5;

         Lux.AddSubScene (new SheetModelScene (), new (xGutter, yGutter, xMid - xGutter / 2, yMid - yGutter / 2));
         Lux.AddSubScene (new SurfModelScene (), new (xGutter, yMid + yGutter / 2, xMid - xGutter / 2, 1 - yGutter));
         Lux.AddSubScene (new UnfoldScene (), new (xMid + xGutter / 2, yGutter, 1 - xGutter, 1 - yGutter));
         Lib.Post (Redraw);
      }
   }
}

class SurfModelScene : Scene3 {
   public SurfModelScene () {
      BgrdColor = new Color4 (192, 196, 200);
      mDisp = UnfolderDemo.FileChanged.Subscribe (_ => Draw ());
      Draw ();
   }
   public override void Detached () => mDisp.Dispose ();
   IDisposable mDisp;

   public void Draw () {
      var model = new STEPReader (UnfolderDemo.Files[UnfolderDemo.NFile]).Load ();
      Bound = model.Bound; ZoomExtents (); 
      Root = new Model3VN (model);
   }
}

class SheetModelScene : Scene3 {
   public SheetModelScene () {
      BgrdColor = new Color4 (192, 196, 200);
      mDisp = UnfolderDemo.FileChanged.Subscribe (_ => Draw ());
      Draw ();
   }
   public override void Detached () => mDisp.Dispose ();
   IDisposable mDisp;

   public void Draw () {
      var model = new STEPReader (UnfolderDemo.Files[UnfolderDemo.NFile]).Load ();
      var shmodel = new SheetMetalizer (model).Process ().Value;
      foreach (var flex in shmodel.Ents.OfType<E3Flex> ())
         flex.IsSelected = true;
      Bound = shmodel.Bound; ZoomExtents ();
      Root = new Model3VN (shmodel);
   }
}

class UnfoldScene : Scene2 {
   public UnfoldScene () {
      BgrdColor = new Color4 (192, 196, 200);
      mDisp = UnfolderDemo.FileChanged.Subscribe (_ => Draw ());
      Draw ();
   }
   public override void Detached () => mDisp.Dispose ();
   IDisposable mDisp;

   public void Draw () {
      var model = new STEPReader (UnfolderDemo.Files[UnfolderDemo.NFile]).Load ();
      var shmodel = new SheetMetalizer (model).Process ().Value;
      var dwg = new Unfolder (shmodel).Process ().Value;
      Bound = dwg.Bound.InflatedF (1.05); ZoomExtents ();
      Root = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg)]);
   }
}