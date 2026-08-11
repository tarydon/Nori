// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CList.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using Nori;
namespace UXDemo;

public interface ICustomList {
   public int Count { get; }
   public int MeasureY (int item, int xAvailable) => xAvailable;
   public Vec2S Measure (int item) => new (128, 128);
   public void Draw (int item, RectS rect);
   public void Dispose (int item);
   public bool NeedsRemeasure => false;
}

class BToolVNode : VNode {
   public BToolVNode (int zLevel, Poly poly, Bound2 bound, RectS rect) {
      mZLevel = zLevel;
      mPoly = poly; mBound = bound.InflatedF (1.2); Rect = rect;
   }
   readonly Poly mPoly;
   readonly Bound2 mBound;
   readonly int mZLevel;
   public RectS Rect;

   public override void SetAttributes () {
      Lux.ZLevel = mZLevel + 1;
      Lux.SetDirectXfm (mBound, Rect);
      Lux.LineWidth = 1.5f;
      Lux.Color = Color4.Black;
   }

   public override VNode? GetChild (int n) {
      if (n != 0) return null;
      if (mFillVN == null) {
         Dwg2 dwg = new (); dwg.Add (mPoly);
         mFillVN = new DwgFillVN (dwg) { ZLevel = mZLevel };
      }
      return mFillVN;
   }
   DwgFillVN? mFillVN;

   public override void OnAttach () {
      cTotal++; cAlive++;
      StatsVN.Add ("BToolVN:", $"{cAlive}/{cTotal}");
   }

   public override void OnDetach () {
      cAlive--;
      StatsVN.Add ("BToolVN:", $"{cAlive}/{cTotal}");
   }
   static int cAlive, cTotal;

   public override void Draw () {
      Lux.Poly (mPoly);
   }
}

class BToolList : ICustomList {
   public BToolList () {
      mData = [.. Directory.GetFiles ("W:\\AllBendTools", "*.dxf").Take (100).Select (a => new Data (a))];
      mLast = DateTime.Now;
   }
   List<Data> mData;
   static DateTime mLast;

   public int Count => mData.Count;

   public static bool AnimateScale;

   public static double Scale {
      get {
         if (AnimateScale) {
            double ts = (DateTime.Now - mLast).TotalSeconds;
            mAng += ts * 0.4;
            mLast = DateTime.Now;
            return 1.2 * Math.Sin (mAng) + 2.5;
         } else
            return 2.5;
      }
   }
   static double mAng = 0;

   public bool NeedsRemeasure => true;

   public void Draw (int item, RectS rect) {
      var data = mData[item];
      Lux.Color = rect.Contains (UXSystem.MousePos) ? Color4.Gray (160) : Color4.Gray (216);
      Lux.RRect (rect, 5);
      if (item == 0 && !AnimateScale && Hub.Keyboard.IsShiftDown) {
         AnimateScale = true;
         mLast = DateTime.Now;
      }

      if (data.VNode == null) {
         data.VNode = new BToolVNode (Lux.ZLevel + 1, data.Poly, data.Bound, rect);
         UXSystem.RetainedVN.Add (data.VNode);
      } else
         data.VNode.Rect = rect;
   }

   public int MeasureY (int item, int xAvailable) {
      var bound = mData[item].Bound;
      double scale = xAvailable / bound.Width;
      return (int)(Math.Min (scale * bound.Height + 0.5, xAvailable * 3));
   }

   public Vec2S Measure (int item) {
      var bound = mData[item].Bound;
      if (item == 0) StatsVN.Add ("Scale", Scale.R3 ());
      return new Vec2S ((short)(bound.Width * Scale + 0.5), (short)(bound.Height * Scale + 0.5));
   }

   public void Dispose (int item) {
      Data d = mData[item];
      if (d.VNode is { } vn) {
         UXSystem.QueueForDelete.Add (vn);
         d.VNode = null;
      }
   }

   class Data {
      public Data (string name) { Name = name; }
      public string Name;

      public Poly Poly {
         get {
            if (mPoly == null) {
               mPoly = DXFReader.Load (Name).Polys.MaxBy (a => a.GetBound ().Area)!;
               mPoly = mPoly.DiscretizeP (ETess.Coarse);
               mBound = mPoly.GetBound ();
            }
            return mPoly;
         }
      }        
      Poly? mPoly;

      public Bound2 Bound { get { _ = Poly; return mBound; } }
      Bound2 mBound;

      public BToolVNode? VNode;
   }
}

class ModelVNode : VNode {
   public ModelVNode (int zLevel, Mesh3 mesh, RectS rect) {
      mZLevel = zLevel; mMesh = mesh;
      mColor = Color4.RandomLight;
      Rect = rect;
      mLast = DateTime.Now;
   }
   readonly int mZLevel;
   readonly Mesh3 mMesh;
   readonly Color4 mColor;
   double mZRot = 45;
   DateTime mLast;

   public override void OnAttach () {
      ++cAlive; ++cTotal;
      StatsVN.Add ("ModelVN", $"{cAlive}/{cTotal}");
   }

   public override void OnDetach () {
      --cAlive;
      StatsVN.Add ("ModelVN", $"{cAlive}/{cTotal}");
   }
   static int cAlive, cTotal;

   public override void SetAttributes () {
      Lux.ZLevel = mZLevel + 1;
      double ts = (DateTime.Now - mLast).TotalSeconds;
      if (Rect.Contains (UXSystem.MousePos)) mSpeed = 100;
      else mSpeed = Math.Max (mSpeed - ts / 0.03, 0);
      mZRot += ts * mSpeed;
      Lux.SetDirectXfm (mMesh.Bound, Rect, Quaternion.FromAxisRotations (-60.D2R (), 0, mZRot.D2R ()));
      Lux.LineWidth = 1.5f;
      Lux.Color = Color4.White;
      mLast = DateTime.Now;
   }
   double mSpeed = 0; 

   public override void Draw () {
      Lux.Mesh (mMesh, EShadeMode.Flat);
   }

   public RectS Rect { get; set; }
}

class ModelList : ICustomList {
   public ModelList ()
      => mData = [.. Directory.GetFiles ("W:\\STEP-SheetMetal", "*.stp").Take (100).Select (a => new Data (a))];
   List<Data> mData;

   public int Count => mData.Count;

   class Data {
      public Data (string name) { Name = name; }
      public readonly string Name;

      public Mesh3 Mesh {
         get {
            if (mMesh == null) {
               Ent3.MeshQuality = ETess.Coarse;
               var model = STEPReader.Load (Name);
               mMesh = new Mesh3 (model.Ents.OfType<E3Surface> ().Select (a => a.Mesh));
            }
            return mMesh;
         }
      }
      Mesh3? mMesh;

      public ModelVNode? VNode;
   }

   public int MeasureY (int item, int xAvailable) => xAvailable;

   public Vec2S Measure (int item) => new (270, 270);

   public void Draw (int item, RectS rect) {
      var data = mData[item];
      Lux.Color = rect.Contains (UXSystem.MousePos) ? Color4.Gray (160) : Color4.Gray (216);
      Lux.RRect (rect, 5);

      if (data.VNode == null) {
         data.VNode = new ModelVNode (Lux.ZLevel + 1, data.Mesh, rect);
         UXSystem.RetainedVN.Add (data.VNode);
      } else
         data.VNode.Rect = rect;
   }

   public void Dispose (int item) {
      Data d = mData[item];
      if (d.VNode is { } vn) {
         UXSystem.QueueForDelete.Add (vn);
         d.VNode = null;
      }
   }
}
