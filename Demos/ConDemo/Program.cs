using Nori;
namespace ConDemo;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      string[] ra = ["a0", "a1", "a2"];
      string[] rb = ["b0", "b1", "b2"];
      string[,] R = { { "R00", "R01", "R02" }, { "R10", "R11", "R12" }, { "R20", "R21", "R22" } };
      string[,] AR = { { "AR00", "AR01", "AR02" }, { "AR10", "AR11", "AR12" }, { "AR20", "AR21", "AR22" } };
      string[] T = ["t0", "t1", "t2"];

      Console.WriteLine ("// Test A axes: aX, aY, aZ");
      for (int i = 0; i < 3; i++) 
         Console.WriteLine ($"if (Abs ({T[i]}) > {ra[i]} + {rb[0]} * {AR[i, 0]} + {rb[1]} * {AR[i, 1]} + {rb[2]} * {AR[i, 2]}) return false;");
      Console.WriteLine ();

      Console.WriteLine ("// Test B axes: bX, bY, bZ");
      for (int i = 0; i < 3; i++)
         Console.WriteLine ($"if (Abs ({T[0]} * {R[0, i]} + {T[1]} * {R[1, i]} + {T[2]} * {R[2, i]}) > {rb[i]} + {ra[0]} * {AR[0, i]} + {ra[1]} * {AR[1, i]} + {ra[2]} * {AR[2, i]}) return false;");      
      Console.WriteLine ();

      Console.WriteLine ("// Test 9 (Ai x Bj) axis pairs");
      string[] A = ["aX", "aY", "aZ"], B = ["bX", "bY", "bZ"];
      for (int i = 0; i < 3; i++) {
         for (int j = 0; j < 3; j++) {
            int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
            int j1 = (j + 1) % 3, j2 = (j + 2) % 3;
            Console.Write ($"if (Abs ({T[i2]} * {R[i1, j]} - {T[i1]} * {R[i2, j]}) > ({ra[i1]} * {AR[i2, j]} + {ra[i2]} * {AR[i1, j]} + {rb[j1]} * {AR[i, j2]} + {rb[j2]} * {AR[i, j1]})) return false;");
            Console.WriteLine ($"  // {A[i]} x {B[j]}");
         }
         Console.WriteLine ();
      }
   }
}