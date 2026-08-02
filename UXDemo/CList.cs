// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CList.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace UXDemo;

public interface ICustomList {
   public int Count { get; }
   public int MeasureY (int item, int xAvailable);
   public void Draw (int item, RectS rect);
   public object Dispose (int item);
}

class BToolVNode : VNode {
   public BToolVNode (int zLevel, Poly poly, Bound2 bound, RectS rect) {
      mZLevel = zLevel;
      mPoly = poly; mBound = bound.InflatedF (1.2); mRect = rect;
   }
   readonly Poly mPoly;
   readonly Bound2 mBound;
   readonly int mZLevel;

   public RectS Rect {
      get => mRect;
      set => mRect = value; 
   }
   RectS mRect;

   public override void SetAttributes () {
      Lux.ZLevel = mZLevel + 1;
      Lux.SetDirectXfm (mBound, mRect);
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

   public override void Draw () {
      Lux.Poly (mPoly);
   }
}

class BToolList : ICustomList {
   public BToolList () 
      => mData = [.. Directory.GetFiles ("W:\\AllBendTools", "*.dxf").Take (50).Select (a => new Data (a))];
   List<Data> mData;

   public int Count => mData.Count;

   public void Draw (int item, RectS rect) {
      var data = mData[item];
      Lux.Color = Color4.Gray (224);
      Lux.RRect (rect, 5);

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

   public object Dispose (int item) 
      => throw new NotImplementedException ();

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
