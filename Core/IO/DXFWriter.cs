// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFWriter.cs
// ║║║║╬║╔╣║ Implements DXFWriter: writes out a Dwg2 to a DXF file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class DXFWriter (Dwg2 dwg) {
   public string Write () {
      S.Clear ();
      Out (" 0\nSECTION\n 2\nHEADER\n 0\nENDSEC\n 0\nSECTION\n 2\nTABLES\n");
      OutLayers ();
      OutStyles ();
      OutBlocks ();
      Out (" 0\nENDSEC\n 0\nSECTION\n 2\nENTITIES\n");
      OutEntities (mDwg.Ents);
      Out (" 0\nENDSEC\n 0\nEOF\n");
      return S.ToString ();
   }

   public static void SaveFile (Dwg2 dwg, string file)
      => File.WriteAllText (file, new DXFWriter (dwg).Write ());

   int Out (string s) { S.Append (s); return 0; }

   // Output the entities (could be the ents in the drawing, or within a block)
   void OutEntities (IEnumerable<Ent2> ents) {
      foreach (var ent in ents) {
         _ = ent switch {
            E2Poly ep => OutEnt (ep),
            E2Point ep => OutEnt (ep),
            E2Text et => OutEnt (et),
            E2Solid es => OutSolid (es),
            E2Insert ei => OutEnt (ei),
            E2Dimension e2d => OutDimension (e2d),
            _ => throw new BadCaseException (ent.GetType ().Name)
         };
      }
   }

   // This is a placeholder, we don't write dimensions out yet
   int OutDimension (E2Dimension ed) => 0;

   void OutLayers () {
      var layers = mDwg.Layers;
      Out ($" 0\nTABLE\n 2\nLAYER\n 70\n{layers.Count}\n");
      foreach (var layer in layers) {
         int flags = layer.IsVisible == true ? 0 : 1, color = ToACADColor (layer.Color);
         string name = layer.Name, ltype = layer.Linetype.ToString ().ToUpper ();
         Out ($" 0\nLAYER\n 70\n{flags}\n 2\n{name}\n 62\n{color}\n 6\n{ltype}\n");
      }
      Out (" 0\nENDTAB\n");
   }

   void OutStyles () {
      var styles = mDwg.Styles; if (styles.Count == 0) return;
      Out ($" 0\nTABLE\n 2\nSTYLE\n 70\n{styles.Count}\n");
      foreach (var s in styles) {
         Out ($" 0\nSTYLE\n 2\n{s.Name}\n 70\n0\n 40\n{s.Height}\n 41\n{s.XScale}\n");
         Out ($" 50\n{s.Oblique.R2D ()}\n 71\n0\n 42\n1\n 3\n{s.Font}\n 4\n\n");
      }
      Out (" 0\nENDTAB\n");
   }

   void OutBlocks () {
      var blocks = mDwg.Blocks; if (blocks.Count == 0) return;
      Out ($" 0\nTABLE\n 2\nBLOCKS\n 70\n{blocks.Count}\n");
      foreach (var block in blocks) {
         var pt = block.Base;
         Out ($" 0\nBLOCK\n 70\n65\n 2\n{block.Name}\n 10\n{pt.X}\n 20\n{pt.Y}\n");
         OutEntities (block.Ents);
         Out (" 0\nENDBLK\n");
      }
      Out (" 0\nENDTAB\n");
   }

   int OutEnt (E2Poly e2p) {
      var poly = e2p.Poly;
      if (poly.IsCircle) { OutCircle (e2p); return 0; }
      if (poly.Count == 1) {
         if (poly.IsLine) OutLine (e2p); else OutArc (e2p);
      } else {
         OutColor (e2p, "POLYLINE");
         Out ($" 66\n1\n 10\n0\n 20\n0\n 70\n{(poly.IsClosed ? 1 : 0)}\n");
         foreach (var seg in poly.Segs) {
            var pt = Lib.Testing ? seg.A.R6 () : seg.A;
            Out ($" 0\nVERTEX\n 8\n0\n 10\n{pt.X}\n 20\n{pt.Y}\n");
            if (seg.IsArc) {
               if (seg.IsCircle) {
                  pt = seg.Center;
                  double bulge = seg.IsCCW ? 1 : -1;
                  Out ($" 42\n{bulge}\n 0\nVERTEX\n 8\n0\n 10\n{pt.X}\n 20\n{pt.Y}\n 42\n{bulge}\n");
               } else {
                  double a1 = seg.Center.AngleTo (seg.A), a2 = seg.Center.AngleTo (seg.B);
                  a2 += seg.IsCCW ? (a2 < a1 ? Lib.TwoPI : 0) : (a2 > a1 ? -Lib.TwoPI : 0);
                  Out ($" 42\n{Math.Tan ((a2 - a1) / 4).R6 ()}\n");
               }
            }
            if (!poly.IsClosed && seg.IsLast) {
               pt = Lib.Testing ? seg.B.R6 () : seg.B;
               Out ($" 0\nVERTEX\n 8\n0\n 10\n{pt.X}\n 20\n{pt.Y} \n");
            }
         }
         Out ($" 0\nSEQEND\n 8\n0\n");
      }
      return 0;
   }

   int OutEnt (E2Insert e) {
      Out ($" 0\nINSERT\n 8\n{e.Layer.Name}\n 2\n{e.BlockName}\n 10\n{e.Pt.X}\n 20\n{e.Pt.Y}\n");
      Out ($" 41\n{e.XScale}\n 42\n{e.YScale}\n 50\n{e.Angle.R2D ().R6 ()}\n");
      return 0;
   }

   int OutEnt (E2Point e2p) {
      var (a, b) = (e2p.Pt.X, e2p.Pt.Y);
      OutColor (e2p, "POINT");
      return Out ($" 10\n{a}\n 20\n{b}\n");
   }

   // Output text entity
   int OutEnt (E2Text e) {
      var (pt, height, angle) = (e.Pt, e.Height, e.Angle.R2D ().R6 ());
      if (Lib.Testing) (pt, height) = (pt.R6 (), height.R6 ());
      OutColor (e, "TEXT");
      int align = (int)e.Alignment - 1, horz = align % 3, vert = 3 - align / 3;
      Out ($" 10\n{pt.X}\n 20\n{pt.Y}\n 40\n{height}\n 1\n{e.Text}\n");
      if (e.Alignment != ETextAlign.BaseLeft) Out ($" 11\n{pt.X}\n 21\n{pt.Y}\n");
      if (!angle.IsZero ()) Out ($" 50\n{angle}\n");
      if (e.Style.Name != "STANDARD") Out ($" 7\n{e.Style.Name}\n");
      if (!e.Oblique.IsZero ()) Out ($" 51\n{e.Oblique.R2D ().R6 ()}\n");
      if (!e.XScale.EQ (1)) Out ($" 41\n{e.XScale.R6 ()}\n");
      if (horz > 0) Out ($" 72\n{horz}\n");
      if (vert > 0) Out ($" 73\n{vert}\n");
      return 0;
   }

   void OutColor (Ent2 ent, string type) {
      Out ($" 0\n{type}\n 8\n{ent.Layer.Name}\n");
      if (!ent.Color.IsNil) Out ($" 62\n{ToACADColor (ent.Color)}\n");
   }

   int OutLine (E2Poly e2p) {
      OutColor (e2p, "LINE");
      var s = e2p.Poly[0];
      var (p1, p2) = (s.A, s.B);
      if (Lib.Testing) (p1, p2) = (p1.R6 (), p2.R6 ());
      return Out ($" 10\n{p1.X}\n 20\n{p1.Y}\n 11\n{p2.X}\n 21\n{p2.Y}\n");
   }

   int OutArc (E2Poly e2p) {
      OutColor (e2p, "ARC");
      var s = e2p.Poly[0];
      var (a1, a2) = s.GetStartAndEndAngles ();
      var (c, r, sa, ea) = (s.Center, s.Radius, a1.R2D (), a2.R2D ());
      if (!s.IsCCW) (sa, ea) = (ea, sa); // Swap the angles to handle CW arc
      if (Lib.Testing) (c, r, sa, ea) = (c.R6 (), r.R6 (), sa.R6 (), ea.R6 ());
      return Out ($" 10\n{c.X}\n 20\n{c.Y}\n 40\n{r}\n 50\n{sa}\n 51\n{ea}\n");
   }

   int OutCircle (E2Poly e2p) {
      OutColor (e2p, "CIRCLE");
      var seg = e2p.Poly.Segs.First ();
      var (c, r) = (seg.Center, seg.Radius);
      if (Lib.Testing) (c, r) = (c.R6 (), r.R6 ());
      return Out ($" 10\n{c.X}\n 20\n{c.Y}\n 40\n{r}\n");
   }

   /// <summary>Maps Color4 to nearest ACAD color by comparing RGB values (Not visually accurate).</summary>
   public static int ToACADColor (Color4 color) {
      return DXFReader.ACADColors.MinIndexBy (a => Error (a, color));

      // Helper method
      // Returns square dist between two Colors (RGB comparison only)
      static double Error (Color4 a, Color4 b) {
         int dr = b.R - a.R, dg = b.G - a.G, db = b.B - a.B;
         return dr * dr + dg * dg + db * db;
      }
   }

   int OutSolid (E2Solid es) {
      OutColor (es, "SOLID");
      for (int i = 0; i < es.Pts.Count; i++) {
         var pt = Lib.Testing ? es.Pts[i].R6 () : es.Pts[i];
         Out ($" {10 + i}\n{pt.X}\n {20 + i}\n{pt.Y}\n");
      }
      return 0;
   }

   readonly Dwg2 mDwg = dwg;
   readonly StringBuilder S = new ();
}
