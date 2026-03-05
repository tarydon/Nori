// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangulator2.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.CompilerServices;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;
namespace Nori;

#region class Triangulator : nested types ----------------------------------------------------------
public partial class Triangulator {
   // Enumerations ═════════════════════════════════════════════════════════════════════════════════
   // EVKind lists the types of vertices
   enum EVertex { Regular, Valley, Mountain };
   // EKind lists the types of nodes
   enum ENode { Y, X, Leaf };
   // When a vertex is connected to a tile, which 'chain does it belong to
   enum EChain { HSlice, Left, Right, Valley, Mountain };

   // struct Layer ═════════════════════════════════════════════════════════════════════════════════
   // Represents a Layer in the stitching process
   class Layer {
      // Constructor -----------------------------------------------------------
      public void Init (ref Tile left, ref Tile right) {
         mTiles.Clear (); mTiles.Add (left.Id); mTiles.Add (right.Id);
         mAbove.Clear (); AddN (mAbove, left.Top[0]); AddN (mAbove, left.Top[1]);
         mBelow.Clear (); AddN (mBelow, left.Bot[0]); AddN (mBelow, left.Bot[1]);
      }

      // Methods ---------------------------------------------------------------
      public void AddRights (Layer L1) {
         L1.mAbove.Add (mTiles[1]); mBelow.Add (L1.mTiles[1]);
      }

      public void Connect (ref Tile tBase) {
         ref Tile left = ref Add (ref tBase, mTiles[0]), right = ref Add (ref tBase, mTiles[1]);
         foreach (var n in mAbove) {
            ref Tile above = ref Add (ref tBase, n);
            above.DisconnectFrom (ref left);
            above.ConnectTo (ref left); above.ConnectTo (ref right);
         }
         foreach (var n in mBelow) {
            ref Tile below = ref Add (ref tBase, n);
            left.DisconnectFrom (ref below);
            left.ConnectTo (ref below); right.ConnectTo (ref below);
         }
      }

      // Implementation --------------------------------------------------------
      static void AddN (List<int> items, int n) { if (n > 0) items.Add (n); }
      List<int> mTiles = [], mAbove = [], mBelow = [];
   }

   // struct Node ══════════════════════════════════════════════════════════════════════════════════
   // This represents a Node in the DAG that we build to locate the tiles containing particular
   // points. Nodes are of these types:
   // - Leaf : a bottom level node in the DAG, and Index points to a Tile
   // - Y : a node representing a horizontal split line, Index points to a Vertex whose
   //       Y coordinate guides the search into either the First or Second child node
   // - X : a node representing a non-horizontal split of a tile by a Segment. Index points to
   //       a Segment which guides the search into either the First or Second child node, depending
   //       on whether the search point lies to the left or the right of the segment
   // Initially we start with a single node that is a Leaf coverting the entire working space (a
   // dummy tile). Splits happen when we slice a tile horizontally or vertically - that erstwhile
   // Leaf node pointing to that tile then becomes an X or Y node and gets two children representing
   // the two smaller tiles that now result from the split. 
   struct Node {
      // Constructors ----------------------------------------------------------
      public Node (int id, ENode kind, int index) => (Id, Kind, Index) = (id, kind, index);

      // Properties ------------------------------------------------------------
      public readonly int Id;    // Index of this node within the mN array
      public ENode Kind;         // What kind of node is this? (X split / Y split / leaf)
      public int First;          // First child (lower / left)
      public int Second;         // Second child (upper / right)
      public int Index;          // Pointer to a Vertex / Segment / Tile depending on the node type
   }

   // struct Segment -------------------------------------------------------------------------------
   // This represents a segment connecting two vertices a and b. We reorder the segment so that it
   // always runs from top to bottom (PA.Y > PB.Y internally). In addition, the segment has a 
   // PartOnLeft flag that indicates if the material lies to the left of the segment (as viewed from
   // the birds eye view, not from the segment's own perspective). 
   // We also compute Slope, XPrime etc to make it faster to evaluate the X value at a given Y
   readonly struct Segment {
      // Constructors ----------------------------------------------------------
      public Segment (int id, ref Vertex vBase, int a, int b, bool diagonal = false) {
         Id = id; Diagonal = diagonal;
         Point2 pa = Add (ref vBase, a).Pt, pb = Add (ref vBase, b).Pt;
         if (pa.EQ (pb, FINE)) throw new InvalidOperationException ();
         if (pa.Y < pb.Y) (A, B, PA, PB, PartOnLeft) = (b, a, pb, pa, true);
         else (A, B, PA, PB, PartOnLeft) = (a, b, pa, pb, false);
         Slope = (pa.X - pb.X) / (pa.Y - pb.Y);
         XPrime = PB.X - Slope * PB.Y;
      }

      // Properties ------------------------------------------------------------
      public readonly int Id;             // Index of the segment in the mS array
      public readonly int A, B;           // Indicies of the top and bottom vertex
      public readonly Point2 PA, PB;      // Actual positions of the top and bottom vertex
      public readonly double Slope;       // Slope of the segment
      public readonly double XPrime;      // 'Intercept' used to simplify GetX computation
      public readonly bool PartOnLeft;    // Does the part lie on the left of the segment (as viewed by user)
      public readonly bool Diagonal;      // Interior diagonal with a tile (material exists on both sides)

      // Methods ---------------------------------------------------------------
      // Get the X value at a given Y 
      public double GetX (double y) => XPrime + Slope * y;

      // Get the X values at a couple of Ys
      public (double, double) GetX (double y1, double y2) => (XPrime + Slope * y1, XPrime + Slope * y2);

      // Is the given point to the 'left' of this segment?
      public bool IsLeft (Point2 p) => (PA.X - PB.X) * (p.Y - PB.Y) - (PA.Y - PB.Y) * (p.X - PB.X) > 0;

      public override string ToString ()
         => $"Segment {A}..{B}, Left:{PartOnLeft}";
   }

   // struct Tile ══════════════════════════════════════════════════════════════════════════════════
   // Represents a trapezoid in the Seidel decomposition of the plane. 
   // Each Tile is a trapezoid with top and bottom edges parallel to the X axis (designated
   // by YMin and YMax), and left and right edges that are non-horizontal segments (represented
   // by Left and Right pointers into the mS array). In addition, we maintain some other computed values
   // that are useful to Connect tiles to neighbors where needed. 
   struct Tile {
      // Constructors ----------------------------------------------------------
      // Basic constructor used to make a tile
      public Tile (int id, ref Segment s0, double yMin, double yMax, int left, int right, int node) {
         (Id, YMin, YMax, Left, Right, Node) = (id, yMin, yMax, left, right, node);
         ref Segment L = ref Add (ref s0, left), R = ref Add (ref s0, right);
         Hole = L.PartOnLeft; Check (L.PartOnLeft != R.PartOnLeft);
         (LMin, LMax) = L.GetX (yMin, yMax);
         (RMin, RMax) = R.GetX (yMin, yMax);
      }

      // Cloning constructor used when splitting a Tile
      public Tile (int id, ref Tile t, int node) {
         (Id, YMin, YMax, Left, Right, Node, Hole) = (id, t.YMin, t.YMax, t.Left, t.Right, node, t.Hole);
         (LMin, LMax, RMin, RMax) = (t.LMin, t.LMax, t.RMin, t.RMax);
      }

      public readonly override string ToString () {
         string text = $"{Id}"; if (Hole) text += "*";
         if (VTop > 0) text += $" T{VTop}{ETop.ToString ()[0]}";
         if (VBot > 0) text += $" B{VBot}{EBot.ToString ()[0]}";
         return text;
      }

      // Properties ------------------------------------------------------------
      public int Id;                      // Index of this within the mT array
      public double YMin, YMax;           // Range of Y values spanned by this tile
      public double LMin, LMax;           // Min & Max X on the left side
      public double RMin, RMax;           // Min & Max X on the right side
      public int Left, Right;             // Left and Right segment indices
      public InlineArray2<int> Top;       // Neighbors on the top
      public InlineArray2<int> Bot;       // Neighbors on the bottom
      public bool Hole;                   // Is this a 'hole' tile?
      public int Node;                    // Index of node pointing to this tile

      public int VTop, VBot;              // If non-zero, the node touching the top/bottom line
      public EChain ETop, EBot;           // Where that does node touch the top/bottom line?

      public readonly double XMin => (LMin + RMin) / 2;  // X value in the middle of the YMin line
      public readonly double XMax => (LMax + RMax) / 2;  // X value in the middle of the YMax line

      // Methods ---------------------------------------------------------------
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
         if (Bot[0] == t1.Id) { Bot[0] = Bot[1]; Bot[1] = 0; } else if (Bot[1] == t1.Id) Bot[1] = 0;
         if (t1.Top[0] == Id) { t1.Top[0] = t1.Top[1]; t1.Top[1] = 0; } else if (t1.Top[1] == Id) t1.Top[1] = 0;
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
         Add (ref tBase, t.mTN) = new Tile (t.mTN, ref this, leaf.Second);
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
               t1.ETop = ETop;
               ref Vertex vTop = ref Add (ref vBase, VTop);
               if (vTop.Kind != EVertex.Valley) {
                  if (vTop.Tile[0] == Id) vTop.Tile[0] = t1.Id;
                  else if (vTop.Tile[1] == Id) vTop.Tile[1] = t1.Id;
               }
            }
            t1.VBot = VTop = index;
            t1.EBot = ETop = EChain.HSlice;
         } else {
            // Splitting at a segment. The new tile t1 is going to be on the right of the segment
            ref Segment seg = ref Add (ref sBase, index);
            bool diagonal = seg.Diagonal;
            #if VERIFY
               double yM = (YMin + YMax) / 2;
               ref Segment left = ref Add (ref sBase, Left), right = ref Add (ref sBase, Right);
               double xL = left.GetX (yM), x = seg.GetX (yM), xR = right.GetX (yM);
               Check (xL < x && x < xR);
            #endif
            t1.Left = Right = index;
            (RMin, RMax) = (t1.LMin, t1.LMax) = seg.GetX (YMin, YMax);
            if (!diagonal) { Hole = !seg.PartOnLeft; t1.Hole = seg.PartOnLeft; }
            if (VTop != 0) {
               ref Vertex vtop = ref Add (ref vBase, VTop);
               switch (ETop) {
                  case EChain.HSlice:
                     if (seg.A == VTop) {    // Case (a)
                        ETop = EChain.Right; t1.VTop = VTop; t1.ETop = EChain.Left;
                        if (!diagonal && vtop.Kind == EVertex.Mountain) { Check (vtop.Tile[0] == Id); vtop.Tile[1] = t1.Id; }
                     } else if (!seg.IsLeft (vtop.Pt)) { 
                        t1.VTop = VTop; t1.ETop = ETop; VTop = 0;
                        if (!diagonal) vtop.ReplaceTile (Id, t1.Id);
                     }
                     break;
                  case EChain.Left:
                     if (seg.A == VTop) {    // Case (b)
                        ETop = EChain.Mountain; t1.VTop = VTop; t1.ETop = EChain.Left;
                        if (!diagonal) Check (vtop.Kind == EVertex.Mountain);
                     }                       // Case (c) - nothing to do
                     break;
                  case EChain.Right:
                     if (seg.A == VTop) {    // Case (d)
                        t1.VTop = VTop; t1.ETop = EChain.Mountain;
                        if (!diagonal) Check (vtop.Kind == EVertex.Mountain); 
                     } else {                // Case (e)
                        t1.VTop = VTop; VTop = 0; t1.ETop = EChain.Right;
                        if (!diagonal) vtop.ReplaceTile (Id, t1.Id);
                     }
                     break;
                  case EChain.Mountain:
                     Check (diagonal); t1.VTop = VTop; t1.ETop = ETop;
                     break;
                  default: throw new NotImplementedException ();                     
               }
            }
            if (VBot != 0) {
               ref Vertex vbot = ref Add (ref vBase, VBot);
               switch (EBot) {
                  case EChain.HSlice:
                     if (seg.B == VBot) {    // Case (f)
                        t1.VBot = VBot; EBot = EChain.Right; t1.EBot = EChain.Left;
                        if (!diagonal && vbot.Kind == EVertex.Valley) { Check (vbot.Tile[0] == Id); vbot.Tile[1] = t1.Id; } 
                     } else if (!seg.IsLeft (vbot.Pt)) { 
                        t1.VBot = VBot; t1.EBot = EBot; VBot = 0;
                        if (!diagonal) vbot.ReplaceTile (Id, t1.Id);
                     }
                     break;
                  case EChain.Left:
                     if (seg.B == VBot) {    // Case (g)
                        EBot = EChain.Valley; t1.VBot = VBot; t1.EBot = EChain.Left;
                        if (!diagonal) Check (vbot.Kind == EVertex.Valley); 
                     }                       // Case (h) - nothing to do
                     break;
                  case EChain.Right:
                     if (seg.B == VBot) {    // Case (i)
                        t1.VBot = VBot; t1.EBot = EChain.Valley;
                        if (!diagonal) Check (vbot.Kind == EVertex.Valley); 
                     } else {                // Case (j)
                        t1.VBot = VBot; VBot = 0; t1.EBot = EChain.Right;
                        if (!diagonal) vbot.ReplaceTile (Id, t1.Id);
                     }
                     break;
                  case EChain.Valley:
                     Check (diagonal); t1.VBot = VBot; t1.EBot = EBot;
                     break;
                  default: throw new NotImplementedException ();                     
               }
            }
         }
         t.mNN += 2; t.mTN++;
         return ref t1;
      }

      // Updates one of the 'bottom' connections of the tile
      public void UpdateBottom (int nOld, ref Tile tNew) {
         for (int i = 0; i < 2; i++) {
            if (Bot[i] != nOld) continue;
            Bot[i] = tNew.Id;
            if (Bot[0] != 0 && Bot[1] != 0) {
               ref Tile tLeft = ref Add (ref tNew, Bot[0] - tNew.Id);
               ref Tile tRight = ref Add (ref tNew, Bot[1] - tNew.Id);
               if (tLeft.XMax > tRight.XMax) (Bot[0], Bot[1]) = (Bot[1], Bot[0]);
            }
            return; 
         }
         Unexpected ();
      }

      public void UpdateTop (int nOld, ref Tile tNew) {
         for (int i = 0; i < 2; i++) {
            if (Top[i] != nOld) continue;
            Top[i] = tNew.Id;
            if (Top[0] != 0 && Top[1] != 0) {
               ref Tile tLeft = ref Add (ref tNew, Top[0] - tNew.Id);
               ref Tile tRight = ref Add (ref tNew, Top[1] - tNew.Id);
               if (tLeft.XMin > tRight.XMin) (Top[0], Top[1]) = (Top[1], Top[0]);
            } 
            return;
         }
         Unexpected ();
      }
   }

   // struct Vertex ════════════════════════════════════════════════════════════════════════════════
   // This represents a Vertex in the tessellation (a node picked from one of the Poly inputs)
   // Each vertex is classified as Regular, Mountain or Valley. A regular vertex has one neighbor
   // above and one neighbor below it (in Y). A mountain vertex has both neighbors below it (lower Y).
   // Because of our constraints, there is only one vertex ever at this given value of Y. 
   struct Vertex {
      // Constructors ----------------------------------------------------------
      public Vertex (int id, Point2 pt, EVertex kind = EVertex.Regular) 
         => (Id, Pt, Kind) = (id, pt, kind);

      // Properties ------------------------------------------------------------
      public readonly int Id;          // Index of the vertex in the mV array
      public readonly Point2 Pt;       // Point location of the vertex
      public readonly EVertex Kind;    // What kind of vertex is this?
      public InlineArray2<int> Tile;   // Two tiles touching this vertex
      public bool Inserted;

      // Methods ---------------------------------------------------------------
      public void ReplaceTile (int nOld, int nNew) {
         if (Tile[0] == nOld) Tile[0] = nNew;
         else if (Tile[1] == nOld) Tile[1] = nNew;
      }

      public readonly override string ToString () => $"Vertex#{Id} {Pt} : {Kind}";
   }
}
#endregion
