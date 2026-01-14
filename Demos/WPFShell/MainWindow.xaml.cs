using System.Reactive.Linq;
using System.Windows;
using Nori;

namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();  // REMOVETHIS later
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();

      mDwg = new DXFReader ("N:/TData/IO/DXF/D17292.dxf").Load ();
      mDwg.Add (new Layer2 ("Black", Color4.Black, ELineType.Continuous));
      mDwg.CurrentLayer = mDwg.Layers[^1];
      Lux.OnReady.Subscribe (OnLuxReady);
   }
   Dwg2 mDwg;

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      
      Lux.UIScene = new DwgScene1 (mDwg);
   }
}

// Scenario 1:
// - On each click, we add a circle to the drawing.
// - Then, we add a point at the center of that circle
// - Then, we remove that circle
// This would trigger the exception if we did not allow Id=0 VNodes to Deregister
// without any fuss (that is effectively a no-op)
class DwgScene1 : Scene2 {
   public DwgScene1 (Dwg2 dwg) {
      dwg.Ents.Clear ();
      Dwg = dwg;
      Bound = Dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      Root = new Dwg2VN (Dwg);

      mClick = HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (OnClick);
   }
   public readonly Dwg2 Dwg;
   IDisposable mClick;

   public override void Detached () => mClick.Dispose ();

   void OnClick (MouseClickInfo click) {
      Point2 pt = (Point2)Lux.PixelToWorld (click.Position);
      double radius = Dwg.Bound.Diagonal / 200;
      Dwg.Add (Poly.Circle (pt, radius));
      Dwg.Add (pt);
      Dwg.Ents.RemoveAt (Dwg.Ents.Count - 2);   // Remove the circle
   }
}

// Scenario 2:
// - On each click, we add a circle to the drawing
// - There is then a drawing ents watch that:
//   = when a circle is added, adds a point and removes that circle
// - Since we don't want to modify the collection when a Subsribe event is firing,
//   we have to encapsulate these changes inside a Lib.Post()
class DwgScene2 : Scene2 {
   public DwgScene2 (Dwg2 dwg) {
      Dwg = dwg;
      Bound = Dwg.Bound.InflatedF (1.1);
      BgrdColor = Color4.Gray (216);
      Root = new Dwg2VN (Dwg);

      mClick = HW.MouseClicks.Where (a => a.IsLeftPress).Subscribe (OnClick);
      Dwg.Ents.Subscribe (OnEntsChanged);
   }
   public readonly Dwg2 Dwg;
   IDisposable mClick;

   public override void Detached () => mClick.Dispose ();

   void OnClick (MouseClickInfo click) {
      Point2 pt = (Point2)Lux.PixelToWorld (click.Position);
      double radius = Dwg.Bound.Diagonal / 200;
      Dwg.Add (Poly.Circle (pt, radius));
   }

   void OnEntsChanged (ListChange info) {
      switch (info.Action) {
         case ListChange.E.Added:
            if (Dwg.Ents[info.Index] is E2Poly e2p) {
               if (e2p.Poly.IsCircle) {
                  Lib.Post (() => {
                     Dwg.Ents.RemoveAt (info.Index);
                     Dwg.Add (e2p.Poly[0].Center);
                  });
               }
            }
            break;
      }
   }
}
