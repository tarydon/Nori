using System;
using System.Collections.Generic;
using System.Text;
namespace Nori;

partial class Triangulator {
   /// <summary>Returns a debug drawing showing the current state of the triangulation</summary>
   public Dwg2 GetDebugDwg () {
      Dwg2 dwg = new ();
      dwg.Add (new Layer2 ("VERTTEXT", Color4.DarkGreen, ELineType.Continuous));
      dwg.Add (new Style2 ("STD", "SIMPLEX", 0, 1, 0));
      dwg.CurrentLayer = dwg.Layers[^1];
      double size = mBound.Height / 100;
      for (int i = 1; i < mVN - 4; i++) {
         ref Vertex v = ref mV[i];
         List<int> tiles = [v.Tile[0], v.Tile[1]]; tiles.RemoveIf (a => a == 0);
         string text = $"{v.Kind.ToString ()[0]}{v.Id}";
         if (tiles.Count > 0) text += $"/{tiles.ToCSV ()}";
         if (mMerged) text = $"{v.Id}";
         var align = v.Kind switch { EVertex.Mountain => ETextAlign.BotCenter, EVertex.Valley => ETextAlign.TopCenter, _ => ETextAlign.MidLeft };
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, v.Pt, size, 0, 0, 1, align));
      }

      if (mTriangles.Count > 0) {
         dwg.Add (new Layer2 ("TRIANGLES", Color4.Blue, ELineType.Continuous));
         dwg.CurrentLayer = dwg.Layers[^1];
         List<Point2> pts = [];
         for (int i = 0; i < mTriangles.Count; i += 3) {
            for (int j = 0; j < 3; j++) {
               ref Vertex v = ref mV[mTriangles[i + j] + 1];
               pts.Add (v.Pt);
            }
            dwg.Add (Poly.Lines (pts, true));
            Point2 cen = (pts[0] + pts[1] + pts[2]) * 0.3333;
            dwg.Add (new E2Point (dwg.CurrentLayer, cen));
            pts.Clear (); 
         }
      } else {
         dwg.Add (new Layer2 ("TILE", Color4.Red, ELineType.Continuous));
         dwg.CurrentLayer = dwg.Layers[^1];
         for (int i = 1; i < mTN; i++) {
            ref var t = ref mT[i];
            if (t.Id == 0) continue;
            dwg.Add (Poly.Lines (Point2.List (t.LMin, t.YMin, t.RMin, t.YMin, t.RMax, t.YMax, t.LMax, t.YMax), true));
         }

         dwg.Add (new Layer2 ("TILETEXT", Color4.Blue, ELineType.Continuous));
         dwg.CurrentLayer = dwg.Layers[^1];
         for (int i = 1; i < mTN; i++) {
            ref Tile t = ref mT[i]; if (t.Id == 0) continue;
            Point2 pos = new (0.75.Along (t.LMin, t.RMin), t.YMin);
            //if (mMerged && !mDiagTiles.Contains (t.Id)) continue;
            dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], t.ToString (), pos, size, 0, 0, 1, ETextAlign.BotCenter));
         }

         dwg.Add (new Layer2 ("LINKS", Color4.Blue, ELineType.Continuous));
         dwg.CurrentLayer = dwg.Layers[^1];
         for (int i = 1; i < mTN; i++) {
            ref Tile t = ref mT[i];
            for (int j = 0; j < 2; j++) {
               if (t.Top[j] > 0) AddArrow (GetCommon (ref mT[t.Top[j]], ref t), true, size);
               if (t.Bot[j] > 0) AddArrow (GetCommon (ref t, ref mT[t.Bot[j]]), false, size);
            }
         }
      }
      return dwg;

      // Helpers ...........................................
      void AddArrow (Point2 p, bool up, double size) {
         if (p.IsNil) return;
         double d = size * 0.5;
         Poly poly = Poly.Lines (Point2.List (0, -size, 0, size, d / 2, size - d, -d / 2, size - d, 0, size), false);
         if (!up) poly *= Matrix2.VMirror;
         poly *= Matrix2.Translation (p.X, p.Y);
         dwg.Add (poly);
      }

      Point2 GetCommon (ref Tile t0, ref Tile t1) {
         if (t0.Id == 0 || t1.Id == 0) return Point2.Nil;
         Check (t0.YMin.EQ (t1.YMax));
         double x0 = mS[t0.Left].GetX (t0.YMin), x1 = mS[t0.Right].GetX (t0.YMin);
         Bound1 b0 = new (x0, x1);
         x0 = mS[t1.Left].GetX (t0.YMin); x1 = mS[t1.Right].GetX (t0.YMin);
         Bound1 b1 = new (x0, x1);
         double x = (b0 * b1).Mid;
         return new (x, t0.YMin);
      }
   }
}
