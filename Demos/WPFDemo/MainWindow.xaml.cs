// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
namespace WPFDemo;

// class MainWindow --------------------------------------------------------------------------------
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Lux.CreatePanel ();

      Lux.UIScene = new LTypeScene ();
   }
}

// class LTypeScene --------------------------------------------------------------------------------
// Demo scene for various line-types
class LTypeScene : Scene2 {
   public LTypeScene () => Bound = new Bound2 (0, 0, 100, 60);

   public override Color4 BgrdColor => Color4.Gray (200);

   public override void Draw () {
      Lux.DrawColor = Color4.Black;
      Lux.LineWidth = 4f;
      Lux.TypeFace = mFace;
      for (var e = ELineType.Continuous; e <= ELineType.Phantom; e++) {
         Lux.LineType = e;
         double y = ((int)e + 1) * 3;
         Lux.Lines ([new (5, y), new (95, y)]);

         var pt = new Point3 (5, y + 0.5, 0) * Xfm;
         double xTxt = (pt.X + 1) / Lux.VPScale.X, yTxt = (pt.Y + 1) / Lux.VPScale.Y;
         Lux.Text (e.ToString (), new Vec2S ((short)xTxt, (short)yTxt));
      }
   }
   TypeFace mFace = new ("C:/Windows/Fonts/segoeui.ttf", 24);
}
