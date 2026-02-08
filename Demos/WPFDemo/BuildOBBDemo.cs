using Nori;
namespace WPFDemo;

class BuildOBBScene : Scene3 {
   public BuildOBBScene () {
      Random r = new (1);
      var model = new T3XReader ("C:/Etc/T3/5X-022.t3x").Load ();
      mSurfaces = [.. model.Ents.OfType<E3Surface> ().OrderByDescending (a => a.Mesh.GetArea ()).Take (20)];
      mSurfaces.ForEach (a => a.IsTranslucent = a.NoStencil = true);
      Lib.Tracer = TraceVN.Print; TraceVN.TextColor = Color4.Yellow;
      TraceVN.HoldTime = 12;

      Bound3 b = new ();
      List<VNode> vns = [mOBB, mOBBFast, TraceVN.It];
      for (int i = 0; i < mSurfaces.Count; i++) {
         var surface = mSurfaces[i];
         double xR = GetAngle (), yR = GetAngle (), zR = GetAngle ();
         Vector3 mid = (Vector3)surface.Bound.Midpoint;
         var xfm = Matrix3.Translation (mid)
                 * Matrix3.Rotation (EAxis.X, xR) * Matrix3.Rotation (EAxis.Y, yR) * Matrix3.Rotation (EAxis.Z, zR)
                 * Matrix3.Translation (-mid);
         mXfms.Add (xfm);
         vns.Add (new XfmVN (xfm, new E3SurfaceVN (surface)));
         b += surface.Mesh.GetBound (xfm);
      }
      Bound = b;
      Root = new GroupVN (vns);
      BgrdColor = new Color4 (128, 96, 64);
      Lib.Trace ("Click to select, Shift+Click to select more");
      Lib.Trace ("Yellow: OBB, White: OBBFast");

      double GetAngle () => (r.NextDouble () - 0.5) * (90.D2R ());
   }

   public override void Picked (object obj) {
      if (obj is not E3Surface e3s) return;
      if (!HW.IsShiftDown) mSurfaces.ForEach (Deselect);
      e3s.IsSelected = true; e3s.IsTranslucent = e3s.NoStencil = false;

      List<Point3f> pts = [];
      for (int i = 0; i < mSurfaces.Count; i++) {
         if (!mSurfaces[i].IsSelected) continue;
         var xfm = mXfms[i];
         pts.AddRange (mSurfaces[i].Mesh.Vertex.Select (a => a.Pos * xfm));
      }
      Lib.Trace ("");
      Lib.Trace ($"{pts.Count} points");
      using (var bt = new BlockTimer ("OBB.Build"))
         mOBB.OBB = OBB.Build (pts.AsSpan ());
      using (var bt = new BlockTimer ("OBB.BuildFast"))
         mOBBFast.OBB = OBB.BuildFast (pts.AsSpan ());
      double a1 = mOBB.OBB.Area, a2 = mOBBFast.OBB.Area;
      Lib.Trace ($"Area ratio (OBBFast/OBB): {Math.Round (a2 / a1, 2)}");
      a1 = mOBB.OBB.Volume; a2 = mOBBFast.OBB.Volume;
      Lib.Trace ($"Volume ratio (OBBFast/OBB): {Math.Round (a2 / a1, 2)}");

      static void Deselect (E3Surface s) { s.IsSelected = false; s.IsTranslucent = s.NoStencil = true; }
   }

   OBBVNode mOBB = new (OBB.Zero, Color4.Yellow);
   OBBVNode mOBBFast = new (OBB.Zero, Color4.White);
   List<E3Surface> mSurfaces;
   List<Matrix3> mXfms = [];
}

class OBBVNode : VNode {
   public OBBVNode (OBB obb, Color4 color) { mBox = obb; mColor = color; }
   readonly Color4 mColor;

   public OBB OBB { get => mBox; set { mBox = value; Redraw (); } }
   OBB mBox;

   public override void SetAttributes () => Lux.Color = mColor;

   public override void Draw () {
      var bx = mBox;
      Vector3f x = bx.X * bx.Extent.X, y = bx.Y * bx.Extent.Y, z = bx.Z * bx.Extent.Z;
      Point3f C = bx.Center;
      Point3f a = C - x - y - z, b = C + x - y - z, c = C + x + y - z, d = C - x + y - z;
      Point3f e = C - x - y + z, f = C + x - y + z, g = C + x + y + z, h = C - x + y + z;
      List<Point3f> pts = [];
      pts.AddM (a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h);
      mPts.Clear ();
      mPts.AddRange (pts.Select (a => (Vec3F)a));
      Lux.Lines (mPts.AsSpan ());
   }
   List<Vec3F> mPts = [];
}
