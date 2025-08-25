namespace ConDemo;
using Nori;

class Program {
   static void Foo (int max, in CoordSystem cs) {
      using (var bt = new BlockTimer (max, "ToAndFrom")) {
         for (int i = 0; i < max; i++) {
            var (m1, m2) = Matrix3.ToAndFrom (in cs);
         }
      }
      using (var bt = new BlockTimer (max, "Inverse")) {
         for (int i = 0; i < max; i++) {
            var m1 = Matrix3.To (in cs);
            var m2 = m1.GetInverse ();
         }
      }
   }

   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.Write;
      var r = new Random (1);
      Point3 pt = new Point3 (125, -50, 70);
      Vector3 vecx = new Vector3 (3, 4, 5).Normalized ();
      Vector3 vecz = new Vector3 (-10, -15, 20).Normalized ();
      Vector3 vecy = (vecz * vecx).Normalized ();
      CoordSystem cs = new CoordSystem (pt, vecx, vecy);
      Foo (100, in cs);
      Console.WriteLine (); Console.WriteLine ();
      Foo (100000000, in cs);
   }
}
