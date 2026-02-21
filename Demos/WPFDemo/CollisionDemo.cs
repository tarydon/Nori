// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CollisionDemo.cs
// ║║║║╬║╔╣║ Demonstrates the collision detections between two meshes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace WPFDemo;
using Nori;
using System.Diagnostics;
using System.Reactive.Linq;

// Demostrates collision between a main model 'A' with multiple collision models 'Bs'.
// It also demostrates displaying the OBB BVH for the underlying models.
class CollisionScene : Scene3 {
   // Number of collision objects
   const int COBJ = 150;
   readonly Random R = new ();

   public CollisionScene () {
      Lib.Tracer = TraceVN.Print;
      TraceVN.It.Clear ();
      
      // Load collision models and create entities
      var mesh = Mesh3.LoadTMesh ("N:/TData/IO/MESH/cow.tmesh");
      // The mesh is normalized below to a constant scale and origin. So any mesh can be
      // plugged to continue the collision testing. Uncomment the line below to experiment.
      // mesh = Mesh3.LoadTMesh ("N:/TData/IO/MESH/cow.tmesh");
      var vecC = (Vector3)mesh.Bound.Midpoint;
      mesh *= Matrix3.Translation (-vecC) * Matrix3.Scaling (5 / mesh.Bound.Diagonal); // Normalize main mesh
      A = new (new (mesh), Matrix3.Rotation (EAxis.Z, -Math.PI / 8).ToCS ());

      BModels = [.. Enumerable.Range (1, 5).Select (n => new CModel (Mesh3.Sphere (Point3.Zero, 0.05 + 0.01 * n, 0.01)))];
      Bs = [.. Enumerable.Range (0, COBJ).Select (_ => new CEnt (BModels[R.Next (BModels.Length)], CS ()))];
      CheckCollision ();

      // Initialize the scene
      mShortcuts = new SimpleVN (() => (Lux.Color, Lux.TypeFace) = (Color4.White, mFont), RenderShortcuts);
      var root = new GroupVN ([new CEntVN (A) { DrawTree = mDrawTree, IsMainEnt = true }, TraceVN.It, mShortcuts]);
      Bs.Select (b => new CEntVN (b) { Color = new Color4 (128, 255, 128), DrawTree = mDrawTree }).ForEach (root.Add);
      
      Root = root; 
      BgrdColor = Color4.Gray (100);
      Bound = mesh.Bound;

      // Set key bindings
      mDisp = HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey);
   }

   void CheckCollision (bool shuffle = false) {
      if (shuffle) {
         foreach (var b in Bs)
            b.CS = CS ();
      }
      // Collision check: Check A collision with Bs.
      Stopwatch sw = Stopwatch.StartNew ();
      Bs.ForEach (B => B.Collided = Check (A, B));
      sw.Stop ();
      
      Lib.Trace ($"{Bs.Length} objects, {Bs.Count (x => x.Collided)} collide");
      Lib.Trace ($"Elapsed: {sw.Elapsed.TotalMilliseconds:F1} ms");

      static bool Check (CEnt a, CEnt b) => OBBCollider.It.Check (a.Model.CTree, a.CS, b.Model.CTree, b.CS);
   }

   CoordSystem CS () => new (new (Scale () * R.NextDouble (), Scale () * R.NextDouble (), Scale () * R.NextDouble ()));
   double Scale () => R.NextDouble () switch { < 0.5 => -2, _ => 2 };

   public override void Detached () {
      base.Detached ();
      mDisp.Dispose ();
   }
   readonly IDisposable mDisp;

   void OnKey (KeyInfo info) {
      switch (info.Key) {
         case EKey.C: CheckCollision (true); break;
         case EKey.T:
            mDrawTree = !mDrawTree;
            for (int i = 0; ; i++) {
               var child = Root?.GetChild (i);
               if (child == null) break;
               if (child is CEntVN ent) ent.DrawTree = mDrawTree;
            }
            mShortcuts.Redraw ();
            break;
      }
   }

   void RenderShortcuts () {
      string str = "Shortcuts\n   C  =  Check Collisions\n   T  =  Toggle Collision Tree";
      if (mDrawTree) {
         str += "\n   Right Click  =  Increase Tree Level\n   Shift+Right Click  =  Decrease Tree Level";
      }
      var lines = str.Split ('\n');
      int x = 20, y = lines.Length * 22;
      foreach (var line in lines) {
         Lux.TextPx (line, new Vec2S (x, y));
         y -= 22;
      }
   }

   // Private data -------------------------------------------------------------
   CEnt A; CEnt[] Bs = [];
   CModel[] BModels;
   bool mDrawTree = false;
   TypeFace mFont = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), (int)(11 * Lux.DPIScale));
   VNode mShortcuts;
}

// The model type contains geometry and bounding tree
class CModel (Mesh3 mesh) {
   // Model geometry
   readonly public Mesh3 Mesh = mesh;
   // Collision tree
   public OBBTree CTree => mTree ??= new (Mesh);
   OBBTree mTree = null!;
}

// The model instance inserted at a particlar point with orientation
class CEnt (CModel model, CoordSystem cs) {
   readonly public CModel Model = model;
   // Insert position and orientation.
   public CoordSystem CS {
      get => mCS;
      set {
         mCS = value;
         mXfm = null!;
      }
   }
   CoordSystem mCS = cs;
   // The cached transformation matrix from CS.
   public Matrix3 Xfm => mXfm ??= Matrix3.From (CS);
   Matrix3 mXfm = null!;
   // Is this entity colliding?
   public bool Collided = false;
}

// A colliding ball view node. This is used to show random spheres that
// may or may not collide with the main mesh.
class CEntVN (CEnt ent) : VNode (ent) {
   // The entity we are rendering.
   readonly CEnt Ent = ent;
   // The entity color.
   public Color4 Color = Color4.Gray (40);
   // Draw BVH?
   public bool DrawTree {
      get => mDrawTree;
      set {
         mDrawTree = value;
         if (mDrawTree) ChildAdded ();
         else if (mChild != null) {
            ChildRemoved (mChild);
            mChild = null;
         }
      }
   }
   bool mDrawTree;
   // Is this the main entity? Used for setting tree level and logs.
   public bool IsMainEnt = false;

   public override void SetAttributes ()
      => (Lux.Color, Lux.Xfm) = (Ent.Collided ? new Color4 (255, 128, 128) : Color, Ent.Xfm);

   public override void Draw () => Lux.Mesh (Ent.Model.Mesh, EShadeMode.Phong);

   public override VNode? GetChild (int n) =>
      DrawTree && n == 0 ? (mChild ??= new TreeVN (Ent.Model.CTree, IsMainEnt)) : null;
   VNode? mChild;

   // This VNode displays one level of the OBB hierarchy (by drawing boxes).
   // This VNode also connects to the keyboard handler.
   class TreeVN (OBBTree tree, bool master = false) : VNode {
      // Overrides ----------------------------------------------------------------
      public override void Draw () {
         List<OBB> boxes = [.. mTree.EnumBoxes (mLevel)];
         Trace ($"Level {mLevel}, {boxes.Count} boxes. Volume: {boxes.Sum (b => b.Volume):F3}");
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
         mDisp = HW.MouseClicks
            .Where (a => a.IsPress && a.Button == EMouseButton.Right)
            .Subscribe (OnMouse);
      }

      public override void OnDetach () => mDisp?.Dispose ();

      void OnMouse (MouseClickInfo mi) {
         if (mMainMesh) {
            mLevel += (mi.Modifier == EKeyModifier.Shift) ? -1 : 1;
            if (mLevel < -1) mLevel = 0;
         }
         Redraw ();
      }

      public override void SetAttributes () => (Lux.Color, Lux.LineWidth) = (Color4.White, 2f);

      void Trace (string msg) {
         if (!mMainMesh) return;
         Lib.Trace (msg);
      }
      bool mMainMesh = master;

      // Private data -------------------------------------------------------------
      static int mLevel = 2;
      readonly OBBTree mTree = tree;
      IDisposable? mDisp;
   }
}
