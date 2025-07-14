namespace WPFDemo;

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
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
      Redo (true);
   }

   public void CreateUI (UIElementCollection ui) {
      ui.Add (new TextBlock { Text = "Forward", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });
      foreach (var m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness (4) }; ui.Add (sp);
         var label = new TextBlock { Text = m.Name, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Center, Width = 15, VerticalAlignment = VerticalAlignment.Center }; sp.Children.Add (label);
         var slider = new Slider { Minimum = m.JMin, MinWidth=150, Maximum = m.JMax, Value = m.JValue, Margin = new Thickness (4, 1, 4, 4) };
         slider.ValueChanged += (s, e) => { m.JValue = e.NewValue; Redo (true); };
         sp.Children.Add (slider);
      }
      ui.Add (new TextBlock { Text = "Inverse", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness (8, 8, 0, 4) });
   }

   void Redo (bool forward) {
      mGripper.Xfm = mTip.Xfm;
      if (forward) { mCS = CoordSystem.World * mTip.Xfm; mCS = new (mCS.Org + new Vector3 (0, 0, -565), mCS.VecX, mCS.VecY); }
      var stances = mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
      Lib.Trace ("-------------------");
      var sb = new StringBuilder ();
      for (int j = 0; j < 8; j++) {
         for (int i = 0; i < 6; i++) {
            double a = stances[j][i].R2D ();
            if (i == 1) a += 90;
            if (i == 4) a -= 90;
            sb.Append ($"{a.Round (0),6}");
         }
         Lib.Trace (sb); sb.Clear ();
      }
      Lib.Trace ("...");
      foreach (var m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         sb.Append ($"{m.JValue.Round (0),6}");
      }
      Lib.Trace (sb); sb.Clear ();
   }

   public Mechanism Mech => mMech;
   CoordSystem mCS = new (new (1166, 0, 1161 - 565), Vector3.XAxis, Vector3.YAxis);
   RBRSolver mSolver = new (150, 770, 0, 0, 1016, 175);
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
