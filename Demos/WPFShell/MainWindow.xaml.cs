using System.Reactive.Linq;
using System.Windows;
using Nori;
namespace WPFShell;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init (); Lux2.Init ();
      InitializeComponent ();
      Content = (UIElement)Lux.CreatePanel ();
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      var source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      new SceneManipulator ();
      Lux.UIScene = new Demo3Scene ();
   }
}

class Demo3Scene : Scene3 {
   public Demo3Scene () {
      Poly poly = Poly.Rectangle (new (0, 0, 100, 50));
      var mesh = Mesh3.Extrude ([poly], 25, Matrix3.Identity);
      Bound = mesh.Bound;
      var svn = new SimpleVN (SetAttr, Draw) { Streaming = true };
      Root = new GroupVN ([new Mesh3VN (mesh), svn]);
   }

   void SetAttr () {
      Lux.TypeFace = face;
   }
   TypeFace face = new TypeFace ("n:/wad/gl/fonts/Roboto-Regular.ttf", 48);

   void Draw () {
      Lux.Text3D ("ABC", new (0, 0, 25), ETextAlign.TopLeft, Vec2S.Zero);
   }
}

class Demo2Scene : Scene2 {
   public Demo2Scene () {
      Bound = new (-50, -50, 150, 100);
      Root = new SimpleVN (Draw) { Streaming = true };
   }

   void Draw () {
      Lux.TypeFace = face;
      Lux.Poly (Poly.Rectangle (0, 0, 100, 50));
      Lux.Color = Color4.Yellow;
      Lux.Text2D ("ABC", new (100, 50), ETextAlign.TopRight, new (0, 0));
   }
   TypeFace face = new TypeFace ("n:/wad/gl/fonts/Roboto-Regular.ttf", 48);
}

class DemoScene : Scene2 {
   public DemoScene () {
      Bound = new Bound2 (0, 0, 100, 50);
      BgrdColor = Color4.Gray (128);
      TraceVN.TextColor = Color4.Yellow; Lib.Tracer = TraceVN.Print;
      Root = new GroupVN([new DemoVN (), TraceVN.It]);
   }
}

class DemoVN : VNode {
   public DemoVN () => Streaming = true;

   public override void SetAttributes () 
      => Lux.Color = Color4.White;

   public override void Draw () {
      byte[] D = Lib.ReadBytesFromZip ("c:/etc/Logo.zip", "Logo.bmp");
      for (int y = 0; y < 128; y++)
         for (int x = 0; x < 128; x++) {
            int n = 150 + y * 512 + x * 4;
            byte b = D[n], g = D[n + 1], r = D[n + 2], a = D[n + 3];
            Lux.Point ((x + 10, 138 - y), new Color4 (a, r, g, b));
         }

      List<Vec2S> pts = [];
      for (int i = 0; i <= 100; i += 10) 
         pts.AddM ((140 + i, 20), (140, 120 - i));
      Lux.Lines (pts.AsSpan ());

      pts.Clear ();
      pts.AddM ((270, 20), (370, 20), (270, 120),
                (280, 120), (320, 120), (370, 30),
                (330, 120), (370, 120), (370, 40));
      Lux.Triangles (pts.AsSpan ());

      pts.Clear ();
      pts.AddM ((400, 20), (500, 20), (500, 65), (400, 65),
                (400, 120), (442, 120), (462, 72), (400, 72),
                (450, 120), (500, 120), (500, 72), (470, 72));
      Lux.Quads (pts.AsSpan ());

      Lux.BorderColor = Color4.Gray (32);
      Lux.Rect (new (20, 150, 120, 250));
      Lux.RRect (new RectS (150, 150, 250, 250), 20);
      Lux.RectBorder (new RectS (280, 150, 380, 250), 10);
      Lux.RRectBorder (new RectS (410, 150, 510, 250), 30, 10);

      Lux.Dee (new (20, 280, 120, 380), 30, 0);
      Lux.Dee (new (150, 280, 250, 380), 30, 1);
      Lux.Dee (new (280, 280, 380, 380), 30, 2);
      Lux.Dee (new (410, 280, 510, 380), 30, 3);

      Lux.RRectBorder (new RectS (20, 400, 300, 550), 40, 50);
   }
}
