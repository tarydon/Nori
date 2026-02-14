using System.Text;
namespace Nori;

partial class Triangulator {
   // Nested types -------------------------------------------------------------
   // Each segment runs from top to bottom
   class Segment {
      public Segment (Point2[] pts, int a, int b) {
         Point2 pa = pts[a], pb = pts[b];
         if (pa.Y == pb.Y) throw new NotImplementedException ();
         if (pa.Y < pb.Y) (pa, pb, a, b) = (pb, pa, b, a);
         A = a; B = b;
         Slope = (pa.X - pb.X) / (pa.Y - pb.Y);
      }

      // Get the X value at a given Y
      public double GetX (Point2[] pts, double y) {
         Point2 b = pts[B];
         return b.X + Slope * (y - b.Y);
      }

      // Is the given point to the left of this segment?
      public bool Left (Point2[] pts, Point2 p) {
         Point2 a = pts[A], b = pts[B];
         return (a.X - b.X) * (p.Y - b.Y) - (a.Y - b.Y) * (p.X - b.X) > 0;
      }

      public int A;                    // Upper point
      public int B;                    // Lower point
      public readonly double Slope;

      public override string ToString () => $"Segment {A} to {B}";
   }

   class Trapezoid {
      public Trapezoid (int id, double yMin, double yMax, int left, int right, Node node)
         => (Id, YMin, YMax, Left, Right, Node) = (id, yMin, yMax, left, right, node);

      public override string ToString () => $"Trap#{Id} Y:{YMin.R6 ()} to {YMax.R6 ()} Left:{Left} Right:{Right}";

      // Returns true if this Trapezoid has no top segment (ends in a single point)
      public bool NoTop (Triangulator t, int nPt) {
         // First, a quick check : given that nPt is a top corner of this 
         // trapezoid, we can quickly check if left and right segments both start at that point
         ref Segment sa = ref t.mSegs[Left], sb = ref t.mSegs[Right];
         if (sa.A == nPt && sb.A == nPt) return true;
         // Slower: we have to evaluate X at both segments and check if they are the same here
         double xa = sa.GetX (t.mPts, YMax), xb = sb.GetX (t.mPts, YMax);
         return xa.EQ (xb, FINE);
      }

      public bool NoBottom (Triangulator t, int nPt) {
         // First, a quick check : given that nPt is a bottom corner of this 
         // trapezoid, we can quickly check if left and right segments both end at that point
         ref Segment sa = ref t.mSegs[Left], sb = ref t.mSegs[Right];
         if (sa.B == nPt && sb.B == nPt) return true;
         // Slower: we have to evaluate X at both segments and check if they are the same here
         double xa = sa.GetX (t.mPts, YMin), xb = sb.GetX (t.mPts, YMin);
         return xa.EQ (xb, FINE);
      }

      public void UpdateBottom (Trapezoid? old, Trapezoid newT) {
         if (BotA == old) BotA = newT;
         else if (BotB == old) BotB = newT;
         else throw new NotImplementedException ();
      }

      public bool AddBottom1 (Trapezoid newT) {
         if (BotA == null) BotA = newT;
         else if (BotB == null) BotB = newT;
         else return false;
         return true;
      }

      public void AddBottom (Trapezoid newT) => AddBottom1 (newT);

      public void RemoveBottom (Trapezoid old) {
         if (BotA == old) BotA = BotB;
         else if (BotB == old) BotB = null;
         else throw new NotImplementedException ();
      }

      public void UpdateTop (Trapezoid? old, Trapezoid newT) {
         if (TopA == old) TopA = newT;
         else if (TopB == old) TopB = newT;
         else throw new NotImplementedException ();
      }

      public bool AddTop1 (Trapezoid newT) {
         if (TopA == null) TopA = newT;
         else if (TopB == null) TopB = newT;
         else return false;
         return true;
      }

      public void AddTop (Trapezoid newT) => UpdateTop (null, newT);

      public void RemoveTop (Trapezoid old) {
         if (TopA == old) TopA = TopB;
         else if (TopB == old) TopB = null;
         else throw new NotImplementedException ();
      }

      public readonly int Id;
      public double YMin, YMax;
      public int Left, Right;
      public Node Node;
      public Trapezoid? BotA, BotB, TopA, TopB;
   }

   enum EKind { Y, X, Leaf }

   class Node {
      public Node (int id, EKind kind, int index) => (Id, Kind, Index) = (id, kind, index);
      public EKind Kind;
      public Node? First, Second;
      public int Index;
      public int Id;

      public override string ToString () => $"Node#{Id} Kind:{Kind} Index:{Index}";

      public void Dump (Triangulator owner, StringBuilder sb, int level) {
         sb.Append (new string (' ', level * 3));
         sb.Append ($"#{Index} {Kind} ");
         if (Kind == EKind.Y) sb.Append (owner.mPts[Index].Y.R6 ());
         sb.AppendLine ();
         First?.Dump (owner, sb, level + 1);
         Second?.Dump (owner, sb, level + 1);
      }

      public Node Find (Triangulator owner, Point2 pt)
         => Kind switch {
            EKind.Y => pt.Y < owner.mPts[Index].Y ? First! : Second!,
            EKind.X => owner.mSegs[Index].Left (owner.mPts, pt) ? First! : Second!,
            _ => this,
         };
   }
}