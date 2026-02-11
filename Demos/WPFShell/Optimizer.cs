using Nori;

namespace WPFShell;

class Optimizer {
   public Optimizer (List<Node> nodes) 
      => A = [.. nodes.OrderBy (a => a.X0)];

   public readonly List<Node> A;

   public void Process () {
      int totalCutTime = A.Sum (a => a.CutTime);
      Lib.Trace ($"All cuts: {totalCutTime}");
      Lib.Trace ($"Optimal time (equal split): {totalCutTime / 2}");
      MakeDefaultSequence ();
      DumpResults ("Default Sequence");

      MakeOptimizedSequence (100, 3);
      DumpResults ("Optimized (100,3)");
      MakeOptimizedSequence (50, 3);
      DumpResults ("Optimized (50,3)");
      MakeOptimizedSequence (60, 3);
      DumpResults ("Optimized (60,3)");
      MakeOptimizedSequence (40, 3);
      DumpResults ("Optimized (40,3)");

      Lib.Trace ("");
      MakeOptimizedSequence (50, 1);
      DumpResults ("Optimized(50,1)");
      MakeOptimizedSequence (50, 2);
      DumpResults ("Optimized(50,2)");
      MakeOptimizedSequence (50, 3);
      DumpResults ("Optimized(50,3)");
      MakeOptimizedSequence (50, 4);
      DumpResults ("Optimized(50,4)");
      MakeOptimizedSequence (50, 5);
      DumpResults ("Optimized(50,5)");
      MakeOptimizedSequence (50, 6);
      DumpResults ("Optimized(50,6)");
      MakeOptimizedSequence (50, 7);
      DumpResults ("Optimized(50,7)");
      MakeOptimizedSequence (50, 8);
      DumpResults ("Optimized(50,8)");

      Lib.Trace ("");
      MakeOptimizedSequence (50, 2);
      DumpResults ("Optimized(50,2)");
   }

   void DumpResults (string name) {
      int totalCutTime = A.Sum (a => a.CutTime);
      int processTime = Frames.Sum (a => Math.Max (a.Time0, a.Time1));
      int efficiency = (int)(100.0 * totalCutTime / (2 * processTime) + 0.5);
      Lib.Trace ($"{name}: {processTime}, Efficiency: {efficiency}%, {Frames.Count} frames");
   }

   void MakeDefaultSequence () {
      Frames.Clear ();
      A.ForEach (a => a.Done = false);
      for (; ; ) {
         var node = A.FirstOrDefault (a => !a.Done);
         if (node == null) break;
         var frame = new Frame (Frames.Count, node.X0);
         frame.Gather (A); frame.Commit ();
         Frames.Add (frame);
      }
   }

   void MakeOptimizedSequence (int step, double penalty) {
      Frames.Clear ();
      A.ForEach (a => a.Done = false);
      for (; ; ) {
         var node = A.FirstOrDefault (a => !a.Done);
         if (node == null) break;

         int xBest = 0; 
         double maxScore = double.MinValue;
         for (int xTry = node.X0 - A1 + step; xTry <= node.X0; xTry += step) {
            var f = new Frame (Frames.Count, xTry);
            f.Gather (A);
            if (f.Time0 + f.Time1 == 0) continue;
            double score = f.Time0 + f.Time1 - penalty * Math.Abs (f.Time0 - f.Time1);
            if (score > maxScore) { maxScore = score; xBest = xTry; }
         }
         var frame = new Frame (Frames.Count, xBest);
         frame.Gather (A); frame.Commit (); Frames.Add (frame);
      }
   }

   public const int A0 = 0, A1 = 1500, B0 = 2500, B1 = 4000;

   class Frame (int n, int left) {
      public readonly int N = n;
      public readonly int XLeft = left;

      public override string ToString () => $"Frame at {XLeft}, {Nodes.Count} cuts, Time: {Time0} / {Time1}";

      // Gathers all the tooling that can be processed in this frame and 
      // tags it as 'done'
      public void Gather (IReadOnlyList<Node> all) {
         mTime0 = mTime1 = 0; 
         foreach (var node in all) {
            if (node.Done) continue;
            if (node.LiesIn (XLeft + A0, XLeft + A1)) {
               node.Head0 = true; Nodes.Add (node);
               mTime0 += node.CutTime;
            } else if (node.LiesIn (XLeft + B0, XLeft + B1)) {
               node.Head0 = false; Nodes.Add (node);
               mTime1 += node.CutTime;
            }
         }
      }
      public List<Node> Nodes = [];

      public int Time0 => mTime0;
      public int Time1 => mTime1;
      int mTime0, mTime1;

      public void Commit () 
         => Nodes.ForEach (a => { a.Done = true; a.Frame = N; });
   }
   List<Frame> Frames = [];
}

class Node {
   public Node (int x0, int x1, int y, int len)
      => (X0, X1, Y, CutTime) = (x0, x1, y, len);

   public bool LiesIn (int left, int right)
      => X0 >= left && X1 <= right;

   public readonly int X0, X1;
   public readonly int Y;
   public readonly int CutTime;

   public bool Done;
   public int Frame;
   public bool Head0;
}
