using System;
using System.Collections.Generic;
using System.Text;

namespace Nori;

public partial class Triangulator {
   /// <summary>
   /// Returns a debug drawing
   /// </summary>
   public Dwg2 GetDebugDwg () {
      Dwg2 dwg = new ();
      for (int i = 0; i < mSN; i++)
         if (!mS[i].Diagonal) dwg.Add (Poly.Line (mS[i].PA, mS[i].PB));
      double size = dwg.Bound.Diagonal / 100.0;

      dwg.Add (new Layer2 ("MARKER", Color4.DarkBlue, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var v in mV) {
         if (v.Kind == EVertex.Mountain) dwg.Add (Poly.Circle (v.Pt, size));
         if (v.Kind == EVertex.Valley) dwg.Add (Poly.Polygon (v.Pt, size, 4, Lib.QuarterPI));
      }

      dwg.Add (new Layer2 ("TILE", Color4.Red, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var t in mT.Where (a => a.Id >= 0)) {
         double y0 = t.YMin, y1 = t.YMax;
         Point2 bl = new (t.LMin, y0), br = new (t.RMin, y0);
         Point2 tl = new (t.LMax, y1), tr = new (t.RMax, y1);
         dwg.Add (Poly.Lines ([bl, br, tr, tl, bl], false));
      }

      dwg.Add (new Layer2 ("LINKS", Color4.Blue, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var t in mT.Where (a => a.Id >= 0)) {
         for (int i = 0; i < 2; i++) {
            AddArrow (GetCommon (t.Top[i], t.Id), true, size);
            AddArrow (GetCommon (t.Id, t.Bot[i]), false, size);
         }
      }

      dwg.Add (new Layer2 ("TEXT", Color4.Black, ELineType.Continuous));
      dwg.Add (new Style2 ("STD", "Simplex", 8, 1, 0));
      dwg.CurrentLayer = dwg.Layers[^1];
      foreach (var t in mT.Where (a => a.Id >= 0)) {
         string text = $"{t.Id}"; if (t.Hole) text += "*";
         var pos = new Point2 (0.25.Along (t.LMin, t.RMin), t.YMin + size / 3);
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, pos, size * 0.75, 0, 0, 1, ETextAlign.BaseCenter));
      }

      dwg.Add (new Layer2 ("DIAG", Color4.DarkGreen, ELineType.Continuous));
      dwg.CurrentLayer = dwg.Layers[^1];
      for (int i = 0; i < mSN; i++)
         if (mS[i].Diagonal) dwg.Add (Poly.Line (mS[i].PA, mS[i].PB));
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

      Point2 GetCommon (int nt0, int nt1) {
         if (nt0 == 0 || nt1 == 0) return Point2.Nil;
         ref Tile t0 = ref mT[nt0], t1 = ref mT[nt1];
         Check (t0.YMin.EQ (t1.YMax));
         double x0 = mS[t0.Left].GetX (t0.YMin), x1 = mS[t0.Right].GetX (t0.YMin);
         Bound1 b0 = new (x0, x1);
         x0 = mS[t1.Left].GetX (t0.YMin); x1 = mS[t1.Right].GetX (t0.YMin);
         Bound1 b1 = new (x0, x1);
         double x = (b0 * b1).Mid; if (x.IsZero ()) Console.Write (" HUH ");
         return new (x, t0.YMin);
      }
   }
}
