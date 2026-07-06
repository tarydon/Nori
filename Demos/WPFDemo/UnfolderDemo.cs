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
   }
   bool mCreated;

   void CreateSubscenes () {
      if (!mCreated) {
         mCreated = true;
         var size = Lux.PanelSize;
         double xG = 12.0 / size.X, yG = 12.0 / size.Y, xM = 0.5, yM = 0.5;
         Lux.AddSubScene (new SurfModelScene (), new (xG, yM + yG / 2, xM - xG / 2, 1 - yG));
         Lux.AddSubScene (new SheetModelScene (), new (xM + xG / 2, yM + yG / 2, 1 - xG, 1 - yG));
         Lux.AddSubScene (new UnfoldScene (), new (xG, yG, 1 - xG, yM - yG / 2));
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
      mModel = new STEPReader (UnfolderDemo.Files[UnfolderDemo.NFile]).Load ();
      Bound = mModel.Bound; ZoomExtents ();
      Root = new GroupVN ([new Model3VN (mModel), new SimpleVN (DrawText) { Streaming = true }]);
   }
   Model3? mModel;

   void DrawText () {
      Lux.Color = Color4.Blue;
      int cy = TypeFace.Default.Measure ("M", false).Height;
      Lux.Text ($"Surface Model ({mModel!.Ents.Count} surfaces)", new (cy, 2 * cy));
      Lux.Text ("Press SpaceBar to go to next model", new (cy, (int)(3.2 * cy)));
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
      mModel = new SheetMetalizer (model).Process ().Value;
      foreach (var flex in mModel.Ents.OfType<E3Flex> ())
         flex.IsSelected = true;
      Bound = mModel.Bound; ZoomExtents ();
      Root = new GroupVN ([new Model3VN (mModel), new SimpleVN (DrawText) { Streaming = true }]);
   }
   Model3? mModel;

   void DrawText () {
      Lux.Color = Color4.Blue;
      int cy = TypeFace.Default.Measure ("M", false).Height;
      Lux.Text ($"Sheet-metal model ({mModel!.Ents.Count} entities)", new (cy, 2 * cy));
      Lux.Text ($"{mModel!.Ents.OfType<E3Flex> ().Count ()} flexes", new (cy, (int)(3.2 * cy)));
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
      mDwg = new Unfolder (shmodel).Process ().Value;
      Bound = mDwg.Bound.InflatedF (1.05); ZoomExtents ();
      Root = new GroupVN ([new Dwg2VN (mDwg), new DwgFillVN (mDwg), new SimpleVN (DrawText) { Streaming = true }]);
   }
   Dwg2? mDwg;

   void DrawText () {
      Lux.Color = Color4.Blue;
      int cy = TypeFace.Default.Measure ("M", false).Height;
      var b = mDwg!.Bound;
      Lux.Text ($"Unfolded drawing (Bound = {b.Width.R3 ()} x {b.Height.R3 ()})", new (cy, 2 * cy));
      Lux.Text ($"{mDwg!.Ents.OfType<E2Bendline> ().Count ()} bends", new (cy, (int)(3.2 * cy)));
   }
}