// ────── ╔╗
// ╔═╦╦═╦╦╬╣ PaperFolder.cs
// ║║║║╬║╔╣║ Implements a 'paper' model folder (no thickness)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class PaperFolder --------------------------------------------------------------------------
/// <summary>PaperFolder can fold drawings with bend-lines into paper-models (no thickness)</summary>
public class PaperFolder {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a PaperFolder from a model</summary>
   public PaperFolder (Dwg2 dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   // Methods ------------------------------------------------------------------
   /// <summary>Construct the Model3 from the drawing</summary>
   public Model3 Process () {
      if (!GatherContours ()) throw new InvalidOperationException ("Ill-formed drawing");
      for (int i = 0; i < mBends.Length; i++)
         if (!SnapBendline (i)) throw new InvalidOperationException ("Incorrect bend-line");
      if (!CheckBendIntersections ()) throw new InvalidOperationException ("Intersecting bend lines");
      LinkNodesPerPoly ();
      for (int i = 0; i < mNNode; i++) MakeFace (i);
      AssignHoles ();
      CreateTree ();
      return CreateModel (); 
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
      for (int i = 0; i < mNNode; i++) {
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

   // Checks every pair of bend lines for intersections
   bool CheckBendIntersections () {
      for (int i = 0; i < mBends.Length; i++) {
         // Take each bend line b1
         ref Bend b1 = ref mBends[i]; var bL1 = b1.BLine;
         b1.Delta = (bL1.Pts[^1] - bL1.Pts[0]).Normalized () * 0.0001;
         // And check it with each other bend line b0
         for (int j = 0; j < i; j++) {
            ref Bend b0 = ref mBends[j]; var bL0 = b0.BLine;
            // p is the index into b0 points, while q us the index into b1 points
            for (int p = 0; p < bL0.Pts.Length; p += 2) {
               Point2 s0 = bL0.Pts[p] + b0.Delta, e0 = bL0.Pts[p + 1] - b0.Delta;
               for (int q = 0; q < bL1.Pts.Length; q += 2) {
                  Point2 s1 = bL1.Pts[q] + b1.Delta, e1 = bL1.Pts[q + 1] - b1.Delta;
                  if (!Geo.LineSegXLineSeg (s0, e0, s1, e1).IsNil) return false;
               }
            }
         }
      }
      return true; 
   }

   // This creates a tree starting with a 'baseplane' and picking up adjacent planes
   // by connectivity. Each segment of each bendline is represented by two nodes on 
   // the two endpoints (consider them like two half-edges running in opposite directions)
   // These two half-edges point to the faces on opposite sides of the bendline, and thus
   // can be used to link them up
   void CreateTree () {
      Queue<int> todo = [];
      mRootFace = mFaces.MaxIndexBy (a => a.Bound.Area);
      mFaces[mRootFace].UsedInModel = true; 
      todo.Enqueue (mRootFace);
      while (todo.TryDequeue (out int nFace)) {
         ref Face face = ref mFaces[nFace]; 
         // 1. Go through each of the half-edges connected to this face
         // 2. Get the paired half-edge on the other side (n0 ^ 1 does this)
         // 3. Get the face on the other side (mNode[n0 ^ 1].NFace)
         // 4. If not reached yet, add that face to the queue
         for (int i = 0; i < face.Edges.Length; i++) {
            int n0 = face.Edges[i], nFace2 = mNode[n0 ^ 1].NFace;
            ref Face face2 = ref mFaces[nFace2];
            if (face2.UsedInModel) face.Children[i] = -1;
            else { 
               face.Children[i] = nFace2; face2.UsedInModel = true; 
               todo.Enqueue (nFace2); 
            }
         }
      }
      // At the end of this loop, we've built a tree (via the Face.Children list), starting
      // with mFace[mRootFace]. 
   }

   // This is called to create the model
   Model3 CreateModel () {
      Model3 model = new ();
      Queue<(int Face, Matrix3 Xfm)> todo = [];
      todo.Enqueue ((mRootFace, Matrix3.Identity));
      List<Poly> polys = [];
      while (todo.TryDequeue (out var tup)) {
         // First, create a plane with this plane's outer contour and holes
         var xfm = tup.Xfm;
         ref Face face1 = ref mFaces[tup.Face];
         polys.Clear (); polys.Add (face1.Outer); polys.AddRange (face1.Holes);
         model.Ents.Add (E3Plane.Build (model.Ents.Count, polys, xfm.ToCS ()));

         // Next, visit each of the children that have not already been queued up, and
         // compute the transform for that child, and enqueue it
         for (int i = 0; i < face1.Children.Length; i++) {
            var (nFace2, nEdge) = (face1.Children[i], face1.Edges[i]);
            if (nFace2 == -1) continue;

            ref Face face2 = ref mFaces[nFace2];
            var bend = mBends[mNode[nEdge].NBend].BLine;
            double angle = bend.Angle; if ((nEdge & 1) == 1) angle = -angle;
            Point2 pa = bend.Pts[0], pb = bend.Pts[^1];
            var xfm1 = Matrix3.Rotation ((Point3)pa, (Point3)pb, angle);
            todo.Enqueue ((nFace2, xfm1 * xfm)); 
         }
      }
      return model; 
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
      mBends = new Bend[bends.Count]; 
      for (int i = 0; i < bends.Count; i++) {
         var bline = bends[i];
         if (bline.Pts.Length.IsOdd ()) return false; // Anamoly!
         mBends[i] = new Bend (bline);
      }
      return true;

      // Helpers ...........................................
      static bool Accept (E2Poly e2p)
         => e2p.Poly.IsClosed && e2p.Poly.GetWinding () != Poly.EWinding.Indeterminate;
   }

   // Create faces from the nodes
   void MakeFace (int a) {
      ref Node node = ref mNode[a]; if (node.UsedInFace) return;

      // We're going to start building a face
      PolyBuilder pb = new ();
      bool bend = true;    // true=traverse a bendline, false=traverse across contour
      mNodeId.ClearFast ();
      for (; ; ) {
         if (bend) {
            // Travel along the bendline - this just adds a single line starting
            // at this point (and which will end at the other end of the bendline)
            if (node.UsedInFace) break;
            (node.UsedInFace, node.NFace) = (true, mNFace);
            pb.Line (node.GetPos (mPolys));
            mNodeId.Add (a);
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
      mFaces[mNFace++] = new Face (pb.Close ().Build (), [.. mNodeId]);
   }
   // Temporary used to hold the list of bends connected to the face we are building
   List<int> mNodeId = [];

   // Snaps bend-lines to begin/end exactly on contours (by trimming/extending them as
   // needed). We also detect here when bend-line spans cross polylines, and cut them up
   // at those intersections
   bool SnapBendline (int nBend) {
      // First, mark the list of contours this bend could intersect. At this point, 
      // we also gather all the intersections of these contours with the infinite bend-line.
      mInters.Clear (); 
      ref Bend bend = ref mBends[nBend]; 
      Span<Point2> buffer = stackalloc Point2[2];
      var pts = bend.BLine.Pts; int nLastPt = pts.Length - 1;
      Point2 a = pts[0], b = pts[nLastPt];
      for (int nPoly = 0; nPoly < mPolys.Length; nPoly++) {
         ref CPoly con = ref mPolys[nPoly];
         con.Intersects = con.Bound.Intersects (a, b);
         foreach (var seg in con.Poly.Segs) {
            var ints = seg.Intersect (a, b, buffer, true);
            foreach (var pt in ints) 
               mInters.Add (new (pt, nPoly, seg.N, pt.GetLieOn (a, b)));
         }
      }
      // Sort the intersections by their lies on the bend-line a..b
      mInters.Sort ((a, b) => a.Lie.CompareTo (b.Lie));

      // Snap each of the points on the bend-line to the closest point within the list
      // of intersections
      int nNext = 0; 
      for (int k = nLastPt; k >= 0; k--) {
         Point2 pt = pts[k];
         int n = mInters.MinIndexBy (a => a.Pt.DistToSq (pt));
         // If this is the last point on the bendline, discard all points beyond this, 
         if (k == nLastPt) mInters.RemoveRange (n + 1, mInters.Count - 1 - n);
         if ((k & 1) == 0) {
            // The indices between n .. nNext are the intersections within this span,
            // which are not yet captured as points on the bendline. Walk through this list,
            // and if we find two intersections at the same point, remove both of them
            for (int m = nNext - 2; m > n; m--) {
               if (!mInters[m].Pt.EQ (mInters[m + 1].Pt)) continue;
               mInters.RemoveRange (m, 2); nNext -= 2;
            }
            // Now, we should have an even number of intersections along this segment. Otherwise,
            // the drawing is ill-formed in some way
            if (((nNext - n) & 1) == 0) {
               // Let's try by removing any intersections that are just 'grazing contacts'
               for (int m = nNext - 1; m > n; m--) {
                  Bound2 bound = mPolys[mInters[m].NPoly].Bound.InflatedL (-0.0001);
                  if (!bound.Intersects (a, b)) { mInters.RemoveAt (m); nNext--; }
               }
            }
            if (((nNext - n) & 1) == 0) return false;
         }
         // If this is the first point on the bendline, discard all points before this
         if (k == 0) mInters.RemoveRange (0, n);
         bend.BLine.Pts = [.. mInters.Select (a => a.Pt)];
         nNext = n; 
      }

      // Now, mInters contains the final set of points on this bendline. Create one Node
      // structure for each of them
      bend.NBase = mNNode;    // Mark where the nodes of this bend start
      Lib.Grow (ref mNode, mNNode, mInters.Count);
      for (int k = 0; k < mInters.Count; k++) {
         ref readonly Inter inter = ref (mInters.AsSpan ())[k];
         ref Node node = ref mNode[mNNode++];
         node.NBend = nBend; node.NPt = k;
         node.Lie = mPolys[node.NPoly = inter.NPoly].Poly[node.NSeg = inter.NSeg].GetLie (inter.Pt);
      }
      return true; 
   }
   // All intersections between the current bendline and the contours
   List<Inter> mInters = [];

   // Set up the Next pointer in each node to point to the next node within the
   // same polyline (going in lie order)
   void LinkNodesPerPoly () {
      // Set up the Next pointer in each node to point to the next node within the
      // same polyline (going in lie order)
      int nStart = 0, nLastPoly = -1;
      var sorted = Enumerable.Range (0, mNNode).ToList ();
      sorted.Sort (CompareNode); sorted.Add (sorted[0]);
      for (int i = 0; i < sorted.Count - 1; i++) {
         int a = sorted[i], b = sorted[i + 1];
         ref Node n0 = ref mNode[a], n1 = ref mNode[b];
         if (n0.NPoly != nLastPoly) { nStart = a; nLastPoly = n0.NPoly; }
         n0.Next = (n0.NPoly == n1.NPoly) ? b : nStart;
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
      public bool Used;
   }

   // Represents an intersection point
   readonly struct Inter (Point2 pt, int poly, int seg, double lie) {
      public readonly Point2 Pt = pt;     // Intersection point
      public readonly double Lie = lie;   // Lie of this point along the bendline
      public readonly ushort NPoly = (ushort)poly;  // Poly this connects to
      public readonly ushort NSeg = (ushort)seg;    // Segment on that poly

      public readonly override string ToString () => $"Inter Poly:{NPoly} Lie:{NSeg + Lie.Round (3)} {Pt.R6 ()}";
   }

   // Node is a junction between a poly and bendline.
   // This is a location where a bendline touches a poly (at its endpoint). 
   struct Node {
      public readonly Point2 GetPos (CPoly[] polys) => polys[NPoly].Poly[NSeg].GetPointAt (Lie);
      public readonly override string ToString () => $"Node Poly:{NPoly} Lie:{NSeg + Lie.Round (3)}";

      // Index of the bendline (within mBends), and the point (with Bend.Pts array)
      public int NBend, NPt;
      // Index of Poly, segment within that poly, and lie within that segment
      public int NPoly, NSeg;
      public double Lie;
      // Next node within this poly (circular linked list)
      public int Next;
      // Face connected to the half-edge starting at this node
      public int NFace; 
      public bool UsedInFace;    // Already used to build a face
   }

   // Represents a bend-line 
   struct Bend {
      public Bend (E2Bendline bend) => BLine = bend;
      public readonly E2Bendline BLine;
      public int NBase;          // Nodes of this Bend start at this location
      public Vector2 Delta;
   }

   // Represents a plane with some holes
   struct Face (Poly outer, int[] edges) {
      public readonly Poly Outer = outer;       // Outer poly of this face
      public readonly Bound2 Bound = outer.GetBound ();
      public readonly List<Poly> Holes = [];    // Hole polys of this face
      public bool UsedInModel;      // Already used this face to build the model

      // Edges is the list of half-bends touching this face, and Children is a parallel
      // list of faces on the 'other side' of these bends. Some of these might be -1
      // (since the face on the other side is already reached via another bend). We
      // build the Bends list when building the faces, and the Children list later when
      // traversing the tree
      public readonly int[] Edges = edges;
      public readonly int[] Children = new int[edges.Length];
   }

   // Private data -------------------------------------------------------------
   CPoly[] mPolys = [];
   Node[] mNode = []; int mNNode;
   Bend[] mBends = [];
   Face[] mFaces = []; int mNFace;
   List<Poly> mOutput = [];
   int mRootFace;
}
#endregion
