// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3ThickDemo
// ║║║║╬║╔╣║ Demonstrates creating E3Flat, E3Flex and adjusting flexes with a BendPose
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
using System.Windows;
using Nori;
namespace WPFDemo;

class E3ThickDemo : Scene3, ISceneWithUI {
   public E3ThickDemo () {
      mModel = MakeModel ();
      mPose = new BendPose (mModel);

      Bound = mPose.GetBound (1) + mPose.GetBound (0);
      BgrdColor = Color4.Gray (208);
      Root = new GroupVN (mPose.Nodes.Select (a => new BPoseNodeVN (a)));
   }
   Model3 mModel;
   BendPose mPose;

   public void CreateUI (UIElementCollection panel) {
      foreach (var node in mPose.Nodes.Where (a => a.IsFlex)) {
         panel.Add (new Label { Content = $"Flex #{node.Id}", Margin = new Thickness (6, 0, 6, 0) });
         var s = new Slider { Minimum = 0, Maximum = 1, Value = node.Lie, Tag = node.Id, Margin = new Thickness (6, 0, 6, 2), Width = 100 };
         s.ValueChanged += OnSliderChanged;
         panel.Add (s);
      }
   }

   void OnSliderChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
      int id = (int)((Slider)sender).Tag;
      mPose.SetFlexLie (id, e.NewValue);
   }

   // Makes a sheet-metal model
   Model3 MakeModel () {
      Model3 model = new ();
      E3Flat p1, p3, p5, p7; E3Flex f2, f4, f6;

      var cs1 = CoordSystem.World;
      model.Ents.Add (p1 = new E3Flat (1, cs1, 4, [Poly.Rectangle (-100, -100, 100, 100), Poly.Rectangle (-40, -40, 40, 40)]));

      var cs2 = new CoordSystem (new (100, 0, 0), -Vector3.YAxis, Vector3.XAxis);
      var spine = new BSpine (8, 1.25 * Lib.HalfPI, 0.5, true);
      model.Ents.Add (f2 = new E3Flex (2, cs2, 4, spine, [Poly.Rectangle (-100, 0, 100, spine.FlatWidth)]));

      var cs3 = f2.GetTailCS (1);
      model.Ents.Add (p3 = new E3Flat (3, cs3, 4, [Poly.Rectangle (-100, 0, 100, 20)]));

      var cs4 = new CoordSystem (cs3.Org + cs3.VecY * 20, cs3.VecX, cs3.VecY);
      var spine2 = new BSpine (24, Lib.HalfPI, 0.5, false);
      model.Ents.Add (f4 = new E3Flex (4, cs4, 4, spine2, [Poly.Rectangle (-100, 0, 100, spine2.FlatWidth), Poly.Rectangle (-90, 10, -60, 27)]));

      var cs5 = f4.GetTailCS (1);
      model.Ents.Add (p5 = new E3Flat (5, cs5, 4, [Poly.Rectangle (-100, 0, 100, 20)]));

      var cs6 = new CoordSystem (new (0, -40, 0));
      var spine3 = new BSpine (8, 0.8 * Lib.HalfPI, 0.5, true);
      model.Ents.Add (f6 = new E3Flex (6, cs6, 4, spine3, [Poly.Rectangle (-35, 0, 35, spine3.FlatWidth)]));

      var cs7 = f6.GetTailCS (1);
      model.Ents.Add (p7 = new E3Flat (7, cs7, 4, [Poly.Rectangle (-35, 0, 35, 62), Poly.Rectangle (-25, 10, 0, 25)]));
      f2.Parent = p1; p3.Parent = f2; f4.Parent = p3; p5.Parent = f4; f6.Parent = p1; p7.Parent = f6;

      return model;
   }
}
