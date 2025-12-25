namespace ConDemo;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;

[MemoryDiagnoser]
public class Benchmark {
   public Benchmark () {
      int steps = 1000;
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
      SurfaceUnlofter un = new SurfaceUnlofter (mSurf);
      double maxError = 0; 
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double error = puv.DistTo (mUVs[i]);
         if (error > maxError) maxError = error;
      }
   }

   //[Benchmark]
   public void TestOldUnlofter () {
      Unlofter un = new Unlofter (mSurf);
      double maxError = 0; 
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double error = puv.DistTo (mUVs[i]);
         if (error > maxError) maxError = error;
      }
   }

   public void RefineNewUnlofter () {
      using var bt = new BlockTimer ("New Unlofter");
      SurfaceUnlofter un = new SurfaceUnlofter (mSurf);
      double totalError = 0, maxError = 0; int iWorst = -1;
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double err = puv.DistTo (mUVs[i]);
         if (err > maxError) (maxError, iWorst) = (err, i);
         totalError += err;
      }
      Console.WriteLine ($"MaxError: {maxError:F6} at {iWorst}");
      Console.WriteLine ($"AvgError: {totalError / mPts.Count:F6}");
      Console.WriteLine ($"Actual UV: {mUVs[iWorst]}");
      Console.WriteLine ($"Computed UV: {un.GetUV (mPts[iWorst])}");
      Console.WriteLine ($"3DPoint: {mPts[iWorst]}");
      Console.WriteLine ($"Check: {mPts[iWorst].DistTo (mSurf.GetPoint (mUVs[iWorst]))}");
      un.DumpStats ();
   }

   public void RefineOldUnlofter () {
      using var bt = new BlockTimer ("Old Unlofter");
      var un = new Unlofter (mSurf);
      double totalError = 0, maxError = 0; int iWorst = -1;
      for (int i = 0; i < mPts.Count; i++) {
         Point2 puv = un.GetUV (mPts[i]);
         double err = puv.DistTo (mUVs[i]);
         if (err > maxError) (maxError, iWorst) = (err, i);
         totalError += err;
      }
      Console.WriteLine ($"MaxError: {maxError:F6} at {iWorst}");
      Console.WriteLine ($"AvgError: {totalError / mPts.Count:F6}");
      Console.WriteLine ($"Actual UV: {mUVs[iWorst]}");
      Console.WriteLine ($"Computed UV: {un.GetUV (mPts[iWorst])}");
      Console.WriteLine ($"3DPoint: {mPts[iWorst]}");
      Console.WriteLine ($"Check: {mPts[iWorst].DistTo (mSurf.GetPoint (mUVs[iWorst]))}");
   }
}

class Program {
   static void Main () {
      BenchmarkRunner.Run<Benchmark> ();
      // new Benchmark ().RefineNewUnlofter ();
   }

}