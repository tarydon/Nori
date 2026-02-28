using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.CompilerServices.Unsafe;

namespace Nori;

partial class Triangulator {
   enum EVKind { Regular, Valley, Mountain };

   readonly struct Segment {
      public Segment (ref Vertex vBase, int a, int b, bool hole) {
         Point2 pa = Add (ref vBase, a).Pt, pb = Add (ref vBase, b).Pt;
         if (pa.EQ (pb, FINE)) throw new InvalidOperationException ();
         if (pa.Y < pb.Y) (A, B, PA, PB, PartOnLeft) = (b, a, pb, pa, true);
         else (A, B, PA, PB, PartOnLeft) = (a, b, pa, pb, false);
         Slope = (pa.X - pb.X) / (pa.Y - pb.Y);
      }

      public override string ToString ()
         => $"Segment {A}..{B}, Left:{PartOnLeft}, Hole:{Hole}";

      public readonly int A, B;
      public readonly Point2 PA, PB;
      public readonly bool PartOnLeft;
      public readonly double Slope;
      public readonly bool Hole;
   }

   struct Vertex {
      public Vertex (Point2 pt, EVKind kind) => (Pt, Kind) = (pt, kind);

      public override string ToString ()
         => $"Vertex {Pt} : {Kind}";

      public readonly Point2 Pt;
      public readonly EVKind Kind;
      public bool Inserted = false;
   }
}
