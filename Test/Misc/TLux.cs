// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TLux.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (16, "Lux rendering tests", "Lux")]
class TLux {
   [Test (48, "Lines, Bezier, Points rendering")]
   void Test1 () {
      var scene = MakeScene (Color4.Black, -10, -10, 110, 110, Draw);
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "LineBezier.png");

      static void Draw () {
         Lux.Color = Color4.White;
         Lux.PointSize = 14f;
         Lux.Lines ([new (0, 0), new (100, 0), new (100, 0), new (70, 100)]);
         Lux.Beziers ([new (0, 0), new (0, 80), new (20, 100), new (70, 100),
                       new (100, 0), new (50, 25), new (25, 50), new (70, 100)]);
         Lux.Points ([new (10, 10), new (30, 10), new (30, 30), new (10, 30)]);
      }
   }

   [Test (49, "Truetype Font rendering")]
   void Test2 () {
      var scene = MakeScene (Color4.Black, -10, -10, 110, 110, Draw);
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "TrueType.png");

      static void Draw () {
         Lux.LineType = ELineType.Dot;
         Lux.LineWidth = 10f;
         var font = new TypeFace (Lib.ReadBytes ("wad:GL/Fonts/Roboto-Regular.ttf"), 28);
         Lux.TypeFace = font;
         Lux.PxText ("Chapter", new (10, 114));
         Lux.Lines ([new (0, 65), new (100, 65)]);
         Lux.TypeFace = TypeFace.Default;
         Lux.PxText ("An example", new (10, 65));
         Lux.PxText ("of TrueType", new (10, 40));
         Lux.PxText ("text.", new (10, 15));
      }
   }

   TestScene MakeScene (Color4 bgrd, double x0, double y0, double x1, double y1, Action draw) {
      var vnode = new SimpleVN (draw);
      return new TestScene (bgrd, new Bound2 (x0, y0, x1, y1), vnode);
   }

   void TestPNG (Scene scene, Vec2S size, DIBitmap.EFormat format, string file) {
      var dib = Lux.RenderToImage (scene, size, format);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual ($"{NT.Data}/Lux/{file}", NT.TmpPNG);
   }

   class TestScene : Scene2 {
      public TestScene (Color4 bgrd, Bound2 bound, VNode root) => (mBgrd, Bound, Root) = (bgrd, bound, root);
      public override Color4 BgrdColor => mBgrd;
      readonly Color4 mBgrd;
   }      
}
