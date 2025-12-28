using Nori;
namespace SurfLab;

class SurfScene : Scene3 {
   public SurfScene (string file) {
      mModel = new T3XReader (file).Load ();
      mModel.Ents.RemoveIf (a => !Include (a));

      BgrdColor = new (96, 128, 160);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It, mPlus, mNormal, mUnloft2]);

      mHooks = HW.MouseMoves.Subscribe (OnMouseMove);
   }
   Model3 mModel;
   IDisposable mHooks;
   PlusMarkerVN mPlus = new (Color4.Blue);
   CrossMarkerVN mCross = new (Color4.Red);
   NormalVN mNormal = new (5);
   MeshLineVN mMeshVN = new ();
   UnloftTracker2 mUnloft2 = new ();

   public override void Picked (object obj) {
      if (obj is E3Surface surf) {
         string s = surf.ToString ();
         if (surf.IsNormalFlipped) s += " Flip";
         Lib.Trace (s);
      }
   }

   bool Include (Ent3 e) {
      //e.IsNormalFlipped = !e.IsNormalFlipped;
      //return e.Id == 71;
      return true;
   }

   public override void Detached () => mHooks.Dispose ();

   // Called when the mouse is moving
   void OnMouseMove (Vec2S pt) {
      if (!HW.IsDragging) {
         if (Lux.Pick (pt)?.Obj is E3Surface e3s) {
            mMeshVN.Mesh = e3s.Mesh;
            Point3 pt3d = Lux.PickPos;
            mPlus.Pt = pt3d;
            Point2 uv = e3s.GetUV (pt3d);
            Point3 ptLoft = e3s.GetPoint (uv.X, uv.Y);
            Vector3 vecNorm = e3s.GetNormal (uv.X, uv.Y);
            mNormal.Ray = (ptLoft, vecNorm);
         } else
            mPlus.Pt = mCross.Pt = Point3.Nil;
      }
   }
}

class UnloftTracker2 : VNode {
   public UnloftTracker2 () {
      NoPicking = true;
      DisposeOnDetach (SurfaceUnlofter.NewTile.Subscribe (OnChanged));
   }

   void OnChanged (SurfaceUnlofter un) {
      (mLines, mPoints, mLabels) = un.GetTileOutlines ();
      Redraw ();
   }
   List<Vec3F> mLines = [], mPoints = [];
   List<SurfaceUnlofter.Label> mLabels = [];

   public override void SetAttributes () { 
      Lux.PointSize = 6f; 
      Lux.Color = Color4.DarkBlue; 
   }

   public override void Draw () {
      Lux.Lines (mLines.AsSpan ());
      Lux.Points (mPoints.AsSpan ());
      //foreach (var lab in mLabels)
      //   Lux.Text3D (lab.Text, lab.Pos, ETextAlign.MidCenter, Vec2S.Zero);
   }
}

class NormalVN : VNode {
   public NormalVN (double len) => (NoPicking, mLen) = (true, len);
   readonly double mLen;

   public (Point3 Pos, Vector3 Vec) Ray {
      set {
         mPts.Clear ();
         mPts.Add ((Vec3F)value.Pos);
         mPts.Add ((Vec3F)(value.Pos + value.Vec * mLen));
         Redraw ();
      }
   }
   List<Vec3F> mPts = [];

   public override void SetAttributes () {
      Lux.Color = Color4.Red;
      Lux.LineWidth = 4f;
   }

   public override void Draw () => Lux.Lines (mPts.AsSpan ());
}

class MeshLineVN : VNode {
   public MeshLineVN () => NoPicking = true;
   public Mesh3 Mesh { set { if (mMesh != value) { mMesh = value; Redraw (); } } }
   Mesh3? mMesh;

   public override void SetAttributes () {
      Lux.Color = Color4.DarkGreen;
      Lux.LineWidth = 1.2f;
   }

   public override void Draw () {
      if (mMesh == null) return;
      List<Vec3F> pts = [];
      var verts = mMesh.Vertex; var tris = mMesh.Triangle;
      for (int i = 0; i < tris.Length; i += 3) {
         Add (tris[i], tris[i + 1]);
         Add (tris[i + 1], tris[i + 2]);
         Add (tris[i + 2], tris[i]);
      }
      Lux.Lines (pts.AsSpan ());

      void Add (int a, int b) {
         if (a < b) { 
            pts.Add ((Vec3F)verts[a].Pos); 
            pts.Add ((Vec3F)verts[b].Pos); 
         }
      }
   }
}

class PlusMarkerVN : VNode {
   public PlusMarkerVN (Color4 color) => (mColor, Streaming) = (color, true);
   readonly Color4 mColor;

   public Point3 Pt { set { mPt = (Vec3F)value; Redraw (); } }
   Vec3F mPt;

   public override void SetAttributes () {
      Lux.Color = mColor;
      Lux.LineWidth = 2f;
   }
   
   public override void Draw () {
      float a = 1, x = mPt.X, y = mPt.Y, z = mPt.Z;
      Lux.Lines ([new Vec3F (x - a, y, z), new (x + a, y, z), 
                        new (x, y - a, z), new (x, y + a, z), 
                        new (x, y, z - a), new (x, y, z + a)]);
   }
}

class CrossMarkerVN : VNode {
   public CrossMarkerVN (Color4 color) => (mColor, Streaming) = (color, true);
   readonly Color4 mColor;

   public Point3 Pt { set { mPt = (Vec3F)value; Redraw (); } }
   Vec3F mPt;

   public override void SetAttributes () {
      Lux.Color = mColor;
      Lux.LineWidth = 3f;
   }

   public override void Draw () {
      float a = 0.7f, x = mPt.X, y = mPt.Y, z = mPt.Z;
      Lux.Lines ([new Vec3F (x - a, y - a, z - a), new (x + a, y + a, z + a),
                        new (x - a, y + a, z - a), new (x + a, y - a, z + a),
                        new (x - a, y - a, z + a), new (x + a, y + a, z - a)]);
   }
}
