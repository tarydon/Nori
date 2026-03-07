using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Nori.Alt;

public partial class Triangulator {
   public string GetNodeGraph () {
      StringBuilder sb = new ();
      sb.AppendLine ("""
         digraph {
           fontname="Segoe UI,sans-serif"
           node [fontname="Segoe UI,sans-serif"]
           edge [fontname="Segoe UI,sans-serif"]
         """);
      for (int i = 0; i < mNN; i++) {
         ref Node n = ref mN[i];
         sb.Append ($"  n{n.Id} [label=\"");
         switch (n.Kind) {
            case ENode.Y: sb.AppendLine ($"V#{n.Index} Y:{mV[n.Index].Pt.Y.Round (0)}\" shape=oval];"); break;
            case ENode.X: sb.AppendLine ($"S#{n.Index} X:{mS[n.Index].A}..{mS[n.Index].B}\" shape=box];"); break;
            case ENode.Redirect: sb.AppendLine ($"R\" shape=circle];"); break;
            case ENode.Leaf: sb.AppendLine ($"T#{n.Index}\" shape=octagon];"); break;
            default: throw new InvalidOperationException (); 
         }
         if (n.Kind != ENode.Leaf) {
            sb.AppendLine ($"    n{n.Id} -> n{n.First} [label=1];");
            if (n.Kind != ENode.Redirect) sb.AppendLine ($"    n{n.Id} -> n{n.Second} [label=2];");
         }
      }
      sb.AppendLine ("}");
      return sb.ToString (); 
   }

   /// <summary>
   /// Returns a debug drawing
   /// </summary>
   public Dwg2 GetDebugDwg () {
      Dwg2 dwg = new ();
      double size = Bound.Diagonal / 150.0;
      dwg.Add (new Style2 ("STD", "Simplex", 8, 1, 0));

      AddLayer ("OUTLINE", Color4.Black);
      for (int i = 0; i < mSN; i++) {
         ref Segment s = ref mS[i];
         if (!s.Diagonal) dwg.Add (Poly.Line (mS[i].PA, mS[i].PB));
      }

      if (mTris.Count > 0) {
         List<Point2> pts = [];
         AddLayer ("TRIS", Color4.Green);
         for (int i = 0; i < mTris.Count; i += 3) {
            for (int j = 0; j < 3; j++) pts.Add (mV[mTris[i + j] + 1].Pt);
            dwg.Add (Poly.Lines (pts, true));
            Point2 pt = (pts[0] + pts[1] + pts[2]) * 0.33333;
            dwg.Add (new E2Point (dwg.CurrentLayer, pt));
            pts.Clear (); 
         }
         return dwg;
      }

      AddLayer ("TILE", Color4.Red);
      List<(Point2, string)> tileText = [];
      Dictionary<int, Bound1> tileTop = [], tileBot = [];
      for (int i = 1; i < mTN; i++) {
         ref Tile t = ref mT[i]; if (t.Id == 0) continue;
         if (mMerged && t.Hole) continue;
         double y0 = t.YMin, y1 = t.YMax;
         ref Segment L = ref mS[t.Left], R = ref mS[t.Right];
         Point2 bl = new (L.GetX (y0), y0), br = new (R.GetX (y0), y0);
         Point2 tl = new (L.GetX (y1), y1), tr = new (R.GetX (y1), y1);
         if (mAddedDiagonals) {
            dwg.Add (Poly.Line (bl, tl)); dwg.Add (Poly.Line (br, tr));
         } else 
            dwg.Add (Poly.Lines ([bl, br, tr, tl, bl], false));
         double xmid = (bl.X + br.X + tl.X + tr.X) / 4, ymid = (y0 + y1) / 2;
         if (!mAddedDiagonals || t.EBot == EChain.Valley) tileText.Add ((new (xmid, ymid), t.ToString ()));
         tileBot.Add (i, new (bl.X, br.X)); tileTop.Add (i, new (tl.X, tr.X));
         Check (Locate (new (xmid, ymid)).Id == t.Id);
      }

      AddLayer ("VERTNO", Color4.DarkGreen);
      for (int i = 1; i < mVN - 4; i++) {
         ref Vertex v = ref mV[i];
         ETextAlign align = ETextAlign.MidLeft; Point2 pt = v.Pt;
         switch (v.Kind) {
            case EVertex.Valley: align = ETextAlign.TopCenter; pt = pt.Moved (0, -size / 4); break;
            case EVertex.Mountain: align = ETextAlign.BaseCenter; pt = pt.Moved (0, size / 4); break;
            default: pt = pt.Moved (size / 4, 0); break;
         }
         string text = mMerged ? v.Id.ToString () : v.ToString ();
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], text, pt, size, 0, 0, 1, align));
      }

      AddLayer ("TILETEXT", Color4.Black);
      foreach (var (pt, s) in tileText) 
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.Styles[^1], s, pt, size, 0, 0, 1, ETextAlign.MidCenter));

      AddLayer ("LINKS", Color4.Blue);
      for (int i = 1; i < mTN; i++) {
         if (mAddedDiagonals) continue; 
         ref Tile t = ref mT[i]; if (t.Id == 0) continue;
         (int top1, int top2) = t.GetTop (mV);
         AddArrow (GetCommon (top1, t.Id), true, size);
         AddArrow (GetCommon (top2, t.Id), true, size);
         (int bot1, int bot2) = t.GetBottom (mV);
         AddArrow (GetCommon (t.Id, bot1), false, size);
         AddArrow (GetCommon (t.Id, bot2), false, size);
      }
      return dwg;

      // Helpers ...........................................
      void AddLayer (string name, Color4 color) {
         dwg.Add (new Layer2 (name, color, ELineType.Continuous));
         dwg.CurrentLayer = dwg.Layers[^1];
      }

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
         if (!tileBot.TryGetValue (nt0, out Bound1 a)) return Point2.Nil;
         if (!tileTop.TryGetValue (nt1, out Bound1 b)) return Point2.Nil;
         ref Tile t0 = ref mT[nt0], t1 = ref mT[nt1];
         Check (t0.YMin.EQ (t1.YMax));
         Bound1 span = a * b;
         return new (span.Mid, t0.YMin);
      }
   }
}
