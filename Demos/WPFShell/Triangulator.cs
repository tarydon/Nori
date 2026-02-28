using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.MemoryMarshal;
using static System.Runtime.CompilerServices.Unsafe;
using System.Text;

namespace Nori;

partial class Triangulator {
   public void Reset () {
      mBound = new ();
      mVN = mSN = 0;
      if (Lib.Testing) mR = new (42);
   }
   Bound2 mBound;

   public void AddContour (ReadOnlySpan<Point2> pts, bool hole) {
      int n = pts.Length;

      // Add the points into the mV array
      Grow (ref mV, mVN, n);
      Point2 prev = pts[n - 1], pt = pts[0];
      for (int i = 0; i < n; i++) {
         Point2 next = pts[(i + 1) % n];
         double dy0 = prev.Y - pt.Y, dy1 = next.Y - pt.Y;

         EVKind kind = EVKind.Regular;
         if (dy0 > 0 && dy1 > 0) kind = EVKind.Valley;
         else if (dy0 < 0 && dy1 < 0) kind = EVKind.Mountain;
         mV[mVN++] = new (pt, kind);
         mBound += pt; 
         prev = pt; pt = next;
      }

      // Now, add the segments into the mS array
      Grow (ref mS, mSN, n);
      ref Vertex vBase = ref GetReference (mV);
      for (int i = 0; i < n; i++) {
         int j = (i + 1) % n;
         mS[mSN++] = new (ref vBase, i, j, hole);
      }
   }

   public IEnumerable<string> Process () {

      yield return "Starting processing";
      yield return "Finished processing";
   }

   // Implementation -----------------------------------------------------------
   // Computes (in mShuffle) a random permutation of the segments. This is
   // critical to achieve good performance from the Seidel algorithm
   void ShuffleSegs () {
      Grow (ref mShuffle, 0, mSN);
      ref int sh0 = ref GetReference (mShuffle);
      for (int i = 0; i < mSN; i++) Add (ref sh0, i) = i;
      for (int i = 0; i < mSN; i++) {
         int j = mR.Next (mSN);
         (Add (ref sh0, j), Add (ref sh0, i)) = (Add (ref sh0, i), Add (ref sh0, j));
      }
   }

   // Helpers ------------------------------------------------------------------
   static void Check (bool condition) {
      if (!condition) throw new InvalidOperationException ("Triangulator");
   }

   void Grow<T> (ref T[] array, int used, int delta) {
      int size = array.Length, total = used + delta;
      while (size <= total) size *= 2;
      if (size > array.Length) {
         var final = new T[size];
         if (used > 0) Array.Copy (array, final, used);
         array = final;
      }
   }

   // Private data -------------------------------------------------------------
   Vertex[] mV = new Vertex[32];    // List of all vertices
   Segment[] mS = new Segment[32];  // List of all segments
   int mVN, mSN;                    // Usage counts (Vertices, Segments)
   Random mR = new (42);            // Used for random insertion of segments
   int[] mShuffle = new int[32];    // A permutation of the segments

   const double FINE = 1e-9;
}
