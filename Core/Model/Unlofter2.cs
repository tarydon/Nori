// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Unlofter2.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Reactive.Subjects;
namespace Nori;

public partial class SurfaceUnlofter {
   public SurfaceUnlofter (E3Surface surf) {
      mDomain = (mSurf = surf).Domain;

      // Create the initial subdivision of 4 x 4 tiles
      double du = mDomain.X.Length / mUDivs, dv = mDomain.Y.Length / mVDivs;
      double uMin = mDomain.X.Min, vMin = mDomain.Y.Min;
      for (int j = 0; j < mVDivs; j++) {
         double v = vMin + (j + 0.5) * dv;          // Center V of the tile
         for (int i = 0; i < mUDivs; i++) {
            double u = uMin + (i + 0.5) * du;       // Center U of the tile
            int node = AddNode (u, v);
            AddTile (-1, node, du / 2, dv / 2, EDir.Root);
         }
      }
      mSubject?.OnNext (this);
      mRootTiles = mUsedTiles;
   }
   int mUDivs = 4, mVDivs = 4;

   public static long Interpolate = 0; 

   public void DumpStats () {
      int cb = mNodes.Length * Marshal.SizeOf<Node> ();
      cb += mTiles.Length * Marshal.SizeOf<Tile> ();
      cb += mProjNodes.Capacity * Marshal.SizeOf<Point2> ();
      Console.WriteLine ($"{cb / 1024} Kb allocated, {Interpolate} evals");
   }

   public Point2 GetUV (Point3 pt) {
      mRung++;
      int iRoot = -1;
      double minDist = double.MaxValue;
      for (int i = 0; i < mRootTiles; i++) {
         ref Node node = ref mNodes[i];
         double dist = pt.DistToSq (node.Pt);
         if (dist < minDist) (minDist, iRoot) = (dist, i);
      }
      mSubject?.OnNext (this);

      // Try this 'best bet' tile. Very often, the point in question
      // will lie within this tile, and we are done - this is the fast happy path
      var (uvBest, nLeaf, overrun) = GetUV (iRoot, pt);
      if (overrun == EDir.Nil) return uvBest;

      // The uv computed did not lie within the tile boundary, so we might need
      // to explore some neighboring tiles
      mQueue.Clear (); mUVAlts.Clear ();
      AddNeighbors (nLeaf, overrun);
      while (mQueue.Count > 0) {
         int nTile2 = mQueue.Dequeue ();
         var (uv2, leaf2, overrun2) = GetUV (nTile2, pt);
         Log ($"   Using {nTile2}, leaf = {leaf2}, overrun = {overrun2}");
         if (overrun2 == EDir.Nil) return uv2;
         mUVAlts.Add (uv2);
         AddNeighbors (leaf2, overrun2);
      }

      double minError = pt.DistToSq (mSurf.GetPoint (uvBest.X, uvBest.Y));
      for (int i = mUVAlts.Count - 1; i >= 0; i--) {
         var uv = mUVAlts[i];
         double error = pt.DistToSq (mSurf.GetPoint (uv.X, uv.Y));
         if (error < minError) (minError, uvBest) = (error, uv);
      }
      return uvBest;
   }
   Queue<int> mQueue = [];
   List<Point2> mUVAlts = [];
   int mRung;

   void AddNeighbors (int n, EDir dir) {
      if (n == -1) return;
      ref Tile tile = ref mTiles[n];
      if ((dir & EDir.W) != 0) {
         // See if there is a tile to the west of this at this same level. If none are found 
         // at this level, we move up to the parent and try. If this is already at the root level,
         // we use special 'grid logic' to figure out the potential neighbor in that direction. 
         switch (tile.Location) {
            case EDir.E or EDir.SE: Add (n - 1); break;
            case EDir.NE: Add (n + 1); break;   // Ordering with 4 tiles = SW SE NE NW
            case EDir.Root: int col = n % mUDivs; if (col > 0) Add (n - 1); break;
            default: AddNeighbors (tile.Parent, EDir.W); break;   
         }
      } else if ((dir & EDir.E) != 0) {
         // See if there is a tile to the east of this
         switch (tile.Location) {
            case EDir.W or EDir.SW: Add (n + 1); break;
            case EDir.NW: Add (n - 1); break;
            case EDir.Root: int col = n % mUDivs; if (col < mUDivs - 1) Add (n + 1); break;
            default: AddNeighbors (tile.Parent, EDir.E); break;
         }
      }

      if ((dir & EDir.S) != 0) {
         // Add the possible tile to the south of this
         switch (tile.Location) {
            case EDir.N or EDir.NE: Add (n - 1); break;
            case EDir.NW: Add (n - 3); break;
            case EDir.Root: int row = n / mUDivs; if (row > 0) Add (n - mUDivs); break;
            default: AddNeighbors (tile.Parent, EDir.S); break;
         }
      } else if ((dir & EDir.N) != 0) {
         switch (tile.Location) {
            case EDir.S or EDir.SE: Add (n + 1); break;
            case EDir.SW: Add (n + 3); break;
            case EDir.Root: int row = n / mUDivs; if (row < mVDivs - 1) Add (n + mUDivs); break;
            default: AddNeighbors (tile.Parent, EDir.N); break;
         }
      }

      // Helpers ...........................................
      void Add (int nTile) {
         ref Tile tile = ref mTiles[nTile];
         if (tile.Rung != mRung) {
            ref Tile outer = ref mTiles[n];
            Log ($"Add {dir} of {n} ({outer.Location} child of {outer.Parent}) : {nTile}");
            tile.Rung = mRung; mQueue.Enqueue (nTile); 
         }
      }
   }

   partial void Log (string s);
   // partial void Log (string s) => Debug.WriteLine (s);

   (Point2 UV, int Leaf, EDir dir) GetUV (int nTile, Point3 pt) {
      for (; ; ) {
         EState state = CheckAndSubdivide (nTile);
         ref Tile tile = ref mTiles[nTile];
         tile.Rung = mRung;
         switch (state) {
            case EState.Subdivide2 or EState.Subdivide4:
               int iBest = -1; double minDist = double.MaxValue;
               for (int i = 0; i < (int)state; i++) {
                  int n = tile.Children + i;
                  ref Tile tileN = ref mTiles[n];
                  ref Node node = ref mNodes[tileN.Center];
                  double dist = pt.DistToSq (node.Pt);
                  if (dist < minDist) (minDist, iBest) = (dist, n);
               }
               nTile = iBest;
               break;
            default:
               Interpolate++;
               return tile.GetUV (this, pt);
         }
      }
   }

   EState CheckAndSubdivide (int nTile) {
      ref Tile tile = ref mTiles[nTile];
      if (tile.State == EState.Raw) {
         GrowArrays ();
         ref Tile tileN = ref mTiles[nTile];
         tileN.Subdivide (this);
         return tileN.State;
      }
      return tile.State;
   }

   public record struct Label (Vec3F Pos, string Text);

   public (List<Vec3F> Lines, List<Vec3F> Points, List<Label> labels) GetTileOutlines () {
      List<Vec3F> lines = [], points = [];
      List<Label> labels = [];
      for (int i = 0; i < mRootTiles; i++) {
         ref Tile tile = ref mTiles[i];
         Process (ref tile);
      }
      return (lines, points, labels);

      // Helpers ...........................................
      void Process (ref Tile tile) {
         Vec3F cen = (Vec3F)mNodes[tile.Center].Pt;
         switch (tile.State) {
            case EState.Raw:
               points.Add (cen);
               labels.Add (new Label (cen, $"{tile.Id}"));
               break;
            case EState.Subdivide2 or EState.Subdivide4:
               labels.Add (new Label (cen, $"{tile.Id}"));
               foreach (int n in new int[] { 0, 1, 1, 2, 2, 3, 3, 0 })
                  lines.Add ((Vec3F)mNodes[tile.Corners[n]].Pt);
               for (int i = 0; i < (int)tile.State; i++) {
                  ref Tile child = ref mTiles[tile.Children + i];
                  Process (ref child);
               }
               break;
            default:
               labels.Add (new Label (cen, $"{tile.Id}"));
               foreach (int n in new int[] { 0, 1, 1, 2, 2, 3, 3, 0 }) 
                  lines.Add ((Vec3F)mNodes[tile.Corners[n]].Pt);
               break;
         }
      }
   }

   // Properties ---------------------------------------------------------------
   public static IObservable<SurfaceUnlofter> NewTile => (mSubject ??= new ());
   static Subject<SurfaceUnlofter>? mSubject;

   // Implementation -----------------------------------------------------------
   // Since we're working with structs for efficiency, we have to be very careful to 
   // ensure that methods that modify these structs, or take refernces to structs 
   // while growing these arrays (mNodes, or mTiles) do not cause any growing to happen 
   // while these methods are executing. 
   // This can be guaranteed by ensuring that mNodes always has space for at least 12
   // nodes more than what we have used at any point, and mTiles always has space for
   // atleast 4 more tiles (when we return from AddNode or AddTile)
   int AddNode (double u, double v) {
      if (mUsedNodes >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mNodes[mUsedNodes] = new Node (mSurf, u, v);
      return mUsedNodes++;
   }

   int AddTile (int parent, int center, double du, double dv, EDir location) {
      if (mUsedTiles >= mTiles.Length) Array.Resize (ref mTiles, mTiles.Length * 2);
      mTiles[mUsedTiles] = new Tile (mUsedTiles, parent, center, du, dv, location);
      return mUsedTiles++;
   }

   int AddTile (int parent, int center, double du, double dv, int botLeft, int botRight, int topRight, int topLeft, EDir location) {
      int nTile = AddTile (parent, center, du, dv, location);
      ref Tile tile = ref mTiles[nTile];
      var corners = tile.Corners;
      corners[0] = botLeft; corners[1] = botRight;
      corners[2] = topRight; corners[3] = topLeft;
      return nTile;
   }

   void GrowArrays () {
      if (mUsedNodes + 12 >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      if (mUsedTiles + 4 >= mTiles.Length) Array.Resize (ref mTiles, mTiles.Length * 2);
   }

   // Nested types -------------------------------------------------------------
   // Used to indicate one of the 8 cardinal directions
   enum EDir {
      Nil, W = 1, E = 2, S = 4, N = 8, SW = 5, SE = 6, NE = 10, NW = 9, Root = 100
   }

   readonly struct Node {
      public Node (E3Surface surf, double u, double v) {
         U = (float)u; V = (float)v;
         Pt = surf.GetPoint (u, v);
      }

      public override string ToString () => $"Node ({U},{V})";

      public readonly float U, V;  // U, V position of this Node
      public readonly Point3 Pt;
      public Point2 UV => new (U, V);
   }
   Node[] mNodes = new Node[16];
   int mUsedNodes;

   List<Point2> mProjNodes = [];

   [InlineArray (4)]
   struct FourInts { int _elem0; }

   enum EState { 
      Raw = 0, Leaf = 1, Subdivide2 = 2, Subdivide4 = 4,
      LeafXY = 5, LeafYZ = 6, LeafXZ = 7  
   };

   struct Tile {
      public Tile (int id, int parent, int center, double du, double dv, EDir location) {
         (Id, Parent, Center, DU, DV, Location) = (id, parent, center, du, dv, location);
         for (int i = 0; i < 4; i++) Corners[i] = -1; Children = -1;
      }

      public Bound2 GetUVBound (SurfaceUnlofter owner) {
         ref Node center = ref owner.mNodes[Center];
         return new (center.U - DU, center.V - DV, center.U + DU, center.V + DV);
      }

      public override string ToString () => $"Tile#{Id} State:{State} Loc:{Location}";

      public int Rung;

      public (Point2 UV, int Id, EDir Overrun) GetUV (SurfaceUnlofter owner, Point3 pt) {
         if (State == EState.Leaf) {
            // If we are in 'Leaf' state, we haven't determined a suitable projection
            // axis yet (and the corners may not yet have been evaluated).
            // First, evaluate the nodes
            var nodes = owner.mNodes;
            ref Node center = ref nodes[Center];
            double uCen = center.U, vCen = center.V;

            // First, if any of the corner nodes have not yet been evaluated,
            // evaluate those
            for (int i = 0; i < 4; i++) {
               if (Corners[i] < 0) {
                  var (uNew, vNew) = i switch {
                     0 => (uCen - DU, vCen - DV),
                     1 => (uCen + DU, vCen - DV),
                     2 => (uCen + DU, vCen + DV),
                     _ => (uCen - DU, vCen + DV)
                  };
                  int n = owner.AddNode (uNew, vNew);
                  Corners[i] = n;
               }
            }
            nodes = owner.mNodes;
            Point3 bl = nodes[Corners[0]].Pt, br = nodes[Corners[1]].Pt, tl = nodes[Corners[3]].Pt;
            Vector3 norm = (br - bl) * (tl - bl);
            // Figure out which of the two axes we want to project through
            double dx = Math.Abs (norm.X), dy = Math.Abs (norm.Y), dz = Math.Abs (norm.Z);
            if (dx >= dy && dx >= dz) State = EState.LeafYZ;
            else if (dy >= dx && dy >= dz) State = EState.LeafXZ;
            else State = EState.LeafXY;

            // Depending on the State, get 2D projections of the 4 corners and
            // add them in
            NProject = owner.mProjNodes.Count;
            for (int i = 0; i < 4; i++) {
               var pt3 = nodes[Corners[i]].Pt;
               Point2 pt2 = State switch {
                  EState.LeafXY => new (pt3.X, pt3.Y),
                  EState.LeafXZ => new (pt3.X, pt3.Z),
                  EState.LeafYZ => new (pt3.Y, pt3.Z),
                  _ => throw new NotImplementedException ()
               };
               owner.mProjNodes.Add (pt2);
            }
         }

         var pnodes = owner.mProjNodes;
         Point2 s = State switch {
            EState.LeafXY => new (pt.X, pt.Y),
            EState.LeafXZ => new (pt.X, pt.Z),
            _ => new (pt.Y, pt.Z)
         };
         Point2 a = pnodes[NProject + 0], b = pnodes[NProject + 1];
         Point2 c = pnodes[NProject + 2], d = pnodes[NProject + 3];
         Vector2 e = b - a, f = d - a, g = (a - b) + (c - d), h = s - a;

         //var dwg = new Dwg2 ();
         //dwg.Add (Poly.Lines ([a, b, c, d]).Close ());
         //dwg.Add (s);
         //DXFWriter.Save (dwg, "c:/etc/test.dxf"); REMOVETHIS

         // Compose the quadratic for v
         double u, v;
         double k2 = g.X * f.Y - g.Y * f.X;
         double k1 = e.X * f.Y - e.Y * f.X + g.Y * h.X - g.X * h.Y;
         double k0 = h.X * e.Y - e.X * h.Y;
         if (k2.IsZero ()) {
            // If k2 is zero, then the 4 points make up a rectangle, and we have actually
            // a linear equation.
            if (k1.IsZero ()) v = 0.5; else v = -k0 / k1;
         } else {
            // If k2 is not zero, we have proper quadrilateral
            double tmp = k1 * k1 - 4 * k0 * k2;
            tmp = Math.Sqrt (Math.Max (tmp, 0));
            double v1 = (-k1 + tmp) / (2 * k2), v2 = (-k1 - tmp) / (2 * k2);
            v = Math.Abs (v1 - 0.5) < Math.Abs (v2 - 0.5) ? v1 : v2;
         }

         // Now that we know v, compute the value for u
         double d1 = e.X + g.X * v, d2 = e.Y + g.Y * v;
         if (Math.Abs (d1) > Math.Abs (d2))
            u = (h.X - f.X * v) / d1;
         else
            u = (h.Y - f.Y * v) / d2;

         ref Node center1 = ref owner.mNodes[Center];
         double left = center1.U - DU, right = center1.U + DU;
         double bottom = center1.V - DV, top = center1.V + DV;
         EDir overrun = EDir.Nil;
         if (u < 0) overrun |= EDir.W; else if (u > 1) overrun |= EDir.E;
         if (v < 0) overrun |= EDir.S; else if (v > 1) overrun |= EDir.N;
         return (new (u.Along (left, right), v.Along (bottom, top)), Id, overrun);
      }

      const double Tolerance = 0.0001;

      public void Subdivide (SurfaceUnlofter owner) {
         // If we get here, we are still in the 'raw' state (first time this tile
         // been used). We need to first figure out if the tile is 'flat enough', or
         // needs to be subdivided. Let's evaluate the corners first
         var nodes = owner.mNodes;
         ref Node center = ref nodes[Center];
         double uCen = center.U, vCen = center.V;
         // First, if any of the corner nodes have not yet been evaluated,
         // evaluate those
         for (int i = 0; i < 4; i++) {
            if (Corners[i] < 0) {
               var (u, v) = i switch {
                  0 => (uCen - DU, vCen - DV),
                  1 => (uCen + DU, vCen - DV),
                  2 => (uCen + DU, vCen + DV),
                  _ => (uCen - DU, vCen + DV)
               };
               Corners[i] = owner.AddNode (u, v);
            }
         }

         // To check if we need to subdivide, we check the perpendicular distance of the
         // center point of the tile from each of its diagonals. If the center deviates too
         // much from either of them, the tile is still too curved and needs to be subdivided 
         Point3 ptCen = center.Pt;
         Point3 botLeft = nodes[Corners[0]].Pt, topRight = nodes[Corners[2]].Pt;
         Point3 botRight = nodes[Corners[1]].Pt, topLeft = nodes[Corners[3]].Pt;
         bool subdivide = ptCen.DistToLineSq (botLeft, topRight) > Tolerance 
                       || ptCen.DistToLineSq (botRight, topLeft) > Tolerance;
         if (!subdivide) { State = EState.Leaf; return; }

         // We need to subdivide. Let's see if a U sibdivision is needed first
         int nLeft = owner.AddNode (uCen - DU, vCen), nRight = owner.AddNode (uCen + DU, vCen);
         int nBottom = owner.AddNode (uCen, vCen - DV), nTop = owner.AddNode (uCen, vCen + DV);
         Point3 left = nodes[nLeft].Pt, right = nodes[nRight].Pt;
         Point3 bottom = nodes[nBottom].Pt, top = nodes[nTop].Pt;
         bool divideU = ptCen.DistToLineSq (left, right) > Tolerance
                     || bottom.DistToLineSq (botLeft, botRight) > Tolerance
                     || top.DistToLineSq (topLeft, topRight) > Tolerance;
         bool divideV = ptCen.DistToLineSq (bottom, top) > Tolerance
                     || left.DistToLineSq (botLeft, topLeft) > Tolerance
                     || right.DistToLineSq (botRight, topRight) > Tolerance;
         if (!divideU && !divideV) divideU = divideV = true;

         double uStep = DU / 2, vStep = DV / 2;
         if (divideU && divideV) {
            State = EState.Subdivide4;
            // Divide in both U and V. We're going to create 4 smaller tiles here. 
            // Because of all the checking above, we've actually added all the nodes
            // needed to define the corners of these 4 tiles already, so we can 
            // reuse those same Nodes. The only new node we need to create is the 
            // one for the center
            // 1. Bottom left quarter-tile
            int nCenter = owner.AddNode (uCen - uStep, vCen - vStep);
            Children = owner.AddTile (Id, nCenter, uStep, vStep, Corners[0], nBottom, Center, nLeft, EDir.SW);
            // 2. Bottom right quarter-tile
            nCenter = owner.AddNode (uCen + uStep, vCen - vStep);
            owner.AddTile (Id, nCenter, uStep, vStep, nBottom, Corners[1], nRight, Center, EDir.SE);
            // 3. Top right quarter-tile
            nCenter = owner.AddNode (uCen + uStep, vCen + vStep);
            owner.AddTile (Id, nCenter, uStep, vStep, Center, nRight, Corners[2], nTop, EDir.NE);
            // 4. Top left quarter-tile
            nCenter = owner.AddNode (uCen - uStep, vCen + vStep);
            owner.AddTile (Id, nCenter, uStep, vStep, nLeft, Center, nTop, Corners[3], EDir.NW);
         } else if (divideU) {
            // Divide only in U
            State = EState.Subdivide2;
            // There is curvature only in the U direction, and not enough in V. So we 
            // create 2 half tiles by slicing vertically
            // 1. Left half-tile
            int nCenter = owner.AddNode (uCen - uStep, vCen);
            Children = owner.AddTile (Id, nCenter, uStep, DV, Corners[0], nBottom, nTop, Corners[3], EDir.W);
            // 2. Right half-tile
            nCenter = owner.AddNode (uCen + uStep, vCen);
            owner.AddTile (Id, nCenter, uStep, DV, nBottom, Corners[1], Corners[2], nTop, EDir.E);
         } else {
            // Divide only in V
            State = EState.Subdivide2;
            // There is curvature only the V direction, and not enough in U. So we create
            // two half-tiles by slicing horizontally
            // 1. Bottom half-tile
            int nCenter = owner.AddNode (uCen, vCen - vStep);
            Children = owner.AddTile (Id, nCenter, DU, vStep, Corners[0], Corners[1], nRight, nLeft, EDir.S);
            // 2. Top half-tile
            nCenter = owner.AddNode (uCen, vCen + vStep);
            owner.AddTile (Id, nCenter, DU, vStep, nLeft, nRight, Corners[2], Corners[3], EDir.N);
         }
         mSubject?.OnNext (owner);
         return;
      }

      public EState State;
      public readonly int Id;
      public readonly int Parent;
      public readonly int Center;      // Index of the node at the center of this tile
      public readonly double DU, DV;   // Half-span in U and V of this tile 
      public FourInts Corners;         // The 4 'corner nodes' of this tile (-1 means not evaluated)
      public int Children;             // Children of this tile start from this index
      public int NProject;
      public readonly EDir Location;   // Position of this tile within the parent's set of children
   }
   Tile[] mTiles = new Tile[16];
   int mUsedTiles, mRootTiles;

   // Private data -------------------------------------------------------------
   readonly E3Surface mSurf;
   readonly Bound2 mDomain;
}
