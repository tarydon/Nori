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
      AssignHoles ();
      return false;
   }

   public void Dump (string file) {
      Dwg2 dwg = new ();
      foreach (var con in mPolys) dwg.Add (con.Poly);
      List<Point2> pts = [];
      foreach (var bend in mBends) {
         pts.Clear ();
         var bl1 = bend.BLine;
         for (int k = 0; k < bl1.Pts.Length; k++) {
            ref Node node = ref mNode[bend.NBase + k];
            pts.Add (node.GetPos (mPolys));
         }
         var bl2 = new E2Bendline (dwg, pts, bl1.Angle, bl1.Radius, bl1.KFactor, bl1.Thickness);
         dwg.Add (bl2);
      }

      double height = dwg.Bound.Diagonal / 200;
      for (int i = 0; i < mNode.Length; i++) {
         ref Node node = ref mNode[i];
         Point2 pt = node.GetPos (mPolys);
         dwg.Add (new E2Text (dwg.CurrentLayer, dwg.GetStyle ("STANDARD")!, i.ToString (), pt, height, 0, 0, 1, ETextAlign.BotCenter));
      }

      var xfm = Matrix2.Translation (dwg.Bound.Width + 10, 0);
      for (int i = 0; i < mNFace; i++) {
         ref Face face = ref mFaces[i];
         dwg.Add (face.Outer * xfm);
         foreach (var hole in face.Holes) dwg.Add (hole * xfm);
      }
      DXFWriter.Save (dwg, file);
   }

   // Implementation -----------------------------------------------------------
   // Adds holes into their owner faces
   void AssignHoles () {
      for (int i = 0; i < mPolys.Length; i++) {
         ref CPoly cp = ref mPolys[i]; if (cp.Used) continue;
         for (int j = 0; j < mFaces.Length; j++) {
            ref Face face = ref mFaces[j];
            if (face.Bound.Contains (cp.Bound)) { face.Holes.Add (cp.Poly); break; }
         }
      }
   }

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
      List<E2Bendline> bends = [.. mDwg.Ents.OfType<E2Bendline> ()];
      mBends = new Bend[bends.Count]; int nBase = 0; 
      for (int i = 0; i < bends.Count; i++) {
         mBends[i] = new Bend (bends[i], nBase);
         nBase += bends[i].Pts.Length;
      }
      mNode = new Node[nBase];
      return true;

      // Helpers ...........................................
      static bool Accept (E2Poly e2p)
         => e2p.Poly.IsClosed && e2p.Poly.GetWinding () != Poly.EWinding.Indeterminate;
   }

   // Create faces from the nodes
   void MakeFace (int a) {
      ref Node node = ref mNode[a]; if (node.Used) return;

      // We're going to start building a face
      PolyBuilder pb = new ();
      bool bend = true;    // true=traverse a bendline, false=traverse across contour
      for (; ; ) {
         if (bend) {
            // Travel along the bendline - this just adds a single line starting
            // at this point (and which will end at the other end of the bendline)
            if (node.Used) break;
            (node.Used, node.NFace) = (true, mNFace);
            pb.Line (node.GetPos (mPolys));
            node = ref mNode[a ^= 1];
         } else {
            // We travel along the contour, from between the start point and the
            // end point
            ref Node next = ref mNode[a = node.Next];
            ref var cpoly = ref mPolys[node.NPoly]; cpoly.Used = true; 
            pb.AddSlice (cpoly.Poly, node.NSeg, node.Lie, next.NSeg, next.Lie, false);
            node = ref next;
         }
         bend = !bend;
      }
      Lib.Grow (ref mFaces, mNFace, 1); 
      mFaces[mNFace++] = new Face (pb.Close ().Build ());
   }

   // Snaps bend-lines to begin/end exactly on contours (by trimming / extending) them
   void SnapBendlines () {
      int nSeg = 0, nPoly = 0; double dist;
      Span<Point2> buffer = stackalloc Point2[2];
      for (int i = 0; i < mBends.Length; i++) {
         ref Bend bend = ref mBends[i];
         var pts = bend.BLine.Pts; Point2 a = pts[0], b = pts[^1];
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
            node.Lie = mPolys[nPoly].Poly[nSeg].GetLie (pBest);
         }
      }

      // Set up the Next pointer in each node to point to the next node within the
      // same polyline (going in lie order)
      int nStart = 0, nLastPoly = -1;
      var sorted = Enumerable.Range (0, mNode.Length).ToList ();
      sorted.Sort (CompareNode); sorted.Add (sorted[0]);
      for (int i = 0; i < sorted.Count - 1; i++) {
         int a = sorted[i], b = sorted[i + 1];
         ref Node n0 = ref mNode[a], n1 = ref mNode[b];
         if (n0.NPoly != nLastPoly) { nStart = a; nLastPoly = n0.NPoly; }
         n0.Next = (n0.NPoly == n1.NPoly) ? b : nStart;
      }
      sorted.RemoveLast ();

      //foreach (var n in sorted) { REMOVETHIS
      //   ref Node node = ref mNode[n];
      //   Console.WriteLine ($"{n,3} {node.NPoly,3} {node.NSeg,3} {node.Next,3}");
      //}

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
      public bool Used;
   }

   // Node is a junction between a poly and bendline.
   // This is a location where a bendline touches a poly (at its endpoint). 
   struct Node {
      public readonly Point2 GetPos (CPoly[] polys) => polys[NPoly].Poly[NSeg].GetPointAt (Lie);
      public readonly override string ToString () => $"Node  Poly:{NPoly}  Lie:{NSeg + Lie.Round (3)}";

      public int NBend;    // Index of the bendline (within the mBends array)
      public int NPt;      // Index within the Pts array of that bendline
      public int NFace;    // Face attached to the left of this bend 

      public int NPoly;    // Index of poly (with the mPolys array)
      public int NSeg;     // Segment number within that poly
      public double Lie;   // Lie within that segment
      public int Next;     // Next node within this Poly
      public bool Used;    // Have we used this already
   }
   Node[] mNode = [];

   // Represents a bend-line 
   readonly struct Bend {
      public Bend (E2Bendline bend, int nBase) => (BLine, NBase) = (bend, nBase);
      public readonly E2Bendline BLine;
      public readonly int NBase;    // Nodes of this Bend start at this location
   }

   // Represents a plane with some holes
   struct Face (Poly outer) {
      public readonly Poly Outer = outer;
      public readonly Bound2 Bound = outer.GetBound ();
      public readonly List<Poly> Holes = [];
      public bool Used; 
   }

   // Private data -------------------------------------------------------------
   CPoly[] mPolys = [];
   Bend[] mBends = [];
   Face[] mFaces = []; int mNFace;
   List<Poly> mOutput = [];
}
