// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFWriter.cs
// ║║║║╬║╔╣║ Implements DXFWriter: writes out a Dwg2 to a DXF file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DXFWriter ----------------------------------------------------------------------------
/// <summary>DXFWriter writes out Dwg2 files to DXF</summary>
public class DXFWriter {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a DXFWriter, given the Dwg2 to work with</summary>
   public DXFWriter (Dwg2 dwg) => D = dwg;

   // Properties ---------------------------------------------------------------
   /// <summary>If this is set, then no POLYLINE or LWPOLYLINE entities are output - only LINE and ARC</summary>
   public bool NoPolyline { get; set; }

   #region Methods -------------------------------------------------------------
   /// <summary>Utility helper to save a Dwg to DXF file</summary>
   public static void Save (Dwg2 dwg, string file)
      => File.WriteAllText (file, new DXFWriter (dwg).Write ());

   /// <summary>Maps Color4 to nearest ACAD color by comparing RGB values</summary>
   public static int ToACADColor (Color4 color) {
      return DXFReader.ACADColors.MinIndexBy (a => Error (a, color));

      // Helper method
      // Returns square dist between two Colors (RGB comparison only)
      static double Error (Color4 a, Color4 b) {
         int dr = b.R - a.R, dg = b.G - a.G, db = b.B - a.B;
         return dr * dr + dg * dg + db * db;
      }
   }

   /// <summary>Writes the Dwg2 to a string (that can then be saved to a file to make a DXF)</summary>
   public string Write () {
      S.Clear ();
      Out (" 0\nSECTION\n 2\nHEADER\n 0\nENDSEC\n 0\nSECTION\n 2\nTABLES\n");
      OutLayers ();
      OutStyles ();
      OutBlocks ();
      Out (" 0\nENDSEC\n 0\nSECTION\n 2\nENTITIES\n");
      OutEntities (D.Ents);
      Out (" 0\nENDSEC\n 0\nEOF\n");
      return S.ToString ();
   }
   #endregion

   // Implementation -----------------------------------------------------------
   // Basic function to append a string
   int Out (string s) { S.Append (s); return 0; }

   // Writes out the BLOCKS table with the list of blocks
   void OutBlocks () {
      var blocks = D.Blocks; if (blocks.Count == 0) return;
      Out ($" 0\nTABLE\n 2\nBLOCKS\n 70\n{blocks.Count}\n");
      foreach (var block in blocks) {
         var pt = block.Base;
         Out ($" 0\nBLOCK\n 70\n65\n 2\n{block.Name}\n 10\n{pt.X}\n 20\n{pt.Y}\n");
         OutEntities (block.Ents);
         Out (" 0\nENDBLK\n");
      }
      Out (" 0\nENDTAB\n");
   }

   // Output the entities (could be the ents in the drawing, or within a block)
   void OutEntities (IEnumerable<Ent2> ents) {
      foreach (var ent in ents) {
         _ = ent switch {
            E2Poly ep => OutPoly (ep),
            E2Point ep => OutPoint (ep),
            E2Text et => OutText (et),
            E2Solid es => OutSolid (es),
            E2Insert ei => OutInsert (ei),
            E2Dimension e2d => OutDimension (e2d),
            E2Bendline e2b => OutBendLine (e2b),
            E2Spline e2s => OutSpline (e2s),
            _ => throw new BadCaseException (ent.GetType ().Name)
         };
      }
   }

   // Given an entity, writes out the 8 group with the layer and possibly the
   // 62 group with the color (if the color is not BYLAYER)
   void OutEntPrologue (Ent2 ent, string type) {
      Out ($" 0\n{type}\n 8\n{ent.Layer.Name}\n");
      if (!ent.Color.IsNil) Out ($" 62\n{ToACADColor (ent.Color)}\n");
   }

   // Writes the LAYER table.
   // Bend lines in the drawing are converted into lines in the BEND and MBEND
   // layers. If these layers do not exist in the drawing, they are added to the LAYER
   // table (but not to the drawing itself).
   void OutLayers () {
      var layers = D.Layers.ToList ();
      if (D.Ents.Any (a => a is E2Bendline)) {
         mBend = layers.FirstOrDefault (a => a.Name.EqIC ("BEND"));
         mMBend = layers.FirstOrDefault (a => a.Name.EqIC ("MBEND"));
         D.Ents.OfType<E2Bendline> ().ForEach (a => {
            if (mBend is null && a.Angle >= 0) layers.Add (mBend = new Layer2 ("BEND", Color4.Green, ELineType.Dot));
            else if (mMBend is null && a.Angle < 0) layers.Add (mMBend = new Layer2 ("MBEND", Color4.Green, ELineType.DashDotDot));
         });
      }
      Out ($" 0\nTABLE\n 2\nLAYER\n 70\n{layers.Count}\n");
      foreach (var layer in layers) {
         int flags = layer.IsVisible ? 0 : 1, color = ToACADColor (layer.Color);
         string name = layer.Name, ltype = layer.Linetype.ToString ().ToUpper ();
         Out ($" 0\nLAYER\n 70\n{flags}\n 2\n{name}\n 62\n{color}\n 6\n{ltype}\n");
      }
      Out (" 0\nENDTAB\n");
   }

   // Writes out the STYLE table with a list of text styles
   void OutStyles () {
      var styles = D.Styles; if (styles.Count == 0) return;
      Out ($" 0\nTABLE\n 2\nSTYLE\n 70\n{styles.Count}\n");
      foreach (var s in styles) {
         Out ($" 0\nSTYLE\n 2\n{s.Name}\n 70\n0\n 40\n{s.Height}\n 41\n{s.XScale}\n");
         Out ($" 50\n{s.Oblique.R2D ()}\n 71\n0\n 42\n1\n 3\n{s.Font}\n 4\n\n");
      }
      Out (" 0\nENDTAB\n");
   }

   // Helper used by OutPoly, writes out a segment from a Poly as a LINE, ARC
   // or CIRCLE entity
   void OutSeg (Ent2 ent, Seg seg) {
      if (seg.IsLine) {
         OutEntPrologue (ent, "LINE");
         var (p1, p2) = (seg.A, seg.B);
         if (Lib.Testing) (p1, p2) = (p1.R6 (), p2.R6 ());
         Out ($" 10\n{p1.X}\n 20\n{p1.Y}\n 11\n{p2.X}\n 21\n{p2.Y}\n");
      } else {
         var (c, r) = (seg.Center, seg.Radius);
         if (Lib.Testing) (c, r) = (c.R6 (), r.R6 ());
         if (seg.IsCircle) {
            OutEntPrologue (ent, "CIRCLE");
            Out ($" 10\n{c.X}\n 20\n{c.Y}\n 40\n{r}\n");
         } else {
            OutEntPrologue (ent, "ARC");
            var (a1, a2) = seg.GetStartAndEndAngles ();
            var (sa, ea) = (a1.R2D (), a2.R2D ());
            if (!seg.IsCCW) (sa, ea) = (ea, sa); // Swap the angles to handle CW arc
            if (Lib.Testing) (sa, ea) = (sa.R6 (), ea.R6 ());
            Out ($" 10\n{c.X}\n 20\n{c.Y}\n 40\n{r}\n 50\n{sa}\n 51\n{ea}\n");
         }
      }
   }

   // Entity writers -----------------------------------------------------------
   // Outputs an E2Bendline entity.
   // Since bend lines are not directly supported in DXF, this outputs a LINE entity,
   // in the BEND or MBEND layers. The entity has 1000 group codes that contain bend
   // information in the form of key value pairs like this:
   // * BEND_ANGLE:90
   // * BEND_RADIUS:2.5
   // * K_FACTOR:0.42
   int OutBendLine (E2Bendline eb) {
      for (int i = 0; i < eb.Pts.Length; i += 2) {
         Point2 pa = eb.Pts[i], pb = eb.Pts[i + 1];
         if (Lib.Testing) (pa, pb) = (pa.R6 (), pb.R6 ());
         var (a, r, k) = (eb.Angle.R2D (), eb.Radius, eb.KFactor);
         if (Lib.Testing) (a, r, k) = (a.R6 (), r.R6 (), k.R6 ());
         var layer = eb.Angle < 0 ? mMBend : mBend;
         Out ($" 0\nLINE\n 8\n{layer!.Name}\n 10\n{pa.X}\n 20\n{pa.Y}\n 11\n{pb.X}\n 21\n{pb.Y}\n");
         Out ($" 1000\nBEND_ANGLE:{a}\n 1000\nBEND_RADIUS:{r}\n 1000\nK_FACTOR:{k} \n");
      }
      return 0;
   }
   Layer2? mMBend, mBend;

   // This is a placeholder, we don't write dimensions out yet
   static int OutDimension (E2Dimension _) => 0;

   // Writes out the E2Insert entities
   int OutInsert (E2Insert e) {
      Out ($" 0\nINSERT\n 8\n{e.Layer.Name}\n 2\n{e.BlockName}\n 10\n{e.Pt.X}\n 20\n{e.Pt.Y}\n");
      Out ($" 41\n{e.XScale}\n 42\n{e.YScale}\n 50\n{e.Angle.R2D ().R6 ()}\n");
      return 0;
   }

   // Writes out an E2Point entity
   int OutPoint (E2Point e2p) {
      var (a, b) = (e2p.Pt.X, e2p.Pt.Y);
      OutEntPrologue (e2p, "POINT");
      return Out ($" 10\n{a}\n 20\n{b}\n");
   }

   // Writes out an E2Poly entity
   // If the poly contains only one segment, or if the NoPolyline flag is set, then the
   // segment(s) are written out as LINE and ARC entities. Otherwise, the complete polyline
   // is written out as a POLYLINE entity
   int OutPoly (E2Poly e2p) {
      var poly = e2p.Poly;
      if (poly.Count == 1 || NoPolyline) {
         foreach (var seg in poly.Segs) OutSeg (e2p, seg);
      } else {
         OutEntPrologue (e2p, "POLYLINE");
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
                  var bulge = Math.Tan (seg.AngSpan / 4); if (Lib.Testing) bulge = bulge.R6 ();
                  Out ($" 42\n{bulge}\n");
               }
            }
            if (!poly.IsClosed && seg.IsLast) {
               pt = Lib.Testing ? seg.B.R6 () : seg.B;
               Out ($" 0\nVERTEX\n 8\n0\n 10\n{pt.X}\n 20\n{pt.Y} \n");
            }
         }
         Out (" 0\nSEQEND\n 8\n0\n");
      }
      return 0;
   }

   // Output an E2Solid entity
   int OutSolid (E2Solid es) {
      OutEntPrologue (es, "SOLID");
      for (int i = 0; i < es.Pts.Count; i++) {
         var pt = Lib.Testing ? es.Pts[i].R6 () : es.Pts[i];
         Out ($" {10 + i}\n{pt.X}\n {20 + i}\n{pt.Y}\n");
      }
      return 0;
   }

   // Outputs a SPLINE curve
   int OutSpline (E2Spline es) {
      OutEntPrologue (es, "SPLINE");
      var (spline, flags) = (es.Spline, 8);     // PLANAR
      if ((es.Flags & E2Flags.Closed) != 0) flags |= 1;  // CLOSED
      if ((es.Flags & E2Flags.Periodic) != 0) flags |= 2;   // PERIODIC
      if (spline.Rational) flags |= 4; // RATIONAL
      var (knots, weights, ctrl) = (spline.Imp.Knot, spline.Weight, spline.Ctrl);
      Out ($" 70\n{flags}\n 71\n{spline.Imp.Degree}\n 72\n{knots.Length}\n 73\n{ctrl.Length}\n");
      foreach (var knot in knots) Out ($" 40\n{knot}\n");
      foreach (var pt0 in ctrl) {
         var pt = Lib.Testing ? pt0.R6 () : pt0;
         Out ($" {10}\n{pt.X}\n 20\n{pt.Y}\n 30\n0\n");
      }
      if (spline.Rational)
         foreach (var wt in weights) Out ($" 41\n{wt}\n");
      return 0; 
   }

   // Output text entity
   int OutText (E2Text e) {
      var (pt, height, angle) = (e.Pt, e.Height, e.Angle.R2D ().R6 ());
      if (Lib.Testing) (pt, height) = (pt.R6 (), height.R6 ());
      OutEntPrologue (e, "TEXT");
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

   // Private data -------------------------------------------------------------
   readonly Dwg2 D;  // The drawing we're writing out
   readonly StringBuilder S = new ();  // The stringbuilder used to compose the output
}
#endregion
