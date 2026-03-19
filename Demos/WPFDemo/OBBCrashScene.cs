using System.Windows;
using System.Windows.Controls;
using Nori;
using NOBBTree = Nori.Alt.OBBTree;

namespace WPFDemo;

class OBBCrashScene : Scene3 {
   public OBBCrashScene () {
      Lib.Tracer = TraceVN.Print;
      TraceVN.TextColor = Color4.Yellow;
      
      mMesh1 = Mesh3.LoadObj ("c://etc//horse//cow.obj");
      mMesh1 *= Matrix3.Scaling (100);
      mVN1 = new Mesh3VN (mMesh1) { Mode = EShadeMode.GlassNoStencil, Color = Color4.Blue };
      Lib.Trace ($"Cow: {mMesh1.Triangle.Length / 3} triangles");
      using (new BlockTimer ("Collider.Cow build"))
         mColl1 = NOBBTree.From (mMesh1, "cow");
      mXfm1 = new XfmVN (Matrix3.Identity, mVN1);

      mMesh2 = Mesh3.LoadObj ("c://etc//horse//hand.obj");
      Lib.Trace ($"Hand: {mMesh2.Triangle.Length / 3} triangles");
      mMesh2 *= Matrix3.Scaling (70);
      mVN2 = new Mesh3VN (mMesh2) { Mode = EShadeMode.GlassNoStencil, Color = Color4.Green };
      using (new BlockTimer ("Collider.Hand build"))
         mColl2 = NOBBTree.From (mMesh2, "hand");
      mXfm2 = new XfmVN (Matrix3.Identity, mVN2);

      mDebug = new CollViewNode ();
      Update ();

      Root = new GroupVN ([mXfm1, mXfm2, mDebug, TraceVN.It]);
      Bound = new Bound3 (-100, -100, -100, 100, 100, 100);
      BgrdColor = Color4.Gray (64);
   }
   Mesh3 mMesh1;
   Mesh3 mMesh2;
   Mesh3VN mVN1, mVN2;
   NOBBTree mColl1, mColl2;
   CollViewNode mDebug;
   XfmVN mXfm2, mXfm1;

   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();
      AddLabel ("Cow");
      AddSlider ("X", -100, 100, mX, f => { mX = f; Update (); });
      AddSlider ("Y", -100, 100, mY, f => { mY = f; Update (); });
      AddSlider ("Z", -100, 100, mZ, f => { mZ = f; Update (); });
      AddSlider ("RX", -180, 180, mRx, f => { mRx = f; Update (); });
      AddSlider ("RY", -180, 180, mRx, f => { mRy = f; Update (); });
      AddSlider ("RZ", -180, 180, mRz, f => { mRz = f; Update (); });

      AddLabel ("Hand");
      AddSlider ("X2", -100, 100, mX2, f => { mX2 = f; Update (); });
      AddSlider ("Y2", -100, 100, mY2, f => { mY2 = f; Update (); });
      AddSlider ("Z2", -100, 100, mZ2, f => { mZ2 = f; Update (); });
      AddSlider ("RX2", -180, 180, mRx2, f => { mRx2 = f; Update (); });
      AddSlider ("RY2", -180, 180, mRx2, f => { mRy2 = f; Update (); });
      AddSlider ("RZ2", -180, 180, mRz2, f => { mRz2 = f; Update (); });

      Button b = new () { Content = "Random" };
      b.Click += (s, e) => Randomize ();
      ui.Add (b);

      // Helper ..................................
      void AddLabel (string text)
         => ui.Add (new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });

      void AddSlider (string text, double min, double max, double value, Action<double> setter) {
         var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness (4) };
         var label = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Center, Width = 15, VerticalAlignment = VerticalAlignment.Center };
         var slider = new Slider { Minimum = min, MinWidth = 150, Maximum = max, Value = value, Margin = new Thickness (4, 1, 4, 4) };
         slider.ValueChanged += (s, e) => setter (e.NewValue);
         slider.IsSnapToTickEnabled = false;
         sp.Children.Add (label); sp.Children.Add (slider); ui.Add (sp);
         mSliders[text] = slider;
      }
   }
   Dictionary<string, Slider> mSliders = [];
   double mX, mY, mZ = -50, mRx, mRy, mRz;
   double mX2, mY2, mZ2, mRx2, mRy2, mRz2;

   void Randomize () {
      int s = 50;
      mPause = true;
      Set ("X", mX = mR.Next (-s, s)); Set ("Y", mY = mR.Next (-s, s)); Set ("Z", mZ = mR.Next (-s, s));
      Set ("X2", mX2 = mR.Next (-s, s)); Set ("Y2", mY2 = mR.Next (-s, s)); Set ("Z2", mZ2 = mR.Next (-s, s));
      Set ("RX", mRx = mR.Next (-180, 180)); Set ("RY", mRy = mR.Next (-180, 180)); Set ("RZ", mRz = mR.Next (-180, 180));
      Set ("RX2", mRx2 = mR.Next (-180, 180)); Set ("RY2", mRy2 = mR.Next (-180, 180)); Set ("RZ2", mRz2 = mR.Next (-180, 180));
      mPause = false;
      Update (true);

      void Set (string s, double f) => mSliders[s].Value = f;
   }
   Random mR = new ();

   void Update (bool timing = false) {
      if (mPause) return;
      var xfm1 = Matrix3.Identity;
      xfm1 *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
      xfm1 *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
      xfm1 *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
      xfm1 *= Matrix3.Translation (mX, mY, mZ);
      mXfm1.Xfm = xfm1;

      var xfm2 = Matrix3.Identity;
      xfm2 *= Matrix3.Rotation (EAxis.X, mRx2.D2R ());
      xfm2 *= Matrix3.Rotation (EAxis.Y, mRy2.D2R ());
      xfm2 *= Matrix3.Rotation (EAxis.Z, mRz2.D2R ());
      xfm2 *= Matrix3.Translation (mX2, mY2, mZ2);
      mXfm2.Xfm = xfm2;

      bool crash;
      using var bc = Nori.Alt.OBBCollider.Borrow ();
      var coll1 = mColl1.With (xfm1);
      var coll2 = mColl2.With (xfm2);
      if (timing) {
         using (var bt = new BlockTimer ("Collision check")) crash = bc.Check (coll1, coll2);
      } else 
         crash = bc.Check (coll1, coll2);
      mVN2.Color = crash ? Color4.Red : Color4.Green;
      mDebug.Redraw ();
   }
   bool mPause;

   class CollViewNode : VNode {
      public override void SetAttributes () { Lux.Color = Color4.Yellow; }
      public override void Draw () => Lux.Lines (Nori.Alt.OBBCollider.Pts.Select (a => (Vec3F)a).ToArray ());
   }
}
