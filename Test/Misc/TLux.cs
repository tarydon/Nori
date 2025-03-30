// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TLux.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (16, "Lux rendering tests", "Lux")]
class TLux {
   [Test (48, "Lines, Bezier rendering")]
   void Test1 () {
      var scene = MakeScene (Color4.Black, Color4.White, -10, -10, 110, 110, Draw);
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "LineBezier.png");

      static void Draw () {
         Lux.Lines ([new (0, 0), new (100, 0), new (100, 0), new (70, 100)]);
         Lux.Beziers ([new (0, 0), new (0, 80), new (20, 100), new (70, 100),
                       new (100, 0), new (50, 25), new (25, 50), new (70, 100)]);
      }
   }

   Scene MakeScene (Color4 bgrd, Color4 fgrd, double x0, double y0, double x1, double y1, Action draw) {
      var vnode = new SimpleVN (fgrd, draw);
      return new SimpleScene (bgrd, new Bound2 (x0, y0, x1, y1), vnode);
   }

   void TestPNG (Scene scene, Vec2S size, DIBitmap.EFormat format, string file) {
      var dib = Lux.RenderToImage (scene, size, format);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual ($"{NT.Data}/Misc/{file}", NT.TmpPNG);
   }

   class SimpleScene : Scene2 {
      public SimpleScene (Color4 bgrd, Bound2 bound, VNode root) {
         mBgrd = bgrd; Bound = bound; Root = root;
      }

      public override Color4 BgrdColor => mBgrd;
      readonly Color4 mBgrd;
   }
      
   class SimpleVN (Color4 color, Action draw) : VNode (draw) {
      public override void SetAttributes () => Lux.Color = color;
      public override void Draw () => draw ();
   }
}
