namespace ConDemo;
using Nori;

class Question {
   public Question (Random r) {
      int n = r.Next (4, 20); // 
      int nodes = n + 2;
      double[] knots = new double[nodes + 4];
      for (int i = 0; i < n; i++) knots[i + 3] = i * 10;
      for (int j = n + 3; j < knots.Length; j++) knots[j] = knots[n + 2];
      Point2[] ctrl = new Point2[nodes];
      for (int i = 0; i < ctrl.Length; i++)
         ctrl[i] = new (r.Next (0, 100), r.Next (0, 100));
      Spline = new Spline2 ([.. ctrl], [.. knots], []);
      Discrete = [];  
   }

   public void Evaluate () {
      int pts = 10000;
      Discrete = new Point2[pts + 1];
      double min = Spline.Imp.Knot[0], max = Spline.Imp.Knot[^1];
      for (int i = 0; i <= pts; i++)
         Discrete[i] = Spline.Evaluate (((double)i / pts).Along (min, max));
   }

   public Spline2 Spline;
   public Point2[] Discrete;
}

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.Write;
      Random r = new Random (1);
      List<Question> Q = new List<Question> ();
      for (int i = 0; i < 10000; i++) Q.Add (new Question (r));

      using (var bt = new BlockTimer (Q.Count * 10000, "Evaluate")) {
         foreach (var q in Q) q.Evaluate ();
         Parallel.For (0, Q.Count, n => Q[n].Evaluate ());
      }
      Q[0].Evaluate ();
   }
}
