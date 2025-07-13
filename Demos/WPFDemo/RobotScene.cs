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
      var gripper = mGripper = new XfmVN (Matrix3.Identity, new MeshVN (CMesh.Load ("N:/Wad/FanucX/65x65-4-30.mesh")));
      Root = new GroupVN ([robot, gripper]);
      mGripper.Xfm = mTip.Xfm;
   }

   public void CreateUI (UIElementCollection ui) {
      foreach (var m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness (4) }; ui.Add (sp);
         var label = new TextBlock { Text = m.Name, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Center, Width = 15, VerticalAlignment = VerticalAlignment.Center }; sp.Children.Add (label);
         var slider = new Slider { Minimum = m.JMin, MinWidth=150, Maximum = m.JMax, Value = m.JValue, Margin = new Thickness (4, 1, 4, 4) };
         slider.ValueChanged += (s, e) => { m.JValue = e.NewValue; Redo (); };
         sp.Children.Add (slider);
      }
   }

   void Redo () {
      mGripper.Xfm = mTip.Xfm;
   }

   public Mechanism Mech => mMech;
   Mechanism mMech, mTip;
   XfmVN mGripper;
}
