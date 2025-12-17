namespace Nori;
using static Math;

public class MeshSlicer {
   public MeshSlicer (Mesh3 mesh) {
      mPts = [.. mesh.Vertex.Select (a => a.Pos)];
      mTri = mesh.Triangle;
   }
   readonly Point3f[] mPts;
   ImmutableArray<int> mTri;

   public List<Point3f> Slice (PlaneDef pd) {
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
      return [];
   }
   List<double> mDist = [];

   // Adds an interpolated point between vertices a and b, where
   // da and db are the distances of these vertices from the plane
   void Add (int a, int b, double da, double db) {
      da = Abs (da); db = Abs (db);
      Point3f dst = (da / (da + db)).Along (mPts[a], mPts[b]);
      if (mOpen) { 
         mSrc = dst; mOpen = false; 
      } else {
         mOpen = true;
         // We want to add a new thread mSrc .. dst.
         int n = mRaw.Count;
         int n1 = mEnds.GetValueOrDefault (mSrc, -1);
         int n2 = mEnds.GetValueOrDefault (dst, -1);

         if (n1 != -1) {
            // The point mSrc is not new, it has been seen before
            if (n2 == -1) {
               // The point dst is new, so we can add it into the mRaw array
               mRaw.Add (dst); mPrev.Add (-1); mNext.Add (-1);
               if (mNext[n1] == -1) { Set (mNext, n1, n); Set (mPrev, n, n1); }      // Add new tail 
               else if (mPrev[n1] == -1) { Set (mPrev, n1, n); Set (mNext, n, n1); } // Add new head
               else throw new InvalidOperationException (); // Should not happen
               mEnds.Remove (mSrc); mEnds.Add (dst, n);
            } else {
               // The point mSrc and dst are both existing endpoints, so we can 
               // connect two threads together
               if (mNext[n1] == -1) {
                  if (mPrev[n2] != -1) ReverseTail (n2);
                  Set (mNext, n1, n2); Set (mPrev, n2, n1);
               } else if (mPrev[n1] == -1) {
                  if (mNext[n2] != -1) ReverseHead (n2);
                  Set (mNext, n2, n1); Set (mPrev, n1, n2);
               } else throw new NotImplementedException ();
               mEnds.Remove (mSrc); mEnds.Remove (dst);
            }
            return;
         } else if (n2 != -1) {
            // Here, dst is already in mRaw, while mSrc is new, so needs to be added
            mRaw.Add (mSrc); mPrev.Add (-1); mNext.Add (-1);
            if (mNext[n2] == -1) { Set (mNext, n2, n); Set (mPrev, n, n2); } 
            else if (mPrev[n2] == -1) { Set (mPrev, n2, n); Set (mNext, n, n2); } 
            else throw new NotImplementedException ();
            mEnds.Remove (dst); mEnds.Add (mSrc, n);
            return;
         }
        
         // If we get here, this thread src .. dst is completely new and unconnected
         // to any existing. So add this into the dictionary
         mRaw.Add (mSrc); mNext.Add (n + 1); mPrev.Add (-1);
         mRaw.Add (dst); mNext.Add (-1); mPrev.Add (n);
         mEnds[mSrc] = n; mEnds[dst] = n + 1;
      }
   }
   Point3f mSrc;
   bool mOpen = true;

   void Set (List<int> list, int n, int v) {
      if (list[n] != -1) Fatal ();
      list[n] = v;
   }

   void ReverseHead (int n) {
      if (mPrev[n] != -1 || mNext[n] == -1) Fatal ();
      while (n != -1) {
         int next = mNext[n];
         mNext[n] = mPrev[n]; mPrev[n] = next;
         n = next;
      }
   }

   void ReverseTail (int n) {
      if (mPrev[n] == -1 || mNext[n] != -1) Fatal ();
      while (n != -1) {
         int prev = mPrev[n];
         mPrev[n] = mNext[n]; mNext[n] = prev;
         n = prev;
      }
   }

   void Fatal () {
      throw new NotImplementedException ();
   }

   List<Point3f> mRaw = [];
   List<int> mNext = [], mPrev = [];
   Dictionary<Point3f, int> mEnds = new (Point3fComparer.Delta);
}

class Point3fComparer (float threshold) : IEqualityComparer<Point3f> {
   public bool Equals (Point3f a, Point3f b)
      => a.X.Round (threshold) == b.X.Round (threshold)
      && a.Y.Round (threshold) == b.Y.Round (threshold)
      && a.Z.Round (threshold) == b.Z.Round (threshold);

   public int GetHashCode (Point3f a)
      => HashCode.Combine (a.X.Round (threshold), a.Y.Round (threshold), a.Z.Round (threshold));

   public static readonly Point3fComparer Delta = new (1e-3f);
   public static readonly Point3fComparer Epsilon = new (1e-6f);
}
