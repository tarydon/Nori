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
      for (int i = 0; i < mNode.Length; i++) MakeFace (i);
      return false;
   }

   public void Dump (string file) {
      Dwg2 dwg = new ();
      foreach (var con in mPolys) dwg.Add (con.Poly);
      foreach (var bend in mBends) {
         var bl1 = bend.BLine;
         var bl2 = new E2Bendline (dwg, bend.Pts, bl1.Angle, bl1.Radius, bl1.KFactor, bl1.Thickness);
         dwg.Add (bl2);
         for (int k = 0; k < bend.Pts.Length; k++) {
            ref Node node = ref mNode[bend.NBase + k];
            var poly = mPolys[node.NPoly].Poly;
            var seg = poly[node.NSeg];
            var pt = seg.GetPointAt (node.Lie);
            dwg.Add (pt); dwg.Add (Poly.Circle (pt, 2));
         }
      }

      foreach (var n in mSorted) {
         var node = mNode[n];
         Console.WriteLine ($"{node.NPoly,4} {node.NSeg,4}  {node.Lie.Round (2)}");
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
      List<E2Poly> tmp = [.. mDwg.Ents.OfType<E2Poly> ().Where (Accept)];
      for (int i = 1; i < tmp.Count; i++)
         if (tmp[i].IsOuter) { (tmp[i], tmp[0]) = (tmp[0], tmp[i]); break; }
      mPolys = new CPoly[tmp.Count];
      for (int i = 0; i < tmp.Count; i++) {
         var e2p = tmp[i];
         var poly = e2p.Poly;
         var wind = poly.GetWinding ();
         if (e2p.IsOuter ^ wind == Poly.EWinding.CW) poly = poly.Reversed ();
         mPolys[i] = new (poly, e2p.Bound);
      }

      // Gather all the bendlines
      List<E2Bendline> tmp2 = [.. mDwg.Ents.OfType<E2Bendline> ()];
      mBends = new Bend[tmp2.Count]; int nBase = 0; 
      for (int i = 0; i < tmp2.Count; i++) {
         mBends[i] = new Bend (tmp2[i], nBase);
         nBase += mBends[i].Pts.Length;
      }
      mNode = new Node[nBase];
      return true;

      // Helpers ...........................................
      static bool Accept (E2Poly e2p)
         => e2p.Poly.IsClosed && e2p.Poly.GetWinding () != Poly.EWinding.Indeterminate;
   }

   // Create faces from the nodes
   void MakeFace (int n) {
      ref Node node = ref mNode[n]; if (node.Used) return;

      // We're going to start building a face
      PolyBuilder pb = new ();
      bool bend = node.Used = true;
      for (; ; ) {
         if (bend) {
            // We travel along the bendline
         } else {
            // We travel along the contour
         }
      }
   }

   // Snaps bend-lines to begin/end exactly on contours (by trimming / extending) them
   void SnapBendlines () {
      int nSeg = 0, nPoly = 0; double dist;
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
            ref Node node = ref mNode[bend.NBase + k];
            (node.NBend, node.NPt) = (i, k);
            for (int j = 0; j < mPolys.Length; j++) {
               ref CPoly con = ref mPolys[j]; if (!con.Intersects) continue;
               (dist, nSeg) = con.Poly.GetDistance (pt);
               if (dist.IsZero ()) {
                  (node.NPoly, node.NSeg, node.Lie) = (j, nSeg, con.Poly[nSeg].GetLie (pt));
                  break;
               }
            }
            if (node.NPoly == -1) continue; 

            // Otherwise, snap the bendline to the closest intersection of any of the poly
            double distMin = 1e99; Point2 pBest = pt;
            for (int j = 0; j < mPolys.Length; j++) {
               ref CPoly con = ref mPolys[j]; if (!con.Intersects) continue;
               foreach (var seg in con.Poly.Segs) {
                  var ints = seg.Intersect (a, b, buffer, true);
                  foreach (var pint in ints) {
                     dist = pint.DistToSq (pt);
                     if (dist < distMin) (distMin, nPoly, nSeg, pBest) = (dist, j, seg.N, pint);
                  }
               }
            }
            // Copy the position and location into index k
            (node.NPoly, node.NSeg) = (nPoly, nSeg);
            node.Lie = mPolys[nPoly].Poly[nSeg].GetLie (bend.Pts[k] = pBest);
         }
      }
      mSorted = [.. Enumerable.Range (0, mNode.Length)];
      mSorted.Sort (CompareNode);
      for (int i = 0; i < mSorted.Length; i++) {
         int j = mSorted[(i + 1) % mSorted.Length];
         ref Node node = ref mNode[mSorted[i]];
         node.Next = j; 
      }

      // Helpers ...........................................
      int CompareNode (int a, int b) {
         ref Node na = ref mNode[a], nb = ref mNode[b];
         int n = na.NPoly.CompareTo (nb.NPoly); if (n != 0) return n;
         n = na.NSeg.CompareTo (nb.NSeg); if (n != 0) return n;
         return na.Lie.CompareTo (nb.Lie);
      }
   }

   // Nested types -------------------------------------------------------------
   // Represents an outer / inner contour of the poly.
   // The outer contour is CCW, while the rest are CW
   struct CPoly (Poly poly, Bound2 bound) {
      public readonly Poly Poly = poly;
      public readonly Bound2 Bound = bound;
      public bool Intersects;
   }

   // Node is a junction between a poly and bendline.
   // This is a location where a bendline touches a poly (at its endpoint). 
   struct Node {
      public int NBend;    // Index of the bendline (within the mBends array)
      public int NPt;      // Index within the Pts array of that bendline
      public int NPoly;    // Index of poly (with the mPolys array)
      public int NSeg;     // Segment number within that poly
      public double Lie;   // Lie within that segment
      public int Next;     // Next node within this Poly
      public bool Used;    // Have we used this already
   }
   Node[] mNode = [];
   int[] mSorted = [];

   // Represents a bend-line 
   readonly struct Bend {
      public Bend (E2Bendline bend, int nBase) {
         Pts = [..(BLine = bend).Pts];
         NBase = nBase;
      }
      public readonly E2Bendline BLine;
      public readonly Point2[] Pts;    // REMOVETHIS
      public readonly int NBase;
   }

   // Private data -------------------------------------------------------------
   CPoly[] mPolys = [];
   Bend[] mBends = [];
}
