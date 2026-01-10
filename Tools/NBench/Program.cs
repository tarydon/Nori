using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;

namespace Benchmarking;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      var model = new T3XReader ("N:/Demos/Data/5X-024-Blank.t3x").Load ();
      mMeshes = [.. model.Ents.OfType<E3Surface> ().Select (a => a.Mesh)];
      mBound = model.Bound;
   }
   Mesh3[] mMeshes;
   Bound3 mBound;

   [Benchmark]
   public void PlaneMeshInt () {
      int step = 1; 
      var pmi = new PlaneMeshIntersector (mMeshes);
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (mBound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();

         double y = (i / 100.0).Along (mBound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
      }
   }

   [Benchmark]
   public void MeshSlice () {
      int step = 1;
      var pmi = new MeshSlicer (mMeshes);
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (mBound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();

         double y = (i / 100.0).Along (mBound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
      }
   }

   public void Test1 () {
      int step = 25;
      var pmi = new PlaneMeshIntersector (mMeshes);
      var sb = new StringBuilder ();
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (mBound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
         sb.AppendLine ($"X = {x}");
         foreach (var pt in set[0].Pts) sb.AppendLine ($" {pt}");

         double y = (i / 100.0).Along (mBound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
         sb.AppendLine ($"Y = {x}");
         foreach (var pt in set[0].Pts) sb.AppendLine ($" {pt}");
      }
      File.WriteAllText ("c:/etc/test1.txt", sb.ToString ());
   }

   public void Test2 () {
      int step = 25;
      var pmi = new MeshSlicer (mMeshes);
      var sb = new StringBuilder ();
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (mBound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         var set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
         sb.AppendLine ($"X = {x}");
         foreach (var pt in set[0].Pts) sb.AppendLine ($" {pt}");

         double y = (i / 100.0).Along (mBound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         set = pmi.Compute (pdef);
         if (set.Count != 1) throw new InvalidOperationException ();
         sb.AppendLine ($"Y = {x}");
         foreach (var pt in set[0].Pts) sb.AppendLine ($" {pt}");
      }
      File.WriteAllText ("c:/etc/test2.txt", sb.ToString ());
   }
}

static class Program {
   public static void Main () {
      // BenchmarkRunner.Run<Tester> ();

      var t = new Tester ();
      t.Test1 ();
      t.Test2 ();
      if (File.ReadAllText ("c:/etc/test1.txt") != File.ReadAllText ("c:/etc/test2.txt"))
         Console.WriteLine ("FILES DIFFERENT!");
      else
         Console.WriteLine ("Files same");
   }
}
