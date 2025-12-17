// ────── ╔╗
// ╔═╦╦═╦╦╬╣ MeshOps.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Math;

#region class MeshSlicer ---------------------------------------------------------------------------
public class MeshSlicer (Mesh3 mesh) {
   public List<Polyline3> Slice (PlaneDef pd) {
      mDist.Clear (); mEnds.Clear ();
      mRaw.Clear (); mNext.Clear (); mPrev.Clear ();

      // First, compute the signed distances of all the points from the Plane. 
      // If any of the distances are zero, that will cause ambiguity so we add a 
      // small bias to such points (effectively shifting them 'upwards' from the plane)
      foreach (var pt in mPts) {
         double d = pd.SignedDist (pt);
         if (Abs (d) < 1e-9) d += 1e-7;
         mDist.Add (d);
      }
      for (int i = 0; i < mTri.Length; i += 3) {
         int a = mTri[i], b = mTri[i + 1], c = mTri[i + 2];
         double da = mDist[a], db = mDist[b], dc = mDist[c];
         if (da * db < 0) Add (a, b, da, db);
         if (db * dc < 0) Add (b, c, db, dc);
         if (dc * da < 0) Add (c, a, dc, da);
         if (!mOpen) throw new InvalidOperationException ();
      }

      List<Point3> result = [];
      List<Polyline3> slices = [];
      foreach (var end in mEnds.Values) {
         int n = mNext[end]; if (n == -1) continue;   // This is not a 'head'
         while (n != -1) { result.Add ((Point3)mRaw[n]); n = mNext[n]; }
         slices.Add (new Polyline3 (0, [.. result]));
         result.Clear (); 
      }
      return slices;
   }
   List<double> mDist = [];

   // Adds an interpolated point between vertices a and b, where
   // da and db are the distances of these vertices from the plane
   void Add (int a, int b, double da, double db) {
      da = Abs (da); db = Abs (db);
      Point3f dst = (da / (da + db)).Along (mPts[a], mPts[b]);
      if (mOpen) {         // First point of a line segment discovered
         mOpen = false; mSrc = dst;
      } else {             // Second point discovered, we can add the thread mSrc .. dst
         int n = mRaw.Count; mOpen = true;
         if (Point3fComparer.Delta.Equals (mSrc, dst)) return;
         int n1 = mEnds.GetValueOrDefault (mSrc, -1), n2 = mEnds.GetValueOrDefault (dst, -1);
         if (n1 != -1) {
            // The point mSrc is not new, it has been seen before
            if (n2 == -1) {
               // The point dst is new, so we can add it into the mRaw array
               mRaw.Add (dst); mPrev.Add (-1); mNext.Add (-1);
               if (mNext[n1] == -1) { mNext[n1] = n; mPrev[n] = n1; }      // Add new tail 
               else { mPrev[n1] = n; mNext[n] = n1; } // Add new head
               mEnds.Remove (mSrc); mEnds.Add (dst, n);
            } else {
               // The point mSrc and dst are both existing endpoints, so we can connect two threads
               // together
               if (mNext[n1] == -1) {
                  if (mPrev[n2] != -1) ReverseTail (n2);
                  mNext[n1] = n2; mPrev[n2] = n1;
               } else {
                  if (mNext[n2] != -1) ReverseHead (n2);
                  mNext[n2] = n1; mPrev[n1] = n2;
               } 
               mEnds.Remove (mSrc); mEnds.Remove (dst);
            }
         } else if (n2 != -1) {
            // Here, dst is already in mRaw, while mSrc is new, so needs to be added
            mRaw.Add (mSrc); mPrev.Add (-1); mNext.Add (-1);
            if (mNext[n2] == -1) { mNext[n2] = n; mPrev[n] = n2; } 
            else { mPrev[n2] = n; mNext[n] = n2; }
            mEnds.Remove (dst); mEnds.Add (mSrc, n);
         } else {    // n1 == -1, n2 == -1
            // If we get here, this thread src .. dst is completely new and unconnected
            // to any existing. So add this into the dictionary
            mRaw.Add (mSrc); mNext.Add (n + 1); mPrev.Add (-1);
            mRaw.Add (dst); mNext.Add (-1); mPrev.Add (n);
            mEnds[mSrc] = n; mEnds[dst] = n + 1;
         }
      }
   }
   Point3f mSrc;
   bool mOpen = true;

   void ReverseHead (int n) {
      while (n != -1) {
         (mPrev[n], mNext[n]) = (mNext[n], mPrev[n]);
         n = mPrev[n];
      }
   }

   void ReverseTail (int n) {
      while (n != -1) {
         (mNext[n], mPrev[n]) = (mPrev[n], mNext[n]);
         n = mNext[n];
      }
   }

   readonly Point3f[] mPts = [.. mesh.Vertex.Select (a => a.Pos)];   // Input points
   ImmutableArray<int> mTri = mesh.Triangle;                         // And their triangulation
   List<Point3f> mRaw = [];                  // Output points...
   List<int> mNext = [], mPrev = [];         // Combined into a doubly-linked list
   Dictionary<Point3f, int> mEnds = new (Point3fComparer.Delta);     // Index (into mRaw) of dangling ends
}
#endregion

class Point3fComparer (float threshold) : IEqualityComparer<Point3f> {
   public bool Equals (Point3f a, Point3f b)
      => a.X.Round (threshold) == b.X.Round (threshold)
      && a.Y.Round (threshold) == b.Y.Round (threshold)
      && a.Z.Round (threshold) == b.Z.Round (threshold);

   public int GetHashCode (Point3f a)
      => HashCode.Combine (a.X.Round (threshold), a.Y.Round (threshold), a.Z.Round (threshold));

   /// <summary>A Point3f comparer that compares points with a threshold of 1e-3</summary>
   public static readonly Point3fComparer Delta = new (1e-3f);
   /// <summary>A Point3f comparer that compares points with a threshold of 1e-6</summary>
   public static readonly Point3fComparer Epsilon = new (1e-6f);
}
