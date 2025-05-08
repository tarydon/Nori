// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TLux.cs
// ║║║║╬║╔╣║ Tests of Lux rendering system
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (16, "Lux rendering tests", "Lux")]
class TLux {
   [Test (48, "Lines, Bezier, Points rendering")]
   void Test1 () {
      var scene = new Scene2 (Color4.Black, new (-10, -10, 110, 110), new SimpleVN (Draw));
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
      var scene = new Scene2 (Color4.Black, new (-10, -10, 110, 110), new SimpleVN (Draw));
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "TrueType.png");

      void Draw () {
         Lux.LineType = ELineType.Dot;
         Lux.LineWidth = 10f;
         Lux.TypeFace = mFace;
         Lux.TextPx ("Chapter", new (10, 114));
         Lux.Lines ([new (0, 65), new (100, 65)]);
         Lux.TypeFace = TypeFace.Default;
         Lux.TextPx ("An example", new (10, 65));
         Lux.TextPx ("of TrueType", new (10, 40));
         Lux.TextPx ("text.", new (10, 15));
      }
   }

   [Test (51, "Text2D alignment points test")]
   void Test3 () {
      var gvn = new GroupVN ([
         new SimpleVN (SetAttributes1, Draw1),
         new SimpleVN (SetAttributes2, Draw2)
      ]);
      var scene = new Scene2 (Color4.Gray (240), new (8, 3, 38, 22), gvn);
      TestPNG (scene, new (420, 280), DIBitmap.EFormat.Gray8, "Text2D.png");

      static void SetAttributes1 () { Lux.Color = Color4.Gray (160); Lux.ZLevel = -1;  }
      static void Draw1 () => Lux.Lines ([new (10, 5), new (36, 5), new (10, 10), new (36, 10),
         new (10, 15), new (36, 15), new (10, 20), new (36, 20), new (10, 5), new (10, 20),
         new (23, 5), new (23, 20), new (36, 5), new (36, 20)]);

      void SetAttributes2 () { Lux.Color = Color4.Black; Lux.TypeFace = mFace; }
      static void Draw2 () {
         Lux.Text2D ("BsL{}", new (10, 10), ETextAlign.BaseLeft);
         Lux.Text2D ("BsC{}", new (23, 10), ETextAlign.BaseCenter);
         Lux.Text2D ("BsR{}", new (36, 10), ETextAlign.BaseRight);
         Lux.Text2D ("MdL{}", new (10, 15), ETextAlign.MidLeft);
         Lux.Text2D ("MdC{}", new (23, 15), ETextAlign.MidCenter);
         Lux.Text2D ("MdR{}", new (36, 15), ETextAlign.MidRight);
         Lux.Text2D ("TpL{}", new (10, 20), ETextAlign.TopLeft);
         Lux.Text2D ("TpC{}", new (23, 20), ETextAlign.TopCenter);
         Lux.Text2D ("TpR{}", new (36, 20), ETextAlign.TopRight);
         Lux.Text2D ("BtL{}", new (10, 5), ETextAlign.BotLeft);
         Lux.Text2D ("BtC{}", new (23, 5), ETextAlign.BotCenter);
         Lux.Text2D ("BtR{}", new (36, 5), ETextAlign.BotRight);
      }
   }

   [Test (52, "ZLevel test")]
   void Test4 () {
      var gvn = new GroupVN ([
         new SimpleVN (SetAttributes1, Draw1),
         new SimpleVN (SetAttributes2, Draw2),
         new SimpleVN (SetAttributes3, Draw3)
      ]);
      var scene = new Scene2 (Color4.Gray (240), new (-2, -2, 42, 22), gvn);
      TestPNG (scene, new (264, 144), DIBitmap.EFormat.Gray8, "ZLevel.png");

      void SetAttributes1 () => Lux.Color = Color4.Gray (160);
      void Draw1 () => Lux.Quads ([new (20, 0), new (40, 0), new (20, 20), new (0, 20)]);

      void SetAttributes2 () { Lux.Color = Color4.Black; Lux.TypeFace = mFace; Lux.ZLevel = -1; }
      void Draw2 () => Lux.Text2D ("Hello, World!", new (0, 5), ETextAlign.MidLeft);

      void SetAttributes3 () { Lux.Color = Color4.Black; Lux.TypeFace = mFace; Lux.ZLevel = +1; }
      void Draw3 () => Lux.Text2D ("Hello, World!", new (0, 15), ETextAlign.MidLeft);
   }


   void TestPNG (Scene scene, Vec2S size, DIBitmap.EFormat format, string file) {
      var dib = Lux.RenderToImage (scene, size, format);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual ($"{NT.Data}/Lux/{file}", NT.TmpPNG);
   }

   TypeFace mFace = new (Lib.ReadBytes ("wad:GL/Fonts/Roboto-Regular.ttf"), 28);
}
