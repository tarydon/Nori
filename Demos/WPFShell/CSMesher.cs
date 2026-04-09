namespace Nori;

/// <summary>
/// This class is used to create a mesh from 2 cross-sections (front & side views)
/// </summary>
public class CSMesher {
   public CSMesher (IEnumerable<Poly> front, IEnumerable<Poly> side) {
      const double t = Lib.CoarseTess, a = Lib.CoarseTessAngle;
      mFront = [.. front.SelectMany (p => p.DiscretizeP (t, a).Segs).Select (s => new CSeg (s)).Order ()];
      mSide = [.. side.SelectMany (p => p.DiscretizeP (t, a).Segs).Select (s => new CSeg (s)).Order ()];
      mSplit.AddRange (mFront.Select (a => a.A.Y)); mSplit.AddRange (mFront.Select (a => a.B.Y));
      mSplit.AddRange (mSide.Select (a => a.A.Y)); mSplit.AddRange (mSide.Select (a => a.B.Y));
      mSplit = [.. mSplit.Order ().Distinct ()];
   }
   CSeg[] mFront, mSide;
   List<double> mSplit = [];

   public void Build () {
      for (int i = 0; i < mFront.Length; i++) {
         ref CSeg front = ref mFront[i];
         mOverlap.Clear ();
         for (int j = 0; j < mSide.Length; j++) {
            ref CSeg side = ref mSide[j];
            if (!front.Overlaps (ref side)) continue;
            mOverlap.Add (j);
         }
      }
   }
   List<int> mOverlap = [];

   readonly struct CSeg : IComparable<CSeg> {
      public CSeg (Seg s) {
         bool flip;
         if (s.A.Y.EQ (s.B.Y)) flip = s.A.X > s.B.X;
         else flip = s.A.Y > s.B.Y;
         (A, B) = (Flip = flip) ? (s.B.R6 (), s.A.R6 ()) : (s.A.R6 (), s.B.R6 ());
         DY = B.Y - A.Y;
      }
      public override string ToString () => $"{(Flip ? '-' : '+')} {A} .. {B}";

      public bool Overlaps (ref CSeg other) {
         if (other.A.Y > B.Y - Lib.Delta) return false;
         if (A.Y > other.B.Y - Lib.Delta) return false;
         return true; 
      }

      public double GetX (double y) {
         double lie = (y - A.Y) / DY;
         return lie.Along (A.X, B.X);
      }

      public int CompareTo (CSeg other) {
         if (!A.Y.EQ (other.A.Y)) return A.Y.CompareTo (other.A.Y);
         return B.Y.CompareTo (other.B.Y);
      }

      public readonly Point2 A;  // Lower point A
      public readonly Point2 B;  // Upper point B
      public readonly bool Flip; // Direction opposing original seg
      public readonly double DY;
   }
}
