// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SubSceneDemo.cs
// ║║║║╬║╔╣║ Creates multiple sub-scenes hosted on top of the main scene
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace WPFDemo;

class SubSceneDemo : Scene2 {
   public SubSceneDemo () {
      BgrdColor = new Color4 (244, 248, 252);
      Root = new BaseVN ();
   }
}

// This VN is the root scene in which the other scenes are housed. 
// This does not draw anything other than the thick 'borders' around the other 3
// scenes. These are drawn using the PxLines shader (drawing lines in pixel-specific
// coordinates) using the Scene.Rect to figure out the extents of the sub-scenes in 
// pixel coordinates. 
class BaseVN : VNode {
   public BaseVN () => Streaming = true;

   public override void Draw () {
      if (!mCreated) CreateSubscenes ();
      else {
         List<Vec2F> a = [];
         var size = Lux.PanelSize;
         foreach (var scene in Lux.SubScenes) {
            var r = scene.Rect;
            for (int i = 1; i < 6; i++) {
               int x0 = r.Left - i, x1 = r.Right + i - 1, y0 = size.Y - r.Top - i - 1, y1 = size.Y - r.Bottom + i - 2;
               Add (x0, y0); Add (x1, y0, 2); Add (x1, y1, 2); Add (x0, y1, 2); Add (x0, y0);
            }
         }
         Lux.Color = new Color4 (144, 148, 152);
         Lux.PxLines (a.AsSpan ());

         // Helpers .......................................
         void Add (int x, int y, int n = 1) { for (int i = 0; i < n; i++) a.Add (new (x, y)); }
      }
   }

   void CreateSubscenes () {
      if (!mCreated) {
         mCreated = true;
         var size = Lux.PanelSize;
         double xGutter = 18.0 / size.X, yGutter = 18.0 / size.Y, xMid = 0.5, yMid = 0.5;

         Color4 color = new (200, 208, 216);
         var dwg = DXFReader.Load ("N:/Demos/Data/Folder/02.dxf");
         Lux.AddSubScene (new DwgSubScene (dwg), new (xGutter, yGutter, xMid, 1 - yGutter));
         Lux.AddSubScene (new ModelSubScene (dwg), new (xMid + xGutter, yGutter, 1 - xGutter, yMid - yGutter));
         Lux.AddSubScene (new FoldSubScene (dwg), new (xMid + xGutter, yMid, 1 - xGutter, 1 - yGutter));
         Lib.Post (Redraw);
      }
   }
   bool mCreated;
}

// The first subscene (using the left of the screen) displays the drawing that we
// are going to fold. The following interactions are supported:
// - Mouse-wheel zooms in/out
// - MiddleClick + Drag pans
// - Ctrl+E 
class DwgSubScene : Scene2 {
   public DwgSubScene (Dwg2 dwg) {
      Bound = dwg.Bound.InflatedF (1.25);
      BgrdColor = new Color4 (232, 236, 240);
      TraceVN.HoldTime = 20; TraceVN.TextColor = Color4.Blue;
      Root = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg, ETess.Medium) { Color = Color4.White }, TraceVN.It]);
      Lib.Tracer = TraceVN.Print;
      Lib.Trace ("Click on planes in bottom right to select them.");
      Lib.Trace ("Use mouse-wheel in any scene to zoom in/out.");
      Lib.Trace ("Use LeftClick + Drag in 3D scenes to rotate.");
      Lib.Trace ("Use MiddleClick + Drag in any scene to pan.");
      Lib.Trace ("Ctrl+E does a zoom-extent of the scene under the cursor.");
   }
}

// The second sub-scene (bottom right) displays the drawing folded to a model, and can 
// be used to test interactions:
class ModelSubScene : Scene3 {
   public ModelSubScene (Dwg2 dwg) {
      var pf = new PaperFolder (dwg);
      if (pf.Process (out mModel)) {
         Bound = mModel.Bound;
         BgrdColor = new Color4 (216, 252, 224);
         Root = new Model3VN (mModel);
      }
   }
   Model3? mModel;

   public override void Picked (object obj) {
      mModel!.Ents.ForEach (a => a.IsSelected = false);
      if (obj is Ent3 ent) ent.IsSelected = true;
   }
}

class FoldSubScene : Scene3 {
   public FoldSubScene (Dwg2 dwg) {
      mDwg = dwg;
      mBends = [.. dwg.Ents.OfType<E2Bendline> ()];
      mAngles = [.. mBends.Select (a => a.Angle)];
      Lux.StartContinuousRender (this, Animate);
      Viewpoint = new (-60, -45);
      BgrdColor = new Color4 (212, 216, 252);
   }

   void Animate (double f) {
      mFoldAng += f * 40; if (mFoldAng > 360) mFoldAng = 0;
      mLie = (1 + Math.Sin (mFoldAng.D2R ())) / 2;
      for (int i = 0; i < mBends.Length; i++)
         mBends[i].Angle = mAngles[i] * mLie;
      if (!new PaperFolder (mDwg).Process (out var model)) return;
      Root = new Model3VN (model);
      Bound = model.Bound;
   }
   Dwg2 mDwg;
   E2Bendline[] mBends;
   double[] mAngles;
   double mFoldAng = -90, mLie;
}
