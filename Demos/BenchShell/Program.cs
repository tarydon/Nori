// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;
namespace NBench;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      var cow = Mesh3.LoadObj (Lib.ReadLinesFromZip ("N:/TData/IO/MESH/cow.zip", "cow.obj"));
      cow *= Matrix3.Scaling (100);
      mCow = OBBTree.From (cow);
      var hand = Mesh3.LoadObj (Lib.ReadLinesFromZip ("N:/TData/IO/MESH/hand.zip", "hand.obj"));
      hand *= Matrix3.Scaling (70);
      mHand = OBBTree.From (hand);
   }

   [Benchmark (Baseline = true)]
   public void OBBCrash () {
      Rand r = new (42);
      int s = 50;
      for (int i = 0; i < Iter; i++) {
         int x1 = r.Next (-s, s), y1 = r.Next (-s, s), z1 = r.Next (-s, s);
         int x2 = r.Next (-s, s), y2 = r.Next (-s, s), z2 = r.Next (-s, s);
         int rx1 = r.Next (-180, 180), ry1 = r.Next (-180, 180), rz1 = r.Next (-180, 180);
         int rx2 = r.Next (-180, 180), ry2 = r.Next (-180, 180), rz2 = r.Next (-180, 180);

         var xfm1 = Matrix3.Identity;
         xfm1 *= Matrix3.Rotation (EAxis.X, rx1.D2R ());
         xfm1 *= Matrix3.Rotation (EAxis.Y, ry1.D2R ());
         xfm1 *= Matrix3.Rotation (EAxis.Z, rz1.D2R ());
         xfm1 *= Matrix3.Translation (x1, y1, z1);

         var xfm2 = Matrix3.Identity;
         xfm2 *= Matrix3.Rotation (EAxis.X, rx2.D2R ());
         xfm2 *= Matrix3.Rotation (EAxis.Y, ry2.D2R ());
         xfm2 *= Matrix3.Rotation (EAxis.Z, rz2.D2R ());
         xfm2 *= Matrix3.Translation (x2, y2, z2);

         var cow = mCow.With (xfm1); var hand = mHand.With (xfm2);
         using var bc = OBBCollider.Borrow ();
         bool crash = bc.Check (cow, hand);
      }
   }

   OBBTree mCow, mHand;
   const int Iter = 100; 
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();
   }
}
