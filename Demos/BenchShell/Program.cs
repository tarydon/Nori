// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Nori;

namespace NBench;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      for (char ch = 'A'; ch <= 'J'; ch++) {
         var polys = LoadPolys (ch);
         mPolys.Add (polys); mOuter.Add (polys.MaxIndexBy (a => a.GetBound ().Area));

         List<Point2> pts = []; List<int> splits = [0];
         foreach (var p in polys) { pts.AddRange (p.Pts); splits.Add (pts.Count); }
         mPts.Add (pts); mSplits.Add (splits);
      }
   }
   List<List<Poly>> mPolys = [];
   List<int> mOuter = [];
   List<List<Point2>> mPts = [];
   List<List<int>> mSplits = [];

   List<Poly> LoadPolys (char ch) {
      List<Poly> input = []; List<Point2> pts = [];
      var dwg = DXFReader.Load ($"N:/TData/Geom/Tess/{ch}.dxf");
      foreach (var e2p in dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")) {
         var poly = e2p.Poly;
         if (poly.HasArcs) {
            pts.Clear (); poly.Discretize (pts, Lib.FineTess, Lib.FineTessAngle);
            input.Add (Poly.Lines (pts, true));
         } else
            input.Add (poly);
      }
      return input;
   }

   [Benchmark (Baseline = true)]
   public void GLUTess () {
      for (int k = 0; k < Iter; k++) {
         for (int i = 0; i < mPts.Count; i++) {
            int n = Tess2D.Process (mPts[i], mSplits[i]).Count / 3;
         }
      }
   }

   [Benchmark]
   public void NoriTess () {
      for (int k = 0; k < Iter; k++) {
         for (int i = 0; i < mPolys.Count; i++) {
            var polys = mPolys[i];
            int outer = mOuter[i];
            using var tess = FastTess2D.Borrow ();
            for (int j = 0; j < polys.Count; j++) tess.AddPoly (polys[j], j != outer);
            tess.Process ();
         }
      }
   }

   const int Iter = 100;
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
      //var t = new Tester ();
      //t.GLUTess ();
      //t.NoriTess ();
   }
}
