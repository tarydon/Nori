namespace ConDemo;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;

[MemoryDiagnoser]
public class Benchmark {
   public Benchmark (int steps) {
      Lib.Init ();
      Lib.Tracer = Console.Write;
      var model = new T3XReader ("C:/Etc/T3/5X-051.t3x").Load ();
      mSurf = (E3NurbsSurface)model.Ents.First (a => a.Id == 338);      
      var domain = mSurf.Domain;

      int evals = steps * steps;
      for (int j = 0; j <= steps; j++) {
         double v = (j / (double)steps).Along (domain.Y.Min, domain.Y.Max);
         for (int i = 0; i <= steps; i++) {
            double u = (i / (double)steps).Along (domain.X.Min, domain.X.Max);
            Point2 puv = new (u, v);
            Point3 p3d = mSurf.GetPoint (puv);
            mUVs.Add (puv); mPts.Add (p3d);
         }
      }
   }
   E3Surface mSurf;
   List<Point3> mPts = [];
   List<Point2> mUVs = [];

   [Benchmark]
   public void TestNewUnlofter () {
      using var bt = new BlockTimer ("New Unlofter");
      SurfaceUnlofter un = new SurfaceUnlofter (mSurf);
      double totalError = 0, maxError = 0; 
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double error = puv.DistTo (mUVs[i]);
         if (error > maxError) maxError = error;
         totalError += error;
      }
      Console.WriteLine ($"MaxErr: {maxError:F5}, AvgErr: {totalError / mPts.Count:F5}");
   }

   [Benchmark]
   public void TestOldUnlofter () {
      using var bt = new BlockTimer ("Old Unlofter");
      Unlofter un = new Unlofter (mSurf);
      double totalError = 0, maxError = 0; 
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double error = puv.DistTo (mUVs[i]);
         if (error > maxError) maxError = error;
         totalError += error;
      }
      Console.WriteLine ($"MaxErr: {maxError:F5}, AvgErr: {totalError / mPts.Count:F5}");
   }

   public void RefineNewUnlofter () {
      using var bt = new BlockTimer ("Evaluate");
      SurfaceUnlofter un = new SurfaceUnlofter (mSurf);
      double maxError = 0; int iWorst = -1;
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double err = puv.DistTo (mUVs[i]);
         if (err > maxError) (maxError, iWorst) = (err, i);
      }
      Console.WriteLine ($"{iWorst}");
      Console.WriteLine ($"UVError: {maxError}");
      Console.WriteLine ($"Actual UV: {mUVs[iWorst]}");
      Console.WriteLine ($"Computed UV: {un.GetUV (mPts[iWorst])}");
      Console.WriteLine ($"3DPoint: {mPts[iWorst]}");
      Console.WriteLine ($"Check: {mPts[iWorst].DistTo (mSurf.GetPoint (mUVs[iWorst]))}");
   }
}

class Program {
   static void Main () {
      var b = new Benchmark (1000);
      b.RefineNewUnlofter ();

   //   b.TestNewUnlofter ();
   //   for (int i = 0; i < 10; i++) b.TestOldUnlofter ();

   //   Console.WriteLine (); Console.WriteLine ();
   ////   Console.WriteLine ();
   //   for (int i = 0; i < 10; i++) b.TestNewUnlofter ();
   ////   // BenchmarkRunner.Run<Benchmark> ();
   }
}