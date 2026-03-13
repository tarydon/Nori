// ────── ╔╗
// ╔-╦╦-╦╦╬╣ FastTess2DAux.cs
// ║║║║╬║╔╣║ Nested types for the Tessellator class (Nori.Ref variant)
// ╚╩-╩-╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Nori.Ref;

#region class FastTess2D : nested types ------------------------------------------------------------
public partial class FastTess2D {
   // Enumerations ---------------------------------------------------------------------------------
   // EVKind lists the types of vertices
   enum EVertex { Regular, Valley, Mountain };
   // EKind lists the types of nodes
   enum ENode { Y, X, Leaf, Redirect };
   // When a vertex is connected to a tile, which 'chain does it belong to
   enum EChain { HSlice, Left, Right, Valley, Mountain };

   // struct Node ----------------------------------------------------------------------------------
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
   struct Node (int id, ENode kind, int index) {
      // Properties ------------------------------------------------------------
      public readonly int Id = id;  // Index of this node within the mN array
      public ENode Kind = kind;     // What kind of node is this? (X split / Y split / leaf)
      public int First;             // First child (lower / left)
      public int Second;            // Second child (upper / right)
      public int Index = index;     // Pointer to a Vertex / Segment / Tile depending on the node type
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
         Point2 pa = Unsafe.Add (ref vBase, a).Pt, pb = Unsafe.Add (ref vBase, b).Pt;
         if (pa.EQ (pb, FINE)) throw new InvalidOperationException ();
         if (pa.Y < pb.Y) (A, B, pa, pb, PartOnLeft) = (b, a, pb, pa, true);
         else (A, B, PartOnLeft) = (a, b, false);
         DX = pa.X - pb.X; DY = pa.Y - pb.Y; PB = pb;
         Slope = DX / DY; XPrime = pb.X - Slope * pb.Y;
      }

      // Properties ------------------------------------------------------------
      public readonly int Id;             // Index of the segment in the mS array
      public readonly int A, B;           // Indices of the top and bottom vertex
      public readonly bool PartOnLeft;    // Does the part lie on the left of the segment (as viewed by user)
      public readonly bool Diagonal;      // Interior diagonal with a tile (material exists on both sides)

      // Methods ---------------------------------------------------------------
      // Get the X value at a given Y
      public double GetX (double y) => XPrime + Slope * y;

      // Is the given point to the 'left' of this segment?
      public bool IsLeft (Point2 p) => DX * (p.Y - PB.Y) - DY * (p.X - PB.X) > 0;

      public override string ToString () => $"Segment {A}..{B}, Left:{PartOnLeft}";

      // Private data ----------------------------------------------------------
      readonly double Slope, XPrime;      // Slope of the segment, 'intercept' used to simplify GetX
      readonly double DX, DY;             // PA.X - PB.X, PA.Y - PB.Y
      readonly Point2 PB;
   }

   // struct Tile ----------------------------------------------------------------------------------
   // Represents a trapezoid in the Seidel decomposition of the plane.
   // Each Tile is a trapezoid with top and bottom edges parallel to the X axis (designated
   // by YMin and YMax), and left and right edges that are non-horizontal segments (represented
   // by Left and Right pointers into the mS array). In addition, we maintain some other computed values
   // that are useful to Connect tiles to neighbors where needed.
   struct Tile {
      // Constructors ----------------------------------------------------------
      // Basic constructor used to make a tile
      public Tile (int id, ref Segment sBase, double yMin, double yMax, int left, int right, int node) {
         (Id, YMin, YMax, Left, Right, Node) = (id, yMin, yMax, left, right, node);
         ref Segment L = ref Unsafe.Add (ref sBase, left);
         ref Segment R = ref Unsafe.Add (ref sBase, right);
         Hole = L.PartOnLeft; Check (L.PartOnLeft != R.PartOnLeft);
      }

      // Cloning constructor used when splitting a Tile
      public Tile (int id, ref Tile t, int node)
         => (Id, YMin, YMax, Left, Right, Node, Hole) = (id, t.YMin, t.YMax, t.Left, t.Right, node, t.Hole);

      public readonly override string ToString () {
         string text = $"{Id}"; if (Hole) text += "*";
         text += $"|{VTop}"; if (VTop != 0) text += ETop.ToString ()[0];
         text += $"|{VBot}"; if (VBot != 0) text += EBot.ToString ()[0];
         return text;
      }

      // Properties ------------------------------------------------------------
      public int Id;                      // Index of this within the mT array
      public double YMin, YMax;           // Range of Y values spanned by this tile
      public int Left, Right;             // Left and Right segment indices
      public bool Hole;                   // Is this a 'hole' tile?
      public int Node;                    // Index of node pointing to this tile
      public int VTop, VBot;              // If non-zero, the node touching the top/bottom line
      public EChain ETop, EBot;           // Where that does node touch the top/bottom line?

      // Methods ---------------------------------------------------------------
      // Gets the one or two bottom tiles that connect to this
      public readonly (int B1, int B2) GetBottom (ref Vertex vBase) {
         int b1 = 0, b2 = 0;
         if (VBot > 0) {
            ref Vertex v = ref Unsafe.Add (ref vBase, VBot);
            switch (EBot) {
               case EChain.HSlice: (b1, b2) = (v.BL, v.BR); break;
               case EChain.Left: b1 = v.BR; break;
               case EChain.Right: b1 = v.BL; break;
               case EChain.Valley: break;
               default: throw new NotImplementedException ();
            }
            if (b2 == b1) b2 = 0;
         }
         return (b1, b2);
      }

      // Gets the one or two top tiles that connect to this
      public readonly (int T1, int T2) GetTop (ref Vertex vBase) {
         int t1 = 0, t2 = 0;
         if (VTop > 0) {
            ref Vertex v = ref Unsafe.Add (ref vBase, VTop);
            switch (ETop) {
               case EChain.HSlice: (t1, t2) = (v.TL, v.TR); break;
               // Logic below is correct: if the vertex is on our LEFT chain, our top neighbor
               // is the top-right tile touching the vertex
               case EChain.Left: t1 = v.TR; break;
               case EChain.Right: t1 = v.TL; break;
               case EChain.Mountain: break;
               default: throw new NotImplementedException ();
            }
            if (t2 == t1) t2 = 0;
         }
         return (t1, t2);
      }

      // Core routine used by both SplitY and SplitX
      // This assumes that the mN and mT arrays have already been grown by the required numbers.
      // AllocTile may grow mT but not mN, so nBase/leaf remain valid across AllocTile.
      ref Tile SplitBase (FastTess2D t, ENode kind, int index) {
         // Create 2 new leaf nodes pointing to the two split tiles (one of them is just this tile
         // and the other will be created at mT[t.mTN]
         ref Node nBase = ref MemoryMarshal.GetArrayDataReference (t.mN);
         ref Node leaf = ref Unsafe.Add (ref nBase, Node); Check (leaf.Kind == ENode.Leaf);
         leaf.Kind = kind; leaf.Index = index;
         int nNew = t.AllocTile ();
         // Get tBase after AllocTile since it may have grown mT (mN is not grown by AllocTile)
         ref Tile tBase = ref MemoryMarshal.GetArrayDataReference (t.mT);
         Unsafe.Add (ref nBase, t.mNN) = new (t.mNN, ENode.Leaf, Id);                // Below
         Unsafe.Add (ref nBase, t.mNN + 1) = new (t.mNN + 1, ENode.Leaf, nNew);      // Above
         Node = leaf.First = t.mNN; leaf.Second = t.mNN + 1;
         t.mNN += 2;

         // Create the new tile by cloning this one
         Unsafe.Add (ref tBase, nNew) = new Tile (nNew, ref this, leaf.Second);
         return ref Unsafe.Add (ref tBase, nNew);
      }

      // Splits the tile into two by a given vertex.
      // This is horizontal split of the tile - this tile continues on as the 'bottom' tile,
      // while the newly created tile becomes the top tile
      public void SplitY (FastTess2D t, ref Vertex v) {
         ref Tile t1 = ref SplitBase (t, ENode.Y, v.Id);
         double y = v.Pt.Y; Check (YMin < y && y < YMax);
         t1.YMin = YMax = y;

         // Link the vertex to the 4 neighboring tiles (BL and BR are same, TL and TR are same)
         // At this point, this newly added vertex appears as a HSlice vertex in both the tiles
         v.BL = v.BR = Id; v.TL = v.TR = t1.Id;

         // At this point, t0's bottom links are fine. However t0's top links should be
         // relinked to t1 (both Tile->Vertex, and Vertex->Tile links)
         if (VTop != 0) {
            ref Vertex vBase = ref MemoryMarshal.GetArrayDataReference (t.mV);
            ref Vertex vt = ref Unsafe.Add (ref vBase, VTop);
            t1.VTop = VTop; t1.ETop = ETop;
            switch (ETop) {
               case EChain.HSlice: Check (vt.BL == Id && vt.BR == Id); vt.BL = vt.BR = t1.Id; break;
               case EChain.Left: Check (vt.BR == Id); vt.BR = t1.Id; break;
               case EChain.Right: Check (vt.BL == Id); vt.BL = t1.Id; break;
               case EChain.Mountain: break;
               default: Unexpected (); break;
            }
         }
         VTop = t1.VBot = v.Id; ETop = t1.EBot = EChain.HSlice;
      }

      // Splits the tile into two left/right by a given segment
      public ref Tile SplitX (FastTess2D t, int segment) {
         ref Tile t1 = ref SplitBase (t, ENode.X, segment);
         ref Segment sBase = ref MemoryMarshal.GetArrayDataReference (t.mS);
         #if VERIFY
            double yM = (YMin + YMax) / 2;
            ref Segment S = ref Unsafe.Add (ref sBase, segment);
            ref Segment L = ref Unsafe.Add (ref sBase, Left);
            ref Segment R = ref Unsafe.Add (ref sBase, Right);
            double xL = L.GetX (yM), x = S.GetX (yM), xR = R.GetX (yM);
            Check (xL < x && x < xR);
         #endif
         Right = t1.Left = segment;
         Check (VTop != 0 && VBot != 0);
         ref Segment seg = ref Unsafe.Add (ref sBase, segment); bool diagonal = seg.Diagonal;
         if (!diagonal) { Hole = !seg.PartOnLeft; t1.Hole = seg.PartOnLeft; }
         ref Vertex vBase = ref MemoryMarshal.GetArrayDataReference (t.mV);
         ref Vertex vt = ref Unsafe.Add (ref vBase, VTop);
         ref Vertex vb = ref Unsafe.Add (ref vBase, VBot);
         switch (ETop) {
            case EChain.HSlice:
               if (seg.A == VTop) {             // Case (a)
                  ETop = EChain.Right; t1.VTop = VTop; t1.ETop = EChain.Left;
                  Check (vt.BR == Id); vt.BR = t1.Id;
               } else {
                  if (!seg.IsLeft (vt.Pt)) {    // Case (c)
                     t1.VTop = VTop; t1.ETop = EChain.HSlice; VTop = 0;
                     vt.ReplaceBottom (Id, t1.Id);
                  }                             // Case (b) - no-op
               }
               break;
            case EChain.Left:
               if (seg.A == VTop) {             // Case (d)
                  ETop = EChain.Mountain; t1.VTop = VTop; t1.ETop = EChain.Left;
                  Check (vt.BR == Id && (diagonal || vt.Kind == EVertex.Mountain)); vt.BR = t1.Id;
               }                                // Case (e) - no-op
               break;
            case EChain.Right:
               if (seg.A == VTop) {             // Case (f)
                  t1.VTop = VTop; t1.ETop = EChain.Mountain;
                  Check (vt.BL == Id && (diagonal || vt.Kind == EVertex.Mountain));
               } else {                         // Case (g)
                  t1.VTop = VTop; t1.ETop = EChain.Right; VTop = 0;
                  vt.ReplaceBottom (Id, t1.Id);
               }
               break;
            case EChain.Mountain:
               Check (diagonal); t1.VTop = VTop; t1.ETop = ETop;
               break;
            default: Unexpected (); break;
         }
         switch (EBot) {
            case EChain.HSlice:
               if (seg.B == VBot) {             // Case (k)
                  EBot = EChain.Right; t1.VBot = VBot; t1.EBot = EChain.Left;
                  Check (vb.TR == Id); vb.TR = t1.Id;
               } else {
                  if (!seg.IsLeft (vb.Pt)) {    // Case (m)
                     t1.VBot = VBot; t1.EBot = EChain.HSlice; VBot = 0;
                     vb.ReplaceTop (Id, t1.Id);
                  }                             // Case (l) - no-op
               }
               break;
            case EChain.Left:                   // Case (n)
               if (seg.B == VBot) {
                  EBot = EChain.Valley; t1.VBot = VBot; t1.EBot = EChain.Left;
                  Check (vb.TR == Id && (diagonal || vb.Kind == EVertex.Valley)); vb.TR = t1.Id;
               }                                // Case (o) - no-op
               break;
            case EChain.Right:
               if (seg.B == VBot) {             // Case (p)
                  t1.VBot = VBot; t1.EBot = EChain.Valley;
                  Check (vb.TL == Id && (diagonal || vb.Kind == EVertex.Valley));
               } else {                         // Case (q)
                  t1.VBot = VBot; t1.EBot = EChain.Right; VBot = 0;
                  vb.ReplaceTop (Id, t1.Id);
               }
               break;
            case EChain.Valley:
               Check (diagonal); t1.VBot = VBot; t1.EBot = EBot;
               break;
            default: Unexpected (); break;
         }
         return ref t1;
      }
   }

   // struct Vertex --------------------------------------------------------------------------------
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
      public bool Inserted;
      public int TL, TR, BL, BR;       // Neighbor tiles in the 4 directions

      // Methods ---------------------------------------------------------------
      public void ReplaceTop (int nOld, int nNew) {
         Check (TL == nOld || TR == nOld);
         if (TL == nOld) TL = nNew; if (TR == nOld) TR = nNew;
      }

      public void ReplaceBottom (int nOld, int nNew) {
         Check (BL == nOld || BR == nOld);
         if (BL == nOld) BL = nNew; if (BR == nOld) BR = nNew;
      }

      public readonly override string ToString () {
         string text = $"{Kind.ToString ()[0]}{Id}|{TL}"; if (TR != TL) text += $",{TR}";
         text += $"|{BL}"; if (BR != BL) text += $",{BR}";
         if (text.EndsWith ("|0|0")) text = text[..^4];
         return text;
      }
   }
}
#endregion
