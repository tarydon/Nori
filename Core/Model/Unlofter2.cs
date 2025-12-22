namespace Nori;

public class SurfaceUnlofter {
   public SurfaceUnlofter (E3Surface surf) {
      mDomain = (mSurf = surf).Domain;

      // Create the initial subdivision of 4 x 4 tiles
      int uDivs = 4, vDivs = 4;
      double du = mDomain.X.Length / uDivs, dv = mDomain.Y.Length / vDivs;
      for (int j = 0; j < vDivs; j++) {
         double v = (j + 0.5) * dv;          // Center V of the tile
         for (int i = 0; i < uDivs; i++) {
            double u = (i + 0.5) * du;       // Center U of the tile
            int node = AddNode (u, v);
            AddTile (-1, node, du, dv);
         }
      }
      mRootTiles = mUsedTiles;
   }

   public Point2 GetUV (Point3 pt) {
      int iBest = -1;
      double minDist = double.MaxValue;
      for (int i = 0; i < mRootTiles; i++) {
         ref Node node = ref mNodes[i];
         double dist = pt.DistToSq (node.Pt);
         if (dist < minDist) (minDist, iBest) = (dist, i);
      }
      ref Tile tile = ref mTiles[iBest];
      return tile.GetUV (this, pt);
   }

   // Implementation -----------------------------------------------------------
   int AddNode (double u, double v) {
      if (mUsedNodes >= mNodes.Length) Array.Resize (ref mNodes, mNodes.Length * 2);
      mNodes[mUsedNodes] = new Node (mSurf, mUsedNodes, u, v);
      return mUsedNodes++;
   }

   int AddTile (int parent, int center, double du, double dv) {
      if (mUsedTiles >= mTiles.Length) Array.Resize (ref mTiles, mTiles.Length * 2);
      mTiles[mUsedTiles] = new Tile (mUsedTiles, parent, center, du, dv);
      return mUsedTiles++;
   }

   int AddTile (int parent, int center, double du, double dv, int botLeft, int botRight, int topRight, int topLeft) {
      int nTile = AddTile (parent, center, du, dv);
      ref Tile tile = ref mTiles[nTile];
      var corners = tile.Corners;
      corners[0] = botLeft; corners[1] = botRight;
      corners[2] = topRight; corners[3] = topLeft;
      return nTile;
   }

   // Nested types -------------------------------------------------------------
   readonly struct Node {
      public Node (E3Surface surf, int id, double u, double v)
         => (Id, U, V, Pt) = (id, u, v, surf.GetPoint (new (u, v)));

      public readonly int Id;       // Node index (within mNodes array)
      public readonly double U, V;  // U, V position of this Node
      public readonly Point3 Pt;    // Corresponding 3D position of the node
   }
   Node[] mNodes = new Node[16];
   int mUsedNodes;

   [InlineArray (4)]
   struct FourInts { int _elem0; }

   enum EState { Raw = 0, Left = 1, Subdivide2 = 2, Subdivide4 = 4 };

   struct Tile {
      public Tile (int id, int parent, int center, double du, double dv) {
         (Id, Parent, Center, DU, DV) = (id, parent, center, du, dv);
         for (int i = 0; i < 4; i++) Corners[i] = Children[i] = -1;
      }

      public Point2 GetUV (SurfaceUnlofter owner, Point3 pt) {
         if (State == EState.Raw) Subdivide (owner);
         ref Node node = ref owner.mNodes[Center];
         return new (node.U, node.V);
      }

      void Subdivide (SurfaceUnlofter owner) {
         // If we get here, we are still in the 'raw' state (first time this tile
         // been used). We need to first figure out if the tile is 'flat enough', or
         // needs to be subdivided. Let's evaluate the corners first
         var nodes = owner.mNodes;
         double tolerance = Lib.FineTessSq;
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
         ref Node botLeft = ref nodes[Corners[0]], topRight = ref nodes[Corners[2]];
         ref Node botRight = ref nodes[Corners[1]], topLeft = ref nodes[Corners[3]];
         bool subdivide = ptCen.DistToLineSq (botLeft.Pt, topRight.Pt) > tolerance 
                       || ptCen.DistToLineSq (botRight.Pt, topLeft.Pt) > tolerance;
         if (!subdivide) { State = EState.Leaf; return; }

         // We need to subdivide. Let's see if a U sibdivision is needed first
         int nLeft = owner.AddNode (uCen - DU, vCen), nRight = owner.AddNode (uCen + DU, vCen);
         int nBottom = owner.AddNode (uCen, vCen - DV), nTop = owner.AddNode (uCen, vCen + DV);
         ref Node left = ref nodes[nLeft], right = ref nodes[nRight];
         ref Node bottom = ref nodes[nBottom], top = ref nodes[nTop];
         bool divideU = ptCen.DistToLineSq (left.Pt, right.Pt) > tolerance
                     || bottom.Pt.DistToLineSq (botLeft.Pt, botRight.Pt) > tolerance
                     || top.Pt.DistToLineSq (topLeft.Pt, topRight.Pt) > tolerance;
         bool divideV = ptCen.DistToLineSq (bottom.Pt, top.Pt) > tolerance
                     || left.Pt.DistToLineSq (botLeft.Pt, topLeft.Pt) > tolerance
                     || right.Pt.DistToLineSq (botRight.Pt, topRight.Pt) > tolerance;
         if (!(divideU || divideV)) { State = EState.Leaf; return; }

         double uStep = DU / 2, vStep = DV / 2;
         var tiles = owner.mTiles;
         if (divideU && divideV) {
            State = EState.Subdivide4;
            // Divide in both U and V. We're going to create 4 smaller tiles here. 
            // Because of all the checking above, we've actually added all the nodes
            // needed to define the corners of these 4 tiles already, so we can 
            // reuse those same Nodes. The only new node we need to create is the 
            // one for the center
            // 1. Bottom left quarter-tile
            int nCenter = owner.AddNode (uCen - uStep, vCen - vStep);
            Children[0] = owner.AddTile (Id, nCenter, uStep, vStep, Corners[0], bottom.Id, Center, left.Id);
            // 2. Bottom right quarter-tile
            nCenter = owner.AddNode (uCen + uStep, vCen - vStep);
            Children[1] = owner.AddTile (Id, nCenter, uStep, vStep, bottom.Id, Corners[1], right.Id, Center);
            // 3. Top right quarter-tile
            nCenter = owner.AddNode (uCen + uStep, vCen + vStep);
            Children[2] = owner.AddTile (Id, nCenter, uStep, vStep, Center, right.Id, Corners[2], top.Id);
            // 4. Top left quarter-tile
            nCenter = owner.AddNode (uCen - uStep, vCen - vStep);
            Children[3] = owner.AddTile (Id, nCenter, uStep, vStep, left.Id, Center, top.Id, Corners[3]);

         } else if (divideU) {
            // Divide only in U
            State = EState.Subdivide2;
            // There is curvature only in the U direction, and not enough in V. So we 
            // create 2 half tiles by slicing vertically
            // 1. Left half-tile
            int nCenter = owner.AddNode (uCen - uStep, vCen);
            Children[0] = owner.AddTile (Id, nCenter, uStep, DV, Corners[0], bottom.Id, top.Id, Corners[3]);
            // 2. Right half-tile
            nCenter = owner.AddNode (uCen + uStep, vCen);
            Children[1] = owner.AddTile (Id, nCenter, uStep, DV, bottom.Id, Corners[1], Corners[2], top.Id);

         } else {
            // Divide only in V
            State = EState.Subdivide2;
            // There is curvature only the V direction, and not enough in U. So we create
            // two half-tiles by slicing horizontally
            // 1. Bottom half-tile
            int nCenter = owner.AddNode (uCen, vCen - vStep);
            Children[0] = owner.AddTile (Id, nCenter, DU, vStep, 
         }
         return;
      }

      public EState State;
      public readonly int Id;
      public readonly int Parent;
      public readonly int Center;      // Index of the node at the center of this tile
      public readonly double DU, DV;   // Half-span in U and V of this tile 
      public FourInts Corners;         // The 4 'corner nodes' of this tile (-1 means not evaluated)
      public FourInts Children;        // The 4 children of this tile (0 means child not existing)
   }
   Tile[] mTiles = new Tile[16];
   int mUsedTiles, mRootTiles;

   // Private data -------------------------------------------------------------
   readonly E3Surface mSurf;
   readonly Bound2 mDomain;
}
