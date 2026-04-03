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

   // Properties ---------------------------------------------------------------
   /// <summary>Result if the folder returns false</summary>
   public EResult Result { get; private set; }

   // Methods ------------------------------------------------------------------
   /// <summary>Construct the Model3 from the drawing</summary>
   public bool Process ([NotNullWhen (true)] out Model3? model) {
      model = null; Result = EResult.OK;
      if ((Result = GatherContours ()) != EResult.OK) return false;
      if (mBends.Length == 0) { Result = EResult.NoBendLines; return false; }
      for (int i = 0; i < mBends.Length; i++)
         if ((Result = SnapBendline (i)) != EResult.OK) return false;
      if ((Result = CheckBendIntersections ()) != EResult.OK) return false;
      LinkNodesPerPoly ();
      MakeFaces (); 
      GatherClusters ();
      ReparentClusters (); 
      AssignHoles ();
      CreateTree ();
      return (Result = CreateModel (out model)) == EResult.OK;
   }

   // Implementation -----------------------------------------------------------
   // Adds holes into their owner faces
   void AssignHoles () {
      // Now, we can take all the holes and move them into the smallest enclosing face
      for (int i = 0; i < mNPoly; i++) {
         ref CPoly cp = ref mPolys[i]; if (cp.UsedInFace) continue;
         int nEnclosing = GetFaceEnclosing (cp.Poly, cp.Bound);
         if (nEnclosing != -1) mFaces[nEnclosing].Holes.Add (cp.Poly);
      }
   }

   // Now all the faces have been created. We can gather 'clusters' of connected faces. 
   // Two faces are 'connected' if they share a common bendline between them. The resulting
   // clusters list is sorted biggest to smallest. 
   // 
   // See file://N:/Doc/Img/PaperFolder.png
   // In this image, there is an outer polyline that has two bendlines touching it (creating
   // two flanges). That creates one cluster with these 3 faces. 
   // 
   // Then, there is an inner hole which has two flanges within it - this is another 
   // separate cluster. Note that this is not part of the first cluster, since there is no bendline
   // connecting any of the 1st cluster polys with any of the 2nd cluster polys. A drawing can thus
   // be broken into one or more clusters like this (more than 1 cluster will result only if there
   // are flanges inside holes, as in this example). 
   // 
   // This routine creates Cluster objects, each of which is just a collection of faces, and 
   // orders them so the largest cluster (by bound) is the first one. All other clusters will 
   // then have to be 'fitted into' other faces that enclose them - that is done by ReparentClusters
   // below. 
   void GatherClusters () {
      var (todo, faces) = (new Queue<int> (), new List<int> ());
      Gather (mRootFace);
      for (int i = 0; i < mNFace; i++) if (!mFaces[i].Tagged) Gather (i);
      mClusters.Sort ((a, b) => b.Bound.Area.CompareTo (a.Bound.Area));

      // Helper ............................................
      void Gather (int seed) {
         mFaces[seed].Tagged = true;
         todo.Enqueue (seed); faces.Clear ();
         while (todo.TryDequeue (out int nFace)) {
            ref Face face = ref mFaces[nFace]; faces.Add (nFace);
            foreach (var nNode in face.HBends) {
               ref Node pair = ref mNode[nNode ^ 1];
               ref Face oface = ref mFaces[pair.NFace];
               if (!oface.Tagged) { oface.Tagged = true; todo.Enqueue (pair.NFace); }
            }
         }
         mClusters.Add (new Cluster (faces, mFaces));
      }
   }
   List<Cluster> mClusters = [];

   // Given a Poly (and its bound), gets the smallest face enclosing it. 
   // Because of flanges in holes etc there could be multiple faces that all contain
   // this. However, the smallest one is the face in which this hole actually belongs
   int GetFaceEnclosing (Poly hole, Bound2 bound) {
      int iBest = -1; 
      for (int i = 0; i < mNFace; i++) {
         ref Face face = ref mFaces[i];
         if (face.Used || !face.Bound.Contains (bound)) continue;
         var (outer, inside) = (face.Outer, false);
         for (double lie = 0; lie < hole.Count - 0.001; lie += hole.Count / 17.0) {
            int n = (int)lie;
            int code = outer.Contains (hole[n].GetPointAt (lie - n));
            if (code == -1) continue;
            inside = code == 1; break;
         }
         if (!inside) continue; 
         if (iBest == -1 || face.Bound.Area < mFaces[iBest].Bound.Area)
            iBest = i; 
      }
      return iBest;
   }

   // Checks every pair of bend lines for intersections
   EResult CheckBendIntersections () {
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
                  if (!Geo.LineSegXLineSeg (s0, e0, s1, e1).IsNil) {
                     b0.BLine.IsError = b1.BLine.IsError = true;
                     return EResult.IntersectingBendlines;
                  }
               }
            }
         }
      }
      return EResult.OK;
   }

   // This creates a tree starting with a 'baseplane' and picking up adjacent planes
   // by connectivity. Each segment of each bendline is represented by two nodes on 
   // the two endpoints (consider them like two half-edges running in opposite directions)
   // These two half-edges point to the faces on opposite sides of the bendline, and thus
   // can be used to link them up
   void CreateTree () {
      Queue<int> todo = [];
      mFaces[mRootFace].Used = true; 
      todo.Enqueue (mRootFace);
      while (todo.TryDequeue (out int nFace)) {
         ref Face face = ref mFaces[nFace];
         // 1. Go through each of the half-edges connected to this face
         // 2. Get the paired half-edge on the other side (n0 ^ 1 does this)
         // 3. Get the face on the other side (mNode[n0 ^ 1].NFace)
         // 4. If not reached yet, add that face to the queue
         for (int i = 0; i < face.HBends.Count; i++) {
            int n0 = face.HBends[i], nFace2 = mNode[n0 ^ 1].NFace;
            ref Face face2 = ref mFaces[nFace2];
            if (face2.Used) { face.Children.Add (-1); continue; }
            face.Children.Add (nFace2); face2.Used = true;
            todo.Enqueue (nFace2);
         }
      }
      // At the end of this loop, we've built a tree (via the Face.Children list), starting
      // with mFace[mRootFace]. 
   }

   // This is called to create the model
   EResult CreateModel (out Model3? outModel) {
      var model = new Model3 (); outModel = null;
      Queue<(int Face, Matrix3 Xfm)> todo = [];
      todo.Enqueue ((mRootFace, Matrix3.Identity));
      List<Poly> polys = [];
      while (todo.TryDequeue (out var tup)) {
         // First, create a plane with this plane's outer contour and holes
         var xfm = tup.Xfm;
         ref Face face1 = ref mFaces[tup.Face];
         polys.Clear (); polys.Add (face1.Outer); polys.AddRange (face1.Holes);
         for (int i = 0; i < polys.Count; i++) {
            var poly = polys[i].Clean ();
            if (poly.Count <= 2 && !poly.HasArcs) return EResult.IllFormedDrawing;
            polys[i] = poly;
         }
         model.Ents.Add (E3Plane.Build (model.Ents.Count, polys, xfm.ToCS ()));

         // Next, visit each of the children that have not already been queued up, and
         // compute the transform for that child, and enqueue it
         for (int i = 0; i < face1.Children.Count; i++) {
            var (nFace2, nEdge) = (face1.Children[i], face1.HBends[i]);
            if (nFace2 == -1) continue;

            ref Face face2 = ref mFaces[nFace2];
            var bend = mBends[mNode[nEdge].NBend].BLine;
            double angle = bend.Angle; if ((nEdge & 1) == 0) angle = -angle;
            Point2 pa = bend.Pts[0], pb = bend.Pts[^1];
            var xfm1 = Matrix3.Rotation ((Point3)pa, (Point3)pb, angle);
            todo.Enqueue ((nFace2, xfm1 * xfm)); 
         }
      }
      outModel = model;
      return EResult.OK;
   }

   // Gathers all the contours, and bend-lines
   EResult GatherContours () {
      // Gather all the closed poly, with the outer one being at 0. The outer poly
      // is CCW, while the holes are CW
      if (!mDwg.MarkInOut ()) return EResult.NoOuterContour;

      // Gather all the closed Poly, and move the outer contour to index 0
      List<E2Poly> tmp = [.. mDwg.Ents.OfType<E2Poly> ().Where (Accept)];
      for (int i = 1; i < tmp.Count; i++)
         if (tmp[i].IsOuter) { (tmp[i], tmp[0]) = (tmp[0], tmp[i]); break; }
      mPolys = new CPoly[tmp.Count];
      for (int i = 0; i < tmp.Count; i++) {
         var e2p = tmp[i];
         var poly = e2p.Poly;
         var wind = poly.GetWinding ();
         if (e2p.IsOuter ^ (wind == Poly.EWinding.CCW)) poly = poly.Reversed ();
         mPolys[mNPoly++] = new (poly, e2p.Bound);
      }

      // Gather all the bendlines
      List<E2Bendline> bends = [.. mDwg.Ents.OfType<E2Bendline> ()];
      mBends = new Bend[bends.Count]; 
      for (int i = 0; i < bends.Count; i++) {
         var bline = bends[i];
         if (bline.Pts.Length.IsOdd ()) { bline.IsError = true; return EResult.BadBendline; }
         mBends[i] = new Bend (bline);
      }
      return EResult.OK;

      // Helpers ...........................................
      static bool Accept (E2Poly e2p)
         => e2p.Poly.IsClosed && e2p.Poly.GetWinding () != Poly.EWinding.Indeterminate;
   }

   // Makes a face, starting at the given node (this node is the start of a bendline)
   void MakeFaces () {
      for (int nNode = 0; nNode < mNNode; nNode++) {
         ref Node node = ref mNode[nNode];
         if (node.UsedInFace) continue; 

         // Start building a face, by alternately traversing between bends and contours.
         // We will travel along a bendline until we reach the node at the end. This is easy to 
         // find since we originally created these nodes in pairs from each bendline (start/end).
         // So the 'other node' of a bendline is just mNodes[nNode ^ 1].
         // At that node, we will cross over to a contour (using the data in the Node structure)
         // and travel along that contour until we get to the next node along that same Poly. 
         // Thanks to the work done in LinkNodes, that is already set up in mNodes[nNode].Next. 
         // Also, because we have already oriented the outer contour to run CCW, while the others
         // run CW, this returns slices of contours in a consistent direction (material always to
         // the left of each one). 
         mHBends.Clear ();
         var (pb, onBend) = (new PolyBuilder (), true);
         for (; ; ) {
            if (onBend) {
               // Travel along bendline, adding just a single line starting at this point
               if (node.UsedInFace) break;   // Looped back to the starting node
               (node.UsedInFace, node.NFace) = (true, mNFace);    // mNFace is the new face we're making
               pb.Line (node.GetPos (mPolys));
               // Gather the list of half-bends connected to this face (we will store this in 
               // Face.HBends when we create the face). That list, along with the parallel list
               // Face.Children will allow us to traverse the tree of faces
               mHBends.Add (nNode);
               node = ref mNode[nNode ^= 1];
            } else {
               // We travel along the contour, until we get to the next node in order (this is CCW for
               // outer contour, CW for inner holes)
               ref Node next = ref mNode[nNode = node.Next];
               ref var cpoly = ref mPolys[node.NPoly]; cpoly.UsedInFace = true;
               pb.AddSlice (cpoly.Poly, node.NSeg, node.Lie, next.NSeg, next.Lie, false);
               node = ref next;
            }
            onBend = !onBend;
         }
         Lib.Grow (ref mFaces, mNFace, 1);
         mFaces[mNFace++] = new Face (pb.Close ().Build (), [.. mHBends], -1);
      }

      // If the 'outer poly' has no bendlines connected to it, it will not get converted
      // to a Face at all. Handle that special case here. Also compute the 'root face' from
      // which the folding is going to start
      if (!mPolys[0].UsedInFace) {
         // If the outer poly has no bendlines touching it, the face we create from it
         // is the root face
         Lib.Grow (ref mFaces, mRootFace = mNFace, 1);
         mFaces[mNFace++] = new Face (mPolys[0].Poly, [], -1);
         mPolys[0].UsedInFace = true;
      } else
         mRootFace = mFaces.Take (mNFace).MaxIndexBy (a => a.Bound.Area);
   }
   // Temporary used to hold the list of bends connected to the face we are building
   List<int> mHBends = [];

   // This is called after GatherClusters. 
   // See file://N:/Doc/Img/PaperFolder.png. 
   // The first (outer-most) cluster is special and is left alone. All other clusters are
   // created from flanges within holes and are process thus:
   // - Find the largest face (by bound). That is effectively the 'hole' inside which all
   //   the other faces are to be housed. That is colored blue in the image above. 
   // - Find the smallest face that fully 'encloses' this hole - this is the large rectangular
   //   plane in the 1st cluster in the image above. Call this 'enclosing'
   // - Add all the faces (other than the largest one we found) as holes inside the 'enclosing'
   //   face
   // - For each half-bend in the largest face, find the corresponding co-bend, and add that
   //   to the HBends list of the enclosing face. 
   // This last step ensures that when we are gathering all the children of the enclosing face,
   // we will also gather the children connected to _holes_ within that enclosing face, and will
   // therefore pick up the two small faces labeled "Faces" in the bottom figure. 
   void ReparentClusters () {
      // For each cluster other than the largest, we have to 'reparent' them. 
      // These clusters are islands essentially flanges inside a hole of some other
      // plane.
      var span = mClusters.AsSpan ();
      for (int i = 1; i < span.Length; i++) {
         // Take the list of faces in the cluster. The largest one of these is basically
         // the 'hole', and the rest are flanges in the hole. Find the smallest enclosing
         // face that holds these. 
         ref readonly Cluster cluster = ref span[i];
         int largest = cluster.Faces.MaxBy (a => mFaces[a].Bound.Area);
         ref Face hole = ref mFaces[largest]; hole.Used = true;
         ref Face enclosing = ref mFaces[GetFaceEnclosing (hole.Outer, hole.Bound)];

         // All the half-bends owned by this hole - transfer them to the enclosing face,
         // and also add this hole as a hole into the enclosing face
         enclosing.HBends.AddRange (hole.HBends);
         enclosing.Holes.Add (hole.Outer);
      }
   }

   // Snaps bend-lines to begin/end exactly on contours (by trimming/extending them as
   // needed). We also detect here when bend-line spans cross polylines, and cut them up
   // at those intersections
   EResult SnapBendline (int nBend) {
      // First, mark the list of contours this bend could intersect. At this point, 
      // we also gather all the intersections of these contours with the infinite bend-line.
      mInters.Clear (); 
      ref Bend bend = ref mBends[nBend]; 
      Span<Point2> buffer = stackalloc Point2[2];
      var pts = bend.BLine.Pts; int nLastPt = pts.Length - 1;
      Point2 a = pts[0], b = pts[nLastPt];
      for (int nPoly = 0; nPoly < mNPoly; nPoly++) {
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
            if (((nNext - n) & 1) == 0) return EResult.BadBendline;
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
      return EResult.OK;
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
   // Represents a cluster of faces that are connected together 
   readonly struct Cluster {
      public Cluster (IList<int> ids, Face[] faces) {
         Faces = [.. ids];
         Bound = new (ids.Select (a => faces[a].Bound));
      }
      public override string ToString ()
         => $"Cluster {Faces.ToCSV ()} {(int)Bound.Width}x{(int)Bound.Height}";

      public readonly int[] Faces;
      public readonly Bound2 Bound;
   }

   // Represents an outer / inner contour of the poly.
   // The outer contour is CCW, while the rest are CW
   struct CPoly (Poly poly, Bound2 bound) {
      public readonly Poly Poly = poly;
      public readonly Bound2 Bound = bound;
      public bool Intersects;
      public bool UsedInFace;    // Has this Poly been used to build a Face?
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
      public bool Used; 
   }

   // Represents a plane with some holes
   struct Face (Poly outer, int[] hbends, int nHole) {
      public readonly Poly Outer = outer;       // Outer poly of this face
      public readonly Bound2 Bound = outer.GetBound ();
      public readonly List<Poly> Holes = [];    // Hole polys of this face
      public int NHole = nHole;     // If > 0, this face is connected to this hole
      public bool Tagged;           // Marker used in various algorithms
      public bool Used;             // This face is dead - don't consider it

      // HBends is the list of half-bends touching this face, and Children is a parallel
      // list of faces on the 'other side' of these bends. Some of these might be -1
      // (since the face on the other side is already reached via another bend). We
      // build the Bends list when building the faces, and the Children list later when
      // traversing the tree
      public readonly List<int> HBends = [.. hbends];
      public readonly List<int> Children = [];
   }

   // Private data -------------------------------------------------------------
   CPoly[] mPolys = []; int mNPoly;
   Node[] mNode = []; int mNNode;
   Bend[] mBends = [];
   Face[] mFaces = []; int mNFace;
   List<Poly> mOutput = [];
   int mRootFace;
}
#endregion
