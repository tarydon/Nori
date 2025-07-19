namespace WPFDemo;
using System.Windows;
using System.Windows.Controls;
using Nori;

class RobotScene : Scene3 {
   public RobotScene () {
      mMech = Mechanism.Load ("N:/Wad/FanucX/mechanism.curl");
      mTip = mMech.EnumTree ().First (a => a.Name == "Tip");
      BgrdColor = Color4.Gray (96);
      Bound = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      var robot = new MechanismVN (mMech);
      var gripper = mGripper = new XfmVN (Matrix3.Identity, new RBRDebugVN ());
      Root = new GroupVN ([robot, gripper, TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
      mGripper.Xfm = mTip.Xfm;
      Lib.Tracer = TraceVN.Print;
      mJoints = [.. "SLURBT".Select (a => mMech.FindChild (a.ToString ())!)];
      for (int i = 0; i < 6; i++) {
         var m = mJoints[i];
         double a = m.JMin, b = m.JMax, delta = i switch { 1 => 0, 4 => 0, _ => 0 };
         mMin[i] = a + delta; mMax[i] = b + delta;
      }
      mSolver = new (150, 770, 0, 0, 1016, 175, mMin, mMax);
      mCS = mHome; ComputeIK ();
   }
   double[] mMin = new double[6], mMax = new double[6];

   public void CreateUI (UIElementCollection ui) {
      ui.Add (new TextBlock { Text = "Forward", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });
      foreach (var m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         AddSlider (m.Name, m.JMin, m.JMax, m.JValue, f => { m.JValue = f; Redo (true); });
      }
      ui.Add (new TextBlock { Text = "Inverse", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });
      AddSlider ("X", -1000, 1000, mX, f => { mX = f; ComputeIK (); });
      AddSlider ("Y", -1000, 1000, mY, f => { mY = f; ComputeIK (); });
      AddSlider ("Z", -1000, 1000, mZ, f => { mZ = f; ComputeIK (); });
      AddSlider ("Rx", -180, 180, mRx, f => { mRx = f; ComputeIK (); });
      AddSlider ("Ry", -180, 180, mRy, f => { mRy = f; ComputeIK (); });
      AddSlider ("Rz", -180, 180, mRz, f => { mRz = f; ComputeIK (); });
      ui.Add (new TextBlock { Text = "Stances", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });

      ui.Add (mStances);
      mStances.SelectionChanged += (s, e) => {
         if (!mComputingIK) {
            mSelStance = mStances.SelectedIndex; ComputeIK ();
         }
      };

      // Helper ..................................
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

   ListBox mStances = new () { Margin = new Thickness (8, 0, 8, 4) };
   Dictionary<string, Slider> mSliders = [];
   double mX, mY, mZ, mRx, mRy, mRz;

   void ComputeIK () {
      mComputingIK = true;
      var cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
      mCS = cs * Matrix3.Translation ((Vector3)(mHome.Org + new Vector3 (mX, mY, mZ)));
      bool newCode = true;
      if (newCode) {
         mStances.Items.Clear ();
         mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
         for (int j = 0; j < 8; j++) {
            var a = mSolver.Solutions[j];
            if (a.OK) mStances.Items.Add ($"Stance {j + 1}");
            else mStances.Items.Add ("----");
            if (j == mSelStance)
               for (int i = 0; i < 6; i++) {
                  double value = a.GetJointAngle (i);
                  // if (i == 1) value += 90;
                  // if (i == 4) value -= 90;
                  mJoints[i].JValue = value;
               }
         }
      } else {
         var stances = mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
         mStances.Items.Clear ();
         for (int j = 0; j < 8; j++) {
            bool ok = true;
            for (int i = 0; i < 6; i++) {
               double a = stances[j][i].R2D ();
//               if (i == 1) a += 90;
//               if (i == 4) a -= 90;
               var m = mJoints[i];
               if (a < m.JMin || a > m.JMax) ok = false;
               if (j == mSelStance) m.JValue = a;
            }
            if (ok) mStances.Items.Add ($"Stance {j + 1}");
            else mStances.Items.Add ($"----");
         }
      }
      mGripper.Xfm = mTip.Xfm;
      mComputingIK = false;
   }
   bool mComputingIK;
   Mechanism[] mJoints;
   int mSelStance;

   void Redo (bool forward) {
      mGripper.Xfm = mTip.Xfm;
   }

   public Mechanism Mech => mMech;
   CoordSystem mHome = new (new (1166, 0, 1161 - 565), Vector3.XAxis, Vector3.YAxis), mCS;
   RBRSolver mSolver;
   Mechanism mMech, mTip;
   XfmVN mGripper;
}

class RBRDebugVN : VNode {
   public override void Draw () {
      Lux.Color = Color4.Yellow;
      Draw (new (0, 0, 0), -Vector3.XAxis, Vector3.YAxis);
   }

   void Draw (Point3 pt, Vector3 x, Vector3 y) {
      List<Vec3F> set = [];
      set.Add ((Vec3F)pt); set.Add ((Vec3F)(pt + x * 400));
      set.Add ((Vec3F)pt); set.Add ((Vec3F)(pt + y * 200));
      Lux.Lines (set.AsSpan ());
   }
}
