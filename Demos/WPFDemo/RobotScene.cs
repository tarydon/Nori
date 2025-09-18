namespace WPFDemo;

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Nori;

class RobotScene : Scene3 {
   public RobotScene () {
      mMech = Mechanism.Load ("N:/Wad/Fanuc10/mechanism.curl");
      mTip = mMech.FindChild ("Tip")!;
      var robot = new MechanismVN (mMech);
      var gripper = mGripper = new XfmVN (Matrix3.Identity, new RBRDebugVN ());
      mJoints = [.. "SLURBT".Select (a => mMech.FindChild (a.ToString ())!)];
      for (int i = 0; i < 6; i++) {
         var m = mJoints[i];
         double a = m.JMin, b = m.JMax, delta = i switch { 1 => 0, 4 => 0, _ => 0 };
         mMin[i] = a + delta; mMax[i] = b + delta;
      }
      mSolver = new (25, 560, 35, 0, 515, 114.5, mMin, mMax);
      mCS = mHome; ComputeIK ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      Root = new GroupVN ([robot, gripper, TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
   }

   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();
      AddLabel ("Forward");
      foreach (var m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         AddSlider (m.Name, m.JMin, m.JMax, m.JValue, f => { m.JValue = f; Redo (true); });
      }
      AddLabel ("Inverse");
      AddSlider ("X", -3000, 1000, mX, f => { mX = f; ComputeIK (); });
      AddSlider ("Y", -2000, 2000, mY, f => { mY = f; ComputeIK (); });
      AddSlider ("Z", -2000, 2000, mZ, f => { mZ = f; ComputeIK (); });
      AddSlider ("Rx", -180, 180, mRx, f => { mRx = f; ComputeIK (); });
      AddSlider ("Ry", -180, 180, mRy, f => { mRy = f; ComputeIK (); });
      AddSlider ("Rz", -180, 180, mRz, f => { mRz = f; ComputeIK (); });
      AddLabel ("Stances");
      ui.Add (mStances);
      mStances.SelectionChanged += (s, e) => {
         if (!mComputingIK) {
            mSelStance = mStances.SelectedIndex; ComputeIK (mCS);
         }
      };
      if (System.IO.File.Exists ("poses.txt")) {
         ui.Add (mPoses);
         foreach (var line in System.IO.File.ReadAllLines ("poses.txt"))
            mPoses.Items.Add (line);
      }

      mPoses.SelectionChanged += OnPosesSelectionChanged;

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

   void OnPosesSelectionChanged (object sender, SelectionChangedEventArgs e) {
      var selectedItem = mPoses.SelectedItem;
      if (selectedItem == null) return;
      var str = selectedItem.ToString ();
      if (string.IsNullOrWhiteSpace (str)) return;
      var regex = new Regex (@"O:\((?<Org>[0-9-.]+,[0-9-.]+,[0-9-.]+)\), X:<(?<XVec>[0-9-.]+,[0-9-.]+,[0-9-.]+)>, Y:<(?<YVec>[0-9-.]+,[0-9-.]+,[0-9-.]+)>, Z:<(?<ZVec>[0-9-.]+,[0-9-.]+,[0-9-.]+)>");
      foreach (Match match in regex.Matches (str)) {
         var org = ToP (match.Groups["Org"].Value);
         var vecX = ToV (match.Groups["XVec"].Value);
         var vecY = ToV (match.Groups["YVec"].Value);
         ComputeIK (new CoordSystem (org, vecX, vecY));
         break;
      }

      static Point3 ToP (string str) {
         var split = str.Split (',');
         return new Point3 (split[0].ToDouble (), split[1].ToDouble (), split[2].ToDouble ());
      }

      static Vector3 ToV (string str) {
         var split = str.Split (',');
         return new Vector3 (split[0].ToDouble (), split[1].ToDouble (), split[2].ToDouble ());
      }
   }

   ListBox mStances = new () { Margin = new Thickness (8, 0, 8, 4) };
   ListBox mPoses = new () { Margin = new Thickness (8, 0, 8, 4) };
   Dictionary<string, Slider> mSliders = [];
   double mX, mY, mZ, mRx, mRy, mRz;

   void ComputeIK () {
      var cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
      cs *= Matrix3.Translation (new Vector3 (mX, mY, mZ));
      ComputeIK (cs);
   }

   void ComputeIK (CoordSystem cs) {
      mCS = cs;
      mComputingIK = true;
      mStances.Items.Clear ();
      mSolver.ComputeStances (mHome.Org + (Vector3)mCS.Org, -mCS.VecZ.Normalized (), mCS.VecX.Normalized ());
      for (int j = 0; j < 8; j++) {
         var a = mSolver.Solutions[j];
         if (a.OK) mStances.Items.Add ($"Stance {j + 1}");
         else mStances.Items.Add ("----");
         if (j == mSelStance)
            for (int i = 0; i < 6; i++)
               mJoints[i].JValue = a.GetJointAngle (i);
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
   CoordSystem mHome = new (new (0, 0, -400), Vector3.XAxis, Vector3.YAxis), mCS;
   double[] mMin = new double[6], mMax = new double[6];
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
