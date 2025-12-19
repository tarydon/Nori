using System.Diagnostics;
using Nori;
namespace SurfLab;

class SurfScene : Scene3 {
   public SurfScene (string file) {
      Point3 pa = new Point3 (1, 2, 3);
      double ang = -Lib.QuarterPI;
      Point3 pb = pa.Rotated (EAxis.X, ang);
      Point3 pc = pa.Rotated (EAxis.Y, ang);
      Point3 pd = pa.Rotated (EAxis.Z, ang);

      Point3 pb2 = pa * Matrix3.Rotation (EAxis.X, ang);
      Point3 pc2 = pa * Matrix3.Rotation (EAxis.Y, ang);
      Point3 pd2 = pa * Matrix3.Rotation (EAxis.Z, ang);

      Debug.Assert (pb.EQ (pb2));
      Debug.Assert (pc.EQ (pc2));
      Debug.Assert (pd.EQ (pd2));


      mModel = new T3XReader (file).Load ();
      mModel.Ents.RemoveIf (a => a.Id is not (662 or 620));

      BgrdColor = new (96, 128, 160);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It, mMarker, mMeshVN]);

      mHooks = HW.MouseMoves.Subscribe (OnMouseMove);
   }
   Model3 mModel;
   IDisposable mHooks;
   MarkerVN mMarker = new ();
   MeshLineVN mMeshVN = new ();

   public override void Detached () => mHooks.Dispose ();

   // Called when the mouse is moving
   void OnMouseMove (Vec2S pt) {
      if (!HW.IsDragging) {
         if (Lux.Pick (pt)?.Obj is E3Surface e3s) {
            mMarker.Pt = Lux.PickPos;
            mMeshVN.Mesh = e3s.Mesh;
         } else
            mMarker.Pt = Point3.Nil;
      } 
   }
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

class MarkerVN : VNode {
   public MarkerVN () => Streaming = true;

   public Point3 Pt { get => field; set { field = value; Redraw (); } }

   public override void SetAttributes () { 
      Lux.Color = Color4.Blue;
      Lux.LineWidth = 2f;
   }
   
   public override void Draw () {
      float a = 1;
      float x = (float)Pt.X, y = (float)Pt.Y, z = (float)Pt.Z;
      Lux.Lines ([new Vec3F (x - a, y, z), new (x + a, y, z), 
                  new (x, y - a, z), new (x, y + a, z), 
                  new (x, y, z - a), new (x, y, z + a)]);
   }
}
