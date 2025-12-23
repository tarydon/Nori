namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.Write;
      var model = new T3XReader ("C:/Etc/T3/5X-051.t3x").Load ();
      var nurb = (E3NurbsSurface)model.Ents.First (a => a.Id == 338);      
      var domain = nurb.Domain;
      List<Point2> uvs = [];
      List<Point3> pts = [];

      int steps = 1000, evals = steps * steps;
      for (int j = 0; j <= steps; j++) {
         double v = (j / (double)steps).Along (domain.Y.Min, domain.Y.Max);
         for (int i = 0; i <= steps; i++) {
            double u = (j / (double)steps).Along (domain.X.Min, domain.X.Max);
            Point2 puv = new (u, v);
            Point3 p3d = nurb.GetPoint (puv);
            uvs.Add (puv); pts.Add (p3d);
         }
      }
      for (int i = 0; i < 100; i++) {
         TestUnlofter (nurb, pts, uvs);
         TestUnlofter2 (nurb, pts, uvs);
      }
      Console.WriteLine ();
   }

   static void TestUnlofter (E3NurbsSurface surf, List<Point3> pts, List<Point2> uvs) {
      using var bt = new BlockTimer ("New Unlofter");
      SurfaceUnlofter un = new SurfaceUnlofter (surf);
      double totalError = 0;
      for (int i = 0; i < pts.Count; i++) {
         Point2 puv = un.GetUV (pts[i]);
         totalError += puv.DistTo (uvs[i]);
      }
//      Console.WriteLine ($"Average Error: {totalError / pts.Count}");
   }

   static void TestUnlofter2 (E3NurbsSurface surf, List<Point3> pts, List<Point2> uvs) {
      using var bt = new BlockTimer ("Old Unlofter");
      Unlofter un = new Unlofter (surf);
      double totalError = 0;
      for (int i = 0; i < pts.Count; i++) {
         Point2 puv = un.GetUV (pts[i]);
         totalError += puv.DistTo (uvs[i]);
      }
//      Console.WriteLine ($"Average Error: {totalError / pts.Count}");
   }
}
