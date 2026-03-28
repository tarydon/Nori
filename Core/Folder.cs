// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Folder.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

public class Folder {
   public Folder (Dwg2 dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   public bool Process () {
      if (!GatherContours ()) return false;
      SnapBendlines ();
      return false;
   }

   public void Dump (string file) {
      Dwg2 dwg = new ();
      foreach (var con in mPolys) dwg.Add (con.Poly);
      foreach (var bend in mBends) {
         var bl1 = bend.BLine;
         var bl2 = new E2Bendline (dwg, bend.Pts, bl1.Angle, bl1.Radius, bl1.KFactor, bl1.Thickness);
         dwg.Add (bl2);
         foreach (var pt in bend.Pts) dwg.Add (pt);
      }
      DXFWriter.Save (dwg, file);
   }

   // Implementation -----------------------------------------------------------
   // Gathers all the contours, and bend-lines
   bool GatherContours () {
      // Gather all the closed poly, with the outer one being at 0. The outer poly
      // is CCW, while the holes are CW
      if (!mDwg.MarkInOut ()) return false;
      
      // Gather all the closed Poly, and move the outer contour to index 0
      var tmp = mDwg.Ents.OfType<E2Poly> ()
                    .Where (a => a.Poly.IsClosed && a.Poly.GetWinding () != Poly.EWinding.Indeterminate)
                    .ToList ();
      for (int i = 1; i < tmp.Count; i++) 
         if (tmp[i].IsOuter) { (tmp[i], tmp[0]) = (tmp[0], tmp[i]); break; }
      mPolys = new CPoly[tmp.Count];
      for (int i = 0; i < tmp.Count; i++) {
         var e2p = tmp[i];
         var poly = e2p.Poly;
         var wind = poly.GetWinding ();
         if (e2p.IsOuter ^ wind == Poly.EWinding.CW) poly = poly.Reversed ();
         mPolys[i] = new (i, poly, e2p.Bound);
      }

      // Gather all the bendlines
      var tmp2 = mDwg.Ents.OfType<E2Bendline> ().ToList ();
      mBends = new Bend[tmp2.Count];
      for (int i = 0; i < tmp2.Count; i++) mBends[i] = new Bend (tmp2[i]);
      return true;
   }

   // Snaps bend-lines to begin/end exactly on contours (by trimming / extending) them
   void SnapBendlines () {
      Span<Point2> buffer = stackalloc Point2[2];
      for (int i = 0; i < mBends.Length; i++) {
         ref Bend bend = ref mBends[i];
         var pts = bend.Pts; Point2 a = pts[0], b = pts[^1];
         // Mark the set of contours this bend-line intersects
         for (int j = 0; j < mPolys.Length; j++) {
            ref CPoly con = ref mPolys[j];
            con.Intersects = con.Bound.Intersects (a, b);
         }

         for (int k = 0; k < pts.Length; k++) {
            // Take each segment of the bend-line, and if necessary, trim/extend the segments.
            // First step: if the endpoint is already snapped to an existing contour, don't
            // do anything (just record the Poly/Seg there and continue)
            Point2 pt = pts[k];
            for (int j = 0; j < mPolys.Length; j++) {
               ref CPoly con = ref mPolys[j]; if (!con.Intersects) continue;
               var (dist, nSeg1) = con.Poly.GetDistance (pt);
               if (dist.IsZero ()) { bend.Poly[k] = j; bend.Seg[k] = nSeg1; break; }
            }
            if (bend.Poly[k] != -1) continue;

            // Otherwise, snap the bendline to the closest intersection of any of the poly
            double distMin = 1e99; int nPoly = -1, nSeg2 = -1; Point2 pBest = pt;
            for (int j = 0; j < mPolys.Length; j++) {
               ref CPoly con = ref mPolys[j]; if (!con.Intersects) continue;
               foreach (var seg in con.Poly.Segs) {
                  var ints = seg.Intersect (a, b, buffer, true);
                  foreach (var pint in ints) {
                     double dist = pint.DistToSq (pt);
                     if (dist < distMin) (distMin, nPoly, nSeg2, pBest) = (dist, j, seg.N, pint);
                  }
               }
            }
            bend.Pts[k] = pBest; bend.Poly[k] = nPoly; bend.Seg[k] = nSeg2;
         }
      }
   }

   // Nested types -------------------------------------------------------------
   // Represents an outer / inner contour of the poly.
   // The outer contour is CCW, while the rest are CW
   struct CPoly (int n, Poly poly, Bound2 bound) {
      public readonly int N = n;
      public readonly Poly Poly = poly;
      public readonly Bound2 Bound = bound;
      public bool Intersects;  
   }

   // Represents a bend-line 
   readonly struct Bend {
      public Bend (E2Bendline bend) {
         Pts = [..(BLine = bend).Pts];
         Poly = new int[Pts.Length]; Seg = new int[Pts.Length];
         for (int i = 0; i < Poly.Length; i++) Poly[i] = -1;
      }
      public readonly E2Bendline BLine;
      public readonly Point2[] Pts;
      public readonly int[] Poly, Seg;
   }

   // Private data -------------------------------------------------------------
   CPoly[] mPolys = [];
   Bend[] mBends = [];
}
