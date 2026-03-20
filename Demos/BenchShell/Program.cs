// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for various Nori benchmarking tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nori;
namespace NBench;
using NOBBTree = Nori.Alt.OBBTree;
using NOBBCollider = Nori.Alt.OBBCollider;
using FMatrix3 = Flux.Matrix3;

[MemoryDiagnoser]
public class Tester {
   public Tester () {
      Lib.Init ();
      var cow = Mesh3.LoadObj ("c:/etc/horse/cow.obj"); cow *= Matrix3.Scaling (100);
      var hand = Mesh3.LoadObj ("c:/etc/horse/hand.obj"); hand *= Matrix3.Scaling (70);
      mCow1 = new OBBTree (cow); mHand1 = new OBBTree (hand);
      mCow2 = NOBBTree.From (cow); mHand2 = NOBBTree.From (hand);

      var cow2 = Flux.Mesh3.LoadObj ("c:/etc/horse/cow.obj"); cow2 = cow2.Xform (Flux.Matrix3.Scaling (100));
      var hand2 = Flux.Mesh3.LoadObj ("c:/etc/horse/hand.obj"); hand2 = hand2.Xform (Flux.Matrix3.Scaling (70));
      mCow3 = new Flux.Collider (cow2); mHand3 = new Flux.Collider (hand2);
   }

   [Benchmark]
   public void OBBUnsafe () {
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

         bool crash = OBBCollider.It.Check (mCow1, xfm1.ToCS (), mHand1, xfm2.ToCS ());
      }
   }

   [Benchmark (Baseline = true)]
   public void OBBNew () {
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

         var cow = mCow2.With (xfm1); var hand = mHand2.With (xfm2);
         using var bc = NOBBCollider.Borrow ();
         bool crash = bc.Check (cow, hand);
      }
   }

   [Benchmark]
   public void FluxAABB () {
      Rand r = new (42);
      int s = 50;
      for (int i = 0; i < Iter; i++) {
         int x1 = r.Next (-s, s), y1 = r.Next (-s, s), z1 = r.Next (-s, s);
         int x2 = r.Next (-s, s), y2 = r.Next (-s, s), z2 = r.Next (-s, s);
         int rx1 = r.Next (-180, 180), ry1 = r.Next (-180, 180), rz1 = r.Next (-180, 180);
         int rx2 = r.Next (-180, 180), ry2 = r.Next (-180, 180), rz2 = r.Next (-180, 180);

         var xfm1 = FMatrix3.Identity;
         xfm1 *= FMatrix3.Rotation (Flux.EAxis.X, rx1.D2R ());
         xfm1 *= FMatrix3.Rotation (Flux.EAxis.Y, ry1.D2R ());
         xfm1 *= FMatrix3.Rotation (Flux.EAxis.Z, rz1.D2R ());
         xfm1 *= FMatrix3.Translation (x1, y1, z1);

         var xfm2 = FMatrix3.Identity;
         xfm2 *= FMatrix3.Rotation (Flux.EAxis.X, rx2.D2R ());
         xfm2 *= FMatrix3.Rotation (Flux.EAxis.Y, ry2.D2R ());
         xfm2 *= FMatrix3.Rotation (Flux.EAxis.Z, rz2.D2R ());
         xfm2 *= FMatrix3.Translation (x2, y2, z2);

         var cow = new Flux.Collider (mCow3, xfm1);
         var hand = new Flux.Collider (mHand3, xfm2);
         bool crash = Flux.Collider.Crash (cow, hand);
      }
   }

   OBBTree mCow1, mHand1;
   NOBBTree mCow2, mHand2;
   Flux.Collider mCow3, mHand3;
   const int Iter = 100; 
}

static class Program {
   public static void Main () {
      BenchmarkRunner.Run<Tester> ();

      //var t = new Tester ();
      //t.OldOBB ();
      //t.NewOBB ();
   }
}
