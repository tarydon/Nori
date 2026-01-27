namespace Nori;

public readonly partial struct OBB2 {
   public OBB2 (Point3f cen, Vector3f x, Vector3f y, Vector3f ext) {
      Cen = cen; X = x; Y = y; Ext = ext;
   }

   /// <summary>Center point of OBB</summary>
   public readonly Point3f Cen;
   /// <summary>Local X axis of the OBB</summary>
   public readonly Vector3f X;
   /// <summary>Local Y axis of the OBB</summary>
   public readonly Vector3f Y;
   /// <summary>Local Z axis of the OBB</summary>
   public Vector3f Z => X * Y;
   /// <summary>Half-extents of the bounding box in X, Y, Z</summary>
   public readonly Vector3f Ext;

   public double Area => 8 * (Ext.X * Ext.Y + Ext.Y * Ext.Z + Ext.X * Ext.Z);

   public static OBB2 FromPCA (ReadOnlySpan<Point3f> pts) {
      // Compute the mean (xc, yc, zc)
      int n = pts.Length;
      float xc = 0, yc = 0, zc = 0;
      foreach (var p in pts) { xc += p.X; yc += p.Y; zc += p.Z; }
      xc /= n; yc /= n; zc /= n;

      // Compute the covariance
      float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
      foreach (var p in pts) {
         float x = p.X - xc, y = p.Y - yc, z = p.Z - zc;
         xx += x * x; yy += y * y; zz += z * z;
         xy += x * y; xz += x * z; yz += y * z;
      }
      xx /= n; yy /= n; zz /= n;
      xy /= n; xz /= n; yz /= n;

      // Prepare for JacobiEigenDecomposition
      Span<Vector3f> axis = stackalloc Vector3f[3];
      axis[0] = new (1, 0, 0); axis[1] = new (0, 1, 0); axis[2] = new (0, 0, 1);
      Span<float> a = stackalloc float[9];
      a[0] = xx; a[1] = xy; a[2] = xy;
      a[3] = xy; a[4] = yy; a[5] = yz;
      a[6] = xz; a[7] = yz; a[8] = zz;

      // Iterate to compute the eigenvectors
      for (int iter = 0; iter < 50; iter++) {
         Rotate (a, axis, 0, 1);
         Rotate (a, axis, 0, 2);
         Rotate (a, axis, 1, 2);

         float offDiagonal = MathF.Abs (a[1]) + MathF.Abs (a[2]) + MathF.Abs (a[5]);
         if (offDiagonal < 1e-6f) break;
      }

      // Now ensure the vectors are an orthonormal basis
      axis[0] = axis[0].Normalized (); axis[1] = axis[1].Normalized ();
      axis[2] = (axis[0] * axis[1]).Normalized ();
      axis[1] = (axis[2] * axis[0]).Normalized ();

      // Project points onto axes
      Span<float> min = stackalloc float[3], max = stackalloc float[3];
      for (int i = 0; i < 3; i++) { min[i] = float.MaxValue; max[i] = float.MinValue; }
      foreach (var p in pts) {
         float x = p.X - xc, y = p.Y - yc, z = p.Z - zc;
         for (int i = 0; i < 3; i++) {
            var ax = axis[i];
            float dot = x * ax.X + y * ax.Y + z * ax.Z;
            min[i] = MathF.Min (min[i], dot);
            max[i] = MathF.Max (max[i], dot);
         }
      }

      // Compute center and half-sizes
      Vector3f ext = (new Vector3f (max[0] - min[0], max[1] - min[1], max[2] - min[2])) * 0.5f;
      Point3f cen = new (xc, yc, zc);
      for (int i = 0; i < 3; i++) 
         cen += axis[i] * ((min[i] + max[i]) / 2f);

      // Now, return the OBB
      return new OBB2 (cen, axis[0], axis[1], ext);
   }

   static void Rotate (Span<float> a, Span<Vector3f> axis, int p, int q) {
      float aPQ = a[p * 3 + q];
      if (Math.Abs (aPQ) < 1e-6f) return;

      // Note that since we flatten a[3,3] to a flat vector, a[i,j] is effectively
      // reached as a[i * 3 + j]. Thus a[p,p] becomes a[p * 3 + p] (or a[p * 4])
      float aPP = a[p * 4], aQQ = a[q * 4];
      float diff = aQQ - aPP, t = diff == 0 ? 1 : aPQ / diff;
      float c = 1 / MathF.Sqrt (1 + t * t), s = t * c;

      a[p * 4] = c * c * aPP - 2 * s * c * aPQ + s * s * aQQ;
      a[q * 4] = s * s * aPP + 2 * s * c * aPQ + c * c * aQQ;
      a[p * 3 + q] = a[q * 3 + p] = 0;

      int r = 3 - p - q;   // Since p,q,r are selected from (0,1,2)
      float aRP = a[r * 3 + p], aRQ = a[r * 3 + q];
      a[r * 3 + p] = a[p * 3 + r] = c * aRP - s * aRQ;
      a[r * 3 + q] = a[q * 3 + r] = s * aRP + c * aRQ;

      Vector3f vp = axis[p], vq = axis[q];
      axis[p] = vp * c - vq * s;
      axis[q] = vp * s + vq * c;
   }

   public static OBB2 FromHull13 (ReadOnlySpan<Point3f> pts) {
      Span<float> fMin = stackalloc float[13], fMax = stackalloc float[13];
      Span<int> iMin = stackalloc int[13], iMax = stackalloc int[13];
      for (int i = 0; i < 13; i++) { fMin[i] = float.MaxValue; fMax[i] = float.MinValue; }
      for (int i = 0; i < pts.Length; i++) {
         Point3f p = pts[i];
         // Get the projection of this point on each of the 13 axes we want to use
         // and update the min / max
         for (int k = 0; k < 13; k++) {
            float f = 0;
            switch (k) {
               case 0: f = p.X; break;                // X axis (1,0,0)
               case 1: f = p.Y; break;                // Y axis (0,1,0)
               case 2: f = p.Z; break;                // Z axis (0,0,1)
               case 3: f = p.X + p.Y; break;          // Diagonal 1 on XY plane (1,1,0)
               case 4: f = p.X - p.Y; break;          // Diagonal 2 on XY plane (1,-1,0)
               case 5: f = p.X + p.Z; break;          // Diagonal 1 on XZ plane (1,0,1)
               case 6: f = p.X - p.Z; break;          // Diagonal 2 on XZ plane (1,0,-1)
               case 7: f = p.Y + p.Z; break;          // Diagonal 1 on YZ plane (0,1,1)
               case 8: f = p.Y - p.Z; break;          // Diagonal 2 on YZ plane (0,1,-1)
               case 9: f = p.X + p.Y + p.Z; break;    // Diag towards corner (1,1,1)
               case 10: f = p.X + p.Y - p.Z; break;   // Diag towards corner (1,1,-1)
               case 11: f = p.X - p.Y - p.Z; break;   // Diag towards corner (1,-1,-1)
               case 12: f = p.X - p.Y + p.Z; break;   // Diag towards corner (1,-1,1)
            }
            if (f < fMin[k]) { fMin[k] = f; iMin[k] = i; }
            if (f > fMax[k]) { fMax[k] = f; iMax[k] = i; }
         }
      }
      Span<Point3f> hull = stackalloc Point3f[26];
      int cDone = 0; sDone.Clear ();
      for (int i = 0; i < 13; i++) {
         int a = iMin[i], b = iMax[i];
         if (sDone.Add (a)) hull[cDone++] = pts[a];
         if (sDone.Add (b)) hull[cDone++] = pts[b];
      }
      return FromPCA (hull[0..cDone]);
   }
   static HashSet<int> sDone = [];

   // Using 6 axes (icosahedron)
   public static OBB2 FromHull6 (ReadOnlySpan<Point3f> pts) {
      const float a = 0.61803399f;
      Span<float> fMin = stackalloc float[6], fMax = stackalloc float[6];
      Span<int> iMin = stackalloc int[6], iMax = stackalloc int[6];
      for (int i = 0; i < 6; i++) { fMin[i] = float.MaxValue; fMax[i] = float.MinValue; }
      for (int i = 0; i < pts.Length; i++) {
         Point3f p = pts[i];
         // Get the projection of this point on each of the 13 axes we want to use
         // and update the min / max
         for (int k = 0; k < 6; k++) {
            float f = 0;
            switch (k) {
               case 0: f = p.Y + p.Z * a; break;      // (0,1,a)
               case 1: f = p.Y - p.Z * a; break;      // (0,1,-a)
               case 2: f = p.X + p.Y * a; break;      // (1,a,0)
               case 3: f = p.X - p.Y * a; break;      // (1,-a,0)
               case 4: f = p.X * a + p.Z; break;      // (a,0,1)
               case 5: f = p.X * a - p.Z; break;      // (a,0,-1)
            }
            if (f < fMin[k]) { fMin[k] = f; iMin[k] = i; }
            if (f > fMax[k]) { fMax[k] = f; iMax[k] = i; }
         }
      }
      Span<Point3f> hull = stackalloc Point3f[12];
      int cDone = 0; sDone.Clear ();
      for (int i = 0; i < 6; i++) {
         int p = iMin[i], q = iMax[i];
         if (sDone.Add (p)) hull[cDone++] = pts[p];
         if (sDone.Add (q)) hull[cDone++] = pts[q];
      }
      return FromPCA (hull[0..cDone]);
   }

   // Using 10 axes (dodecahedron)
   public static OBB2 FromHull10 (ReadOnlySpan<Point3f> pts) {
      const float a = 0.61803399f, b = 1 + a;
      Span<float> fMin = stackalloc float[10], fMax = stackalloc float[10];
      Span<int> iMin = stackalloc int[10], iMax = stackalloc int[10];
      for (int i = 0; i < 10; i++) { fMin[i] = float.MaxValue; fMax[i] = float.MinValue; }
      for (int i = 0; i < pts.Length; i++) {
         Point3f p = pts[i];
         // Get the projection of this point on each of the 13 axes we want to use
         // and update the min / max
         for (int k = 0; k < 10; k++) {
            float f = 0;
            switch (k) {
               case 0: f = p.Y * a + p.Z * b; break;  // (0,a,b)
               case 1: f = p.Y * a - p.Z * b; break;  // (0,a,-b)
               case 2: f = p.X * a + p.Y * b; break;  // (a,b,0)
               case 3: f = p.X * a - p.Y * b; break;  // (a,-b,0)
               case 4: f = p.X * b + p.Z * a; break;  // (b,0,a)
               case 5: f = p.X * b - p.Z * a; break;  // (b,0,-a)
               case 6: f = p.X + p.Y + p.Z; break;    // (1,1,1)
               case 7: f = p.X + p.Y - p.Z; break;    // (1,1,-1)
               case 8: f = p.X - p.Y + p.Z; break;    // (1,-1,1)
               case 9: f = p.X - p.Y - p.Z; break;    // (1,-1,-1)
            }
            if (f < fMin[k]) { fMin[k] = f; iMin[k] = i; }
            if (f > fMax[k]) { fMax[k] = f; iMax[k] = i; }
         }
      }
      Span<Point3f> hull = stackalloc Point3f[20];
      int cDone = 0; sDone.Clear ();
      for (int i = 0; i < 10; i++) {
         int p = iMin[i], q = iMax[i];
         if (sDone.Add (p)) hull[cDone++] = pts[p];
         if (sDone.Add (q)) hull[cDone++] = pts[q];
      }
      return FromPCA (hull[0..cDone]);
   }

   // Using 7 axes (like Dito algorithm)
   public static OBB2 FromHull7 (ReadOnlySpan<Point3f> pts) {
      Span<float> fMin = stackalloc float[7], fMax = stackalloc float[7];
      Span<int> iMin = stackalloc int[7], iMax = stackalloc int[7];
      for (int i = 0; i < 7; i++) { fMin[i] = float.MaxValue; fMax[i] = float.MinValue; }
      for (int i = 0; i < pts.Length; i++) {
         Point3f p = pts[i];
         // Get the projection of this point on each of the 13 axes we want to use
         // and update the min / max
         for (int k = 0; k < 7; k++) {
            float f = 0;
            switch (k) {
               case 0: f = p.X; break;                // (1,0,0)
               case 1: f = p.Y; break;                // (0,1,0)
               case 2: f = p.Z; break;                // (0,0,1)
               case 3: f = p.X + p.Y + p.Z; break;    // (1,1,1)
               case 4: f = p.X + p.Y - p.Z; break;    // (1,1,-1)
               case 5: f = p.X - p.Y + p.Z; break;    // (1,-1,1)
               case 6: f = p.X - p.Y - p.Z; break;    // (1,-1,-1)
            }
            if (f < fMin[k]) { fMin[k] = f; iMin[k] = i; }
            if (f > fMax[k]) { fMax[k] = f; iMax[k] = i; }
         }
      }
      Span<Point3f> hull = stackalloc Point3f[14];
      int cDone = 0; sDone.Clear ();
      for (int i = 0; i < 7; i++) {
         int p = iMin[i], q = iMax[i];
         if (sDone.Add (p)) hull[cDone++] = pts[p];
         if (sDone.Add (q)) hull[cDone++] = pts[q];
      }
      return FromPCA (hull[0..cDone]);
   }

}
