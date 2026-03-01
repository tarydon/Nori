using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualBasic.Logging;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;
namespace Nori;

#region class Triangulator : nested types ----------------------------------------------------------
partial class Triangulator {
   // Enumerations ---------------------------------------------------------------------------------
   // EVKind lists the types of vertices
   enum EVertex { Regular, Valley, Mountain };
   // EKind lists the types of nodes
   enum ENode { Y, X, Leaf };

   // struct Layer ---------------------------------------------------------------------------------
   // Represents a Layer in the stitching process
   class Layer {
      public void Init (ref Tile left, ref Tile right) {
         mTiles.Clear (); mTiles.Add (left.Id); mTiles.Add (right.Id);
         mAbove.Clear (); AddN (mAbove, left.Top[0]); AddN (mAbove, left.Top[1]);
         mBelow.Clear (); AddN (mBelow, left.Bot[0]); AddN (mBelow, left.Bot[1]);
      }

      public void AddRights (Layer L1) {
         L1.mAbove.Add (mTiles[1]); mBelow.Add (L1.mTiles[1]);
      }

      public void Connect (ref Tile tBase, bool last) {
         ref Tile left = ref Add (ref tBase, mTiles[0]), right = ref Add (ref tBase, mTiles[1]);
         foreach (var n in mAbove) {
            ref Tile above = ref Add (ref tBase, n);
            above.DisconnectFrom (ref left);
            above.ConnectTo (ref left); above.ConnectTo (ref right);
         }
         if (last) {
            foreach (var n in mBelow) {
               ref Tile below = ref Add (ref tBase, n);
               left.DisconnectFrom (ref below);
               left.ConnectTo (ref below); right.ConnectTo (ref below);
            }
         }
      }
      List<int> mTiles = [], mAbove = [], mBelow = [];

      static void AddN (List<int> items, int n) {
         if (n > 0) items.Add (n); 
      }
   }

   // struct Segment -------------------------------------------------------------------------------
   // This represents a segment connecting two vertices a and b. We reorder the segment so that it
   // always runs from top to bottom (PA.Y > PB.Y internally). In addition, the segment has a 
   // PartOnLeft flag that indicates if the material lies to the left of the segment (as viewed from
   // the birds eye view, not from the segment's own perspective). 
   // We also compute Slope, XPrime etc to make it faster to evaluate the X value at a given Y
   readonly struct Segment {
      public Segment (int id, ref Vertex vBase, int a, int b) {
         Id = id;
         Point2 pa = Add (ref vBase, a).Pt, pb = Add (ref vBase, b).Pt;
         if (pa.EQ (pb, FINE)) throw new InvalidOperationException ();
         if (pa.Y < pb.Y) (A, B, PA, PB, PartOnLeft) = (b, a, pb, pa, true);
         else (A, B, PA, PB, PartOnLeft) = (a, b, pa, pb, false);
         Slope = (pa.X - pb.X) / (pa.Y - pb.Y);
         XPrime = PB.X - Slope * PB.Y;
      }

      // Get the X value at a given Y 
      public double GetX (double y) 
         => XPrime + Slope * y;

      // Get the X values at a couple of Ys
      public (double, double) GetX (double y1, double y2)
         => (XPrime + Slope * y1, XPrime + Slope * y2);

      // Is the given point to the 'left' of this segment?
      public bool IsLeft (Point2 p)
         => (PA.X - PB.X) * (p.Y - PB.Y) - (PA.Y - PB.Y) * (p.X - PB.X) > 0;

      public override string ToString ()
         => $"Segment {A}..{B}, Left:{PartOnLeft}";

      public readonly int Id;             // Index of the segment in the mS array
      public readonly int A, B;           // Indicies of the top and bottom vertex
      public readonly Point2 PA, PB;      // Actual positions of the top and bottom vertex
      public readonly double Slope;       // Slope of the segment
      public readonly double XPrime;      // 'Intercept' used to simplify GetX computation
      public readonly bool PartOnLeft;    // Does the part lie on the left of the segment (as viewed by user)
   }

   // struct Vertex --------------------------------------------------------------------------------
   // This represents a Vertex in the tessellation (a node picked from one of the Poly inputs)
   // Each vertex is classified as Regular, Mountain or Valley. A regular vertex has one neighbor
   // above and one neighbor below it (in Y). A mountain vertex has both neighbors below it (lower Y).
   // Because of our constraints, there is only one vertex ever at this given value of Y. 
   struct Vertex {
      public Vertex (int id, Point2 pt, EVertex kind = EVertex.Regular) 
         => (Id, Pt, Kind) = (id, pt, kind);

      public readonly override string ToString ()
         => $"Vertex#{Id} {Pt} : {Kind}";

      public readonly int Id;          // Index of the vertex in the mV array
      public readonly Point2 Pt;       // Point location of the vertex
      public readonly EVertex Kind;    // What kind of vertex is this?
      public InlineArray2<int> Tile;   // Two tiles touching this vertex
      public bool Inserted;
   }

   struct Node {
      public Node (int id, ENode kind, int index) => (Id, Kind, Index) = (id, kind, index);

      public readonly int Id;    // Index of this node within the mN array
      public ENode Kind;         // What kind of node is this? (X split / Y split / leaf)
      public int First;          // First child (lower / left)
      public int Second;         // Second child (upper / right)
      public int Index;          // Pointer to a Vertex / Segment / Tile depending on the node type
   }

   struct Tile {
      public Tile (int id, ref Segment s0, double yMin, double yMax, int left, int right, int node) {
         (Id, YMin, YMax, Left, Right, Node) = (id, yMin, yMax, left, right, node);
         ref Segment L = ref Add (ref s0, left), R = ref Add (ref s0, right);
         Hole = L.PartOnLeft; Check (L.PartOnLeft != R.PartOnLeft);
         (LMin, LMax) = L.GetX (yMin, yMax);
         (RMin, RMax) = R.GetX (yMin, yMax);
      }

      public Tile (int id, Tile t, int node) {
         (Id, YMin, YMax, Left, Right, Node, Hole) = (id, t.YMin, t.YMax, t.Left, t.Right, node, t.Hole);
         (LMin, LMax, RMin, RMax) = (t.LMin, t.LMax, t.RMin, t.RMax);
      }

      // Connects the tile t0 (ABOVE) to a tile t1 (BELOW), if they share any common
      // overlap area
      public void ConnectTo (ref Tile t1) {
         Check (YMin.EQ (t1.YMax));
         if (Bot[0] == t1.Id || Bot[1] == t1.Id) return; // Already connected

         // Set left and right to be the overlapping segment between these two tiles
         double left = t1.LMax, right = t1.RMax;
         if (LMin > left) left = LMin;
         if (RMin < right) right = RMin;
         if (right < left + FINE) return;

         // We found these two tiles are overlapping, connect them up
         UpdateBottom (0, ref t1);
         t1.UpdateTop (0, ref this);
      }

      // This disconnects this tile (the ABOVE) tile from t1 (the BELOW tile). 
      // If there are links between these two tiles, those links are set to 0 (which means NIL). 
      // Note that if only one of Top or Bot is used, we ensure that is Top[0] or Bot[0], and the
      // other one (Top[1], Bot[1]) is set to null.
      public void DisconnectFrom (ref Tile t1) {
         if (Bot[0] == t1.Id) { Bot[0] = Bot[1]; Bot[1] = 0; } 
         else if (Bot[1] == t1.Id) Bot[1] = 0;
         if (t1.Top[0] == Id) { t1.Top[0] = t1.Top[1]; t1.Top[1] = 0; } 
         else if (t1.Top[1] == Id) t1.Top[1] = 0;
      }

      // Splits this tile either vertically or horizontally and returns the new Tile.
      // This also updates the tree structure 
      public ref Tile Split (Triangulator t, ENode kind, int index) {
         // Initially, there is a leaf node pointing to this tile. Update it to become
         // an interior node (with the given kind and index). Depending on the kind, the index
         // points to either a Vertex or a Segment
         ref Node nBase = ref GetReference (t.mN);
         ref Node leaf = ref Add (ref nBase, Node); Check (leaf.Kind == ENode.Leaf);
         leaf.Kind = kind; leaf.Index = index;

         // Next, create two child nodes to point to the two tiles (this tile updated, and
         // another new one we're going to create here)
         Add (ref nBase, t.mNN) = new (t.mNN, ENode.Leaf, Id);
         Add (ref nBase, t.mNN + 1) = new (t.mNN + 1, ENode.Leaf, t.mTN);
         Node = leaf.First = t.mNN; leaf.Second = t.mNN + 1;

         // Now ready to create the new tile
         ref Vertex vBase = ref GetReference (t.mV);
         ref Segment sBase = ref GetReference (t.mS);
         ref Tile tBase = ref GetReference (t.mT);
         Add (ref tBase, t.mTN) = new Tile (t.mTN, this, leaf.Second);
         ref Tile t1 = ref Add (ref tBase, t.mTN);

         if (kind == ENode.Y) {
            // Splitting at a point. The new tile t1 is going to be above
            double y = Add (ref vBase, index).Pt.Y;
            Check (YMin < y && y < YMax);
            t1.YMin = YMax = y;
            ref Segment L = ref Add (ref sBase, Left), R = ref Add (ref sBase, Right);
            t1.LMin = LMax = L.GetX (y); t1.RMin = RMax = R.GetX (y);

            if ((t1.VTop = VTop) > 0) {
               // This tile already has a 'top' vertex connected to it (and that top vertex
               // could be holding onto this tile as one of its two neighbor tiles). Update both links
               ref Vertex vTop = ref Add (ref vBase, VTop);
               if (vTop.Kind != EVertex.Valley) {
                  if (vTop.Tile[0] == Id) vTop.Tile[0] = t1.Id;
                  else if (vTop.Tile[1] == Id) vTop.Tile[1] = t1.Id;
               }
            }
            t1.VBot = VTop = index;
         } else {
            // Splitting at a segment. The new tile t1 is going to be on the right of the segment
            ref Segment seg = ref Add (ref sBase, index);
            if (Verify) {
               double yM = (YMin + YMax) / 2;
               ref Segment left = ref Add (ref sBase, Left), right = ref Add (ref sBase, Right);
               double xL = left.GetX (yM), x = seg.GetX (yM), xR = right.GetX (yM);
               Check (xL < x && x < xR);
            }
            t1.Left = Right = index;
            (RMin, RMax) = (t1.LMin, t1.LMax) = seg.GetX (YMin, YMax);
            Hole = !seg.PartOnLeft; t1.Hole = seg.PartOnLeft;
            if (VTop != 0) {
               // This tile already has a top vertex connected to it, and that top vertex
               // could be holding onto this tile as one of the two neighbor tiles). 
               ref Vertex vTop = ref Add (ref vBase, VTop);
               if (vTop.Kind == EVertex.Mountain) { vTop.Tile[0] = Id; vTop.Tile[1] = t1.Id; }

               // After the split, it's possible that the split line passes through VTop, 
               // to the left of VTop or the right of VTop. Depending on this, we decide which of the
               // two children (this + t1) will carry VTop with it
               if (seg.A == VTop) t1.VTop = VTop;  // Only case where seg passes through VTop
               else {
                  // If the VTop point is to the right of the slicing segment, then VTop should
                  // belong to t1
                  if (!seg.IsLeft (Add (ref vBase, VTop).Pt)) { t1.VTop = VTop; VTop = 0; }
               }
            }
            if (VBot != 0) {
               ref Vertex vBot = ref Add (ref vBase, VBot);
               if (vBot.Kind == EVertex.Valley) { vBot.Tile[0] = Id; vBot.Tile[1] = t1.Id; }

               // Figure out who carries VBot 
               if (seg.B == VBot) t1.VBot = VBot;  // BOth the tiles touch at VBot
               else if (!seg.IsLeft (Add (ref vBase, VBot).Pt)) { t1.VBot = VBot; VBot = 0; }
            }
         }
         t.mNN += 2; t.mTN++;
         return ref t1;
      }

      // Updates one of the 'bottom' connections of the tile
      public void UpdateBottom (int nOld, ref Tile tNew) {
         for (int i = 0; i < 2; i++)
            if (Bot[i] == nOld) {
               Bot[i] = tNew.Id;
               if (i == 1) {
                  // Ensure the bottom two tiles are 'sorted' (we want Bot[0] to 
                  // be the bottom LEFT neighbor, and Bot[1] to be the bottom RIGHT neighbor)
                  ref Tile tLeft = ref Add (ref tNew, Bot[0] - tNew.Id);
                  if (tLeft.XMax > tNew.XMax) 
                     (Bot[0], Bot[1]) = (Bot[1], Bot[0]);
               }
               return;
            }
         Unexpected ();
      }

      public void UpdateTop (int nOld, ref Tile tNew) {
         for (int i = 0; i < 2; i++)
            if (Top[i] == nOld) {
               Top[i] = tNew.Id;
               if (i == 1) {
                  // Ensure the top two tiles are 'sorted' (we want Top[0] to 
                  // be the top LEFT neighbor, and Top[1] to be the top RIGHT neighbor)
                  ref Tile tLeft = ref Add (ref tNew, Top[0] - tNew.Id);
                  if (tLeft.XMax > tNew.XMax)
                     (Top[0], Top[1]) = (Top[1], Top[0]);   // REMOVETHIS?
               }
               return;
            }
         Unexpected ();

      }

      public readonly int Id;             // Index of this within the mT array
      public double YMin, YMax;           // Range of Y values spanned by this tile
      public double LMin, LMax;           // Min & Max X on the left side
      public double RMin, RMax;           // Min & Max X on the right side
      public int Left, Right;             // Left and Right segment indices
      public InlineArray2<int> Top;       // Neighbors on the top
      public InlineArray2<int> Bot;       // Neighbors on the bottom
      public bool Hole;                   // Is this a 'hole' tile?
      public int Node;                    // Index of node pointing to this tile

      public int VTop, VBot;

      public readonly double XMin => (LMin + RMin) / 2;
      public readonly double XMax => (LMax + RMax) / 2;
   }
}
#endregion
