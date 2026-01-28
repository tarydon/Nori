namespace Nori;

public readonly partial struct OBB2 {
   public void FromTetra (ReadOnlySpan<Point3f> pts) {
      Span<float> fMin = stackalloc float[7], fMax = stackalloc float[7];
      Span<int> iMin = stackalloc int[7], iMax = stackalloc int[7];
      Span<float> fVal = stackalloc float[7];
      for (int i = 0; i < 7; i++) { fMin[i] = float.MaxValue; fMax[i] = float.MinValue; }

      // Compute the extremal points along the 7 axes
      for (int i = 0; i < pts.Length; i++) {
         Point3f p = pts[i];
         fVal[0] = p.X;
         fVal[1] = p.Y;
         fVal[2] = p.Z;
         fVal[3] = p.X + p.Y + p.Z;
         fVal[4] = p.X + p.Y - p.Z;
         fVal[5] = p.X - p.Y - p.Z;
         fVal[6] = p.X - p.Y + p.Z;
         for (int k = 0; k < 7; k++) {
            float f = fVal[k];
            if (f < fMin[k]) { fMin[k] = f; iMin[k] = i; }
            if (f < fMax[k]) { fMax[k] = f; iMax[k] = i; }
         }
      }

      // Copy in the min and max points 
      Span<Point3f> min = stackalloc Point3f[7], max = stackalloc Point3f[7];
      for (int i = 0; i < 7; i++) { min[i] = pts[iMin[i]]; max[i] = pts[iMax[i]]; }
   }
}
