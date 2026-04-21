// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TLux.cs
// ║║║║╬║╔╣║ Tests of Lux rendering system
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;
using static Lux;

[Fixture (16, "Lux rendering tests", "Lux")]
class TLux {
   TLux () {
      var poly = Nori.Poly.Rectangle (0, 0, 10, 10);
      mCube = Mesh3.Extrude ([poly], 6, Matrix3.Identity, ETess.Medium);
   }

   [Test (48, "Lines, Bezier, Points rendering")]
   void Test1 () {
      var scene = new Scene2 (Color4.Black, new (-10, -10, 110, 110), new SimpleVN (Draw));
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "LineBezier");

      static void Draw () {
         (Color, PointSize, LineWidth) = (Color4.White, 14, 4);
         Lines ([new Vec2F (0, 0), new (100, 0), new (100, 0), new (70, 100)]);
         Beziers ([new (0, 0), new (0, 80), new (20, 100), new (70, 100),
                       new (100, 0), new (50, 25), new (25, 50), new (70, 100)]);
         Points ([new Vec2F (10, 10), new (30, 10), new (30, 30), new (10, 30)]);
      }
   }

   [Test (49, "Truetype Font rendering")]
   void Test2 () {
      var scene = new Scene2 (Color4.Black, new (-10, -10, 110, 110), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (160, 160), DIBitmap.EFormat.Gray8, "TrueType");

      void Draw () {
         (LTScale, LineType, LineWidth, TypeFace) = (100, ELineType.Dot, 10, mFace);
         Text ("Chapter", new (10, 46));
         Lines ([new Vec2F (0, 65), new (100, 65)]);
         TypeFace = mFace2;
         Text ("An example", new (10, 95));
         Text ("of TrueType", new (10, 120));
         Color = Color4.Yellow;
         Text ("text.", new (10, 145));
      }
   }

   [Test (51, "Text2D alignment points test")]
   void Test3 () {
      var gvn = new GroupVN ([
         new SimpleVN (SetAttributes1, Draw1),
         new SimpleVN (SetAttributes2, Draw2)
      ]);
      var scene = new Scene2 (Color4.Gray (240), new (8, 3, 38, 22), gvn);
      TestPNG (scene, new (420, 280), DIBitmap.EFormat.Gray8, "Text2D");

      static void SetAttributes1 () { Color = Color4.Gray (160); LineWidth = 4; ZLevel = -1;  }
      static void Draw1 () => Lines ([new Vec2F (10, 5), new (36, 5), new (10, 10), new (36, 10),
         new (10, 15), new (36, 15), new (10, 20), new (36, 20), new (10, 5), new (10, 20),
         new (23, 5), new (23, 20), new (36, 5), new (36, 20)]);

      void SetAttributes2 () { Color = Color4.Black; TypeFace = mFace; }
      static void Draw2 () {
         Text2D ("BsL{}", new (10, 10), ETextAlign.BaseLeft, Vec2S.Zero);
         Text2D ("BsC{}", new (23, 10), ETextAlign.BaseCenter, Vec2S.Zero);
         Text2D ("BsR{}", new (36, 10), ETextAlign.BaseRight, Vec2S.Zero);
         Text2D ("MdL{}", new (10, 15), ETextAlign.MidLeft, Vec2S.Zero);
         Text2D ("MdC{}", new (23, 15), ETextAlign.MidCenter, Vec2S.Zero);
         Text2D ("MdR{}", new (36, 15), ETextAlign.MidRight, Vec2S.Zero);
         Text2D ("TpL{}", new (10, 20), ETextAlign.TopLeft, Vec2S.Zero);
         Text2D ("TpC{}", new (23, 20), ETextAlign.TopCenter, Vec2S.Zero);
         Text2D ("TpR{}", new (36, 20), ETextAlign.TopRight, Vec2S.Zero);
         Text2D ("BtL{}", new (10, 5), ETextAlign.BotLeft, Vec2S.Zero);
         Text2D ("BtC{}", new (23, 5), ETextAlign.BotCenter, Vec2S.Zero);
         Text2D ("BtR{}", new (36, 5), ETextAlign.BotRight, Vec2S.Zero);
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
      TestPNG (scene, new (264, 144), DIBitmap.EFormat.Gray8, "ZLevel");

      void SetAttributes1 () { Color = Color4.Gray (160); LineWidth = 4; }
      void Draw1 () => Quads ([new Vec2F (20, 0), new (40, 0), new (20, 20), new (0, 20)]);

      void SetAttributes2 () { Color = Color4.Black; TypeFace = mFace; ZLevel = -1; }
      void Draw2 () => Text2D ("Hello, World!", new (0, 5), ETextAlign.MidLeft, Vec2S.Zero);

      void SetAttributes3 () { Color = Color4.Black; TypeFace = mFace; ZLevel = +1; }
      void Draw3 () => Text2D ("Hello, World!", new (0, 15), ETextAlign.MidLeft, Vec2S.Zero);
   }

   [Test (216, "ZLevel with Streaming")]
   void Test5 () {
      var scene = new Scene2 (Color4.Red, new (-8.5, -8.5, 203.5, 203.5), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (212, 212), DIBitmap.EFormat.RGB8, "ZLevel2");

      static void Draw () {
         Rand r = new (42);
         int[] iter = new int[25];
         for (int i = 0; i < 25; i++) iter[i] = i;
         for (int i = 24; i >= 0; i--) {
            int j = r.Next (i + 1);
            (iter[i], iter[j]) = (iter[j], iter[i]);
         }
         foreach (var n in iter) {
            (ZLevel, Color) = (n, Color4.Gray (n * 10));
            int x = n * 4, y = x;
            Quads ([new Vec2F (x, y), new (x + 98, y), new (x + 98, y + 98), new (x, y + 98)]);
         }
      }
   }

   [Test (217, "Dash-line shader")]
   void Test6 () {
      var scene = new Scene2 (Color4.Gray (48), new (0, 0, 160, 70), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (160, 70), DIBitmap.EFormat.RGB8, "DashLine");

      static void Draw () {
         (LineType, LineWidth, Color) = (ELineType.Center, 2, Color4.White);
         Lines ([new Vec2F (10, 10), new (150, 10)]);
         (LineType, LineWidth, Color) = (ELineType.Dash2, 4, Color4.Yellow);
         Lines ([new Vec2F (10, 20), new (150, 20)]);
         (LineType, LineWidth, Color) = (ELineType.Dot, 6, Color4.Red);
         Lines ([new Vec2F (10, 32), new (150, 32)]);
         (LineType, LineWidth, Color) = (ELineType.DashDot, 8, Color4.Green);
         Lines ([new Vec2F (10, 46), new (150, 46)]);
         LineWidth = 3f;
         Lines ([new Vec2F (10, 60), new (150, 60)]);
      }
   }

   [Test (218, "Mesh3 rendering")]
   void Test7 () {
      var m1 = mCube;
      var bound = new Bound3 (0, -8, 0, 18, 10, 14);
      VNode[] nodes = [new SimpleVN (Attr1, Draw1), new SimpleVN (Attr2, Draw2),
                       new SimpleVN (Attr3, Draw3), new SimpleVN (Attr4, Draw4),
                       new SimpleVN (Attr5, Draw5)];
      var scene = new Scene3 (Color4.Gray (192), bound, new GroupVN (nodes));
      scene.Zoom (1.4);
      TestPNG (scene, new (320, 240), DIBitmap.EFormat.RGB8, "Mesh3");

      void Attr1 () => (Color, Xfm) = (Color4.White, Matrix3.Translation (0, 0, 0));
      void Draw1 () => Mesh (m1, EShadeMode.Flat);
      void Attr2 () => (Color, Xfm) = (Color4.Yellow, Matrix3.Translation (2, -2, 2));
      void Draw2 () => Mesh (m1, EShadeMode.Glass);
      void Attr3 () => (Color, Xfm) = (Color4.Red, Matrix3.Translation (4, -4, 4));
      void Draw3 () => Mesh (m1, EShadeMode.Gourad);
      void Attr4 () => (Color, Xfm) = (Color4.Green, Matrix3.Translation (6, -6, 6));
      void Draw4 () => Mesh (m1, EShadeMode.PhongNoStencil);
      void Attr5 () => (Color, Xfm) = (Color4.Cyan, Matrix3.Translation (8, -8, 8));
      void Draw5 () => Mesh (m1, EShadeMode.GlassNoStencil);
   }
   Mesh3 mCube;

   [Test (219, "Picking")]
   void Test8 () {
      var scene = new Scene3 (Color4.Gray (128), mCube.Bound, new Mesh3VN (mCube) { Color = Color4.White });
      UIScene = scene;
      var vn1 = Pick (new (1, 1)); (vn1 == null).IsTrue ();
      var vn2 = Pick (new (PanelSize.X / 2, PanelSize.Y / 2)); (vn2 is Mesh3VN).IsTrue ();
   }

   [Test (220, "Line3D render")]
   void Test9 () {
      var scene = new Scene3 (Color4.Black, new (0, 0, -1, 10, 8, 1), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (160, 120), DIBitmap.EFormat.RGB8, "Line3D");

      static void Draw () {
         (LineWidth, Color) = (6f, Color4.Green);
         Lines ([new Vec3F (0, 0, 0), new (10, 0, 0)]);
         (LineWidth, Color) = (4f, Color4.Yellow);
         Lines ([new Vec3F (0, 4, 0), new (10, 4, 0), new (0, 4, 0), new (0, 1, 0)]);
         Color = Color4.Cyan;
         Lines ([new Vec3F (0, 8, 0), new (10, 8, 0), new (10, 8, 0), new (10, 5, 0)]);
      }
   }

   [Test (221, "Fill Poly")]
   void Test10 () {
      Dwg2 dwg = new ();
      dwg.Add (Nori.Poly.Parse ("M0,0H100V25Q75,50,1H0Z"));
      dwg.Add (Nori.Poly.Circle (new (75, 25), 20));
      var scene = new Scene2 (Color4.Gray (128), new (-5, -5, 105, 55), new DwgFillVN (dwg, ETess.Medium));
      TestPNG (scene, new Vec2S (240, 120), DIBitmap.EFormat.Gray8, "FillPoly");
   }

   [Test (222, "Points, Triangles, Text in 2D")]
   void Test11 () {
      var scene = new Scene2 (Color4.Black, new (0, 0, 100, 50), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (200, 100), DIBitmap.EFormat.Gray8, "PtsTris");

      void Draw () {
         Color = Color4.Gray (128); 
         List<Vec2F> pts = [new (5, 5), new (90, 5), new (90, 15)];
         Triangles (pts.AsSpan ());
         Color = Color4.Gray (158);
         pts = [new (5, 10), new (90, 20), new (5, 20)];
         Triangles (pts.AsSpan ());

         (Color, PointSize) = (Color4.Gray (188), 4f);
         pts = [new (5, 30), new (20, 30), new (35, 30)];
         Points (pts.AsSpan ());
         (Color, PointSize) = (Color4.Gray (218), 8f);
         pts = [new (10, 40), new (25, 40), new (40, 40)];
         Points (pts.AsSpan ());
         Color = Color4.Gray (248);
         pts = [new (15, 45), new (30, 45)];
         Points (pts.AsSpan ());

         TypeFace = mFace2;
         Text2D ("ABC", new (50, 25), ETextAlign.BaseLeft, Vec2S.Zero);
         Color = Color4.Gray (128);
         Text2D ("def", new (50, 37), ETextAlign.BaseLeft, Vec2S.Zero);
         Xfm = Matrix3.Translation (22, 12, 0);
         Text2D ("123", new (50, 25), ETextAlign.BaseLeft, Vec2S.Zero);
      }
   }

   [Test (223, "Points in 3D")]
   void Test12 () {
      var scene = new Scene3 (Color4.Gray (64), new (0, 0, 0, 10, 20, 1), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, new (160, 100), DIBitmap.EFormat.RGB8, "Pts3D");

      void Draw () {
         (Color, PointSize) = (Color4.Gray (192), 4f);
         List<Vec3F> pts = [new (2, 2, 2), new (5, 2, 2), new (8, 2, 2)];
         Points (pts.AsSpan ());
         (Color, PointSize) = (Color4.Yellow, 7f);
         pts = [new (2, 5, 2), new (5, 5, 2), new (8, 5, 2)];
         Points (pts.AsSpan ());
         (Color, PointSize) = (Color4.Red, 10f);
         pts = [new (2, 8, 2), new (5, 8, 2), new (8, 8, 2)];
         Points (pts.AsSpan ());

         (Color, TypeFace) = (Color4.Yellow, mFace2);
         Text3D ("ABC", new (5, 11, 2), ETextAlign.BaseCenter, Vec2S.Zero);
         Color = Color4.Cyan;
         Text3D ("012", new (5, 16, 2), ETextAlign.BaseCenter, Vec2S.Zero);
      }
   }

   [Test (241, "Various Px shaders")]
   void Test13 () {
      var scene = new Scene2 (Color4.Gray (128), new (0, 0, 528, 400), new SimpleVN (Draw) { Streaming = true });
      TestPNG (scene, (528, 400), DIBitmap.EFormat.Gray8, "PxShader");

      static void Draw () {
         (Color, BorderColor) = (Color4.Gray (224), Color4.Gray (32));
         byte[] D = Lib.ReadBytesFromZip (NT.File ("Lux/Logo.zip"), "Logo.bmp");
         for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++) {
               int n = 150 + y * 512 + x * 4;
               byte b = D[n], g = D[n + 1], r = D[n + 2], a = D[n + 3];
               Point ((x + 10, 138 - y), new Color4 (a, r, g, b));
            }

         List<Vec2S> pts = [];
         for (int i = 0; i <= 100; i += 10)
            pts.AddM ((140 + i, 20), (140, 120 - i));
         Lines (pts.AsSpan ());

         pts.Clear ();
         pts.AddM ((270, 20), (370, 20), (270, 120),
                   (280, 120), (320, 120), (370, 30),
                   (330, 120), (370, 120), (370, 40));
         Triangles (pts.AsSpan ());

         pts.Clear ();
         pts.AddM ((400, 20), (500, 20), (500, 65), (400, 65),
                   (400, 120), (442, 120), (462, 72), (400, 72),
                   (450, 120), (500, 120), (500, 72), (470, 72));
         Quads (pts.AsSpan ());

         Rect (new (20, 150, 120, 250));
         RRect (new RectS (150, 150, 250, 250), 20);
         RectBorder (new RectS (280, 150, 380, 250), 10);
         RRectBorder (new RectS (410, 150, 510, 250), 30, 10);

         Dee (new (20, 280, 120, 380), 30, 0);
         Dee (new (150, 280, 250, 380), 30, 1);
         Dee (new (280, 280, 380, 380), 30, 2);
         Dee (new (410, 280, 510, 380), 30, 3);
      }
   }

   void TestPNG (Scene scene, Vec2S size, DIBitmap.EFormat format, string file) {
      var dib = scene.RenderImage (size, format);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual ($"{NT.Data}/Lux/{file}.png", NT.TmpPNG, dib);
   }

   TypeFace mFace = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), 28);
   TypeFace mFace2 = new (Lib.ReadBytes ("nori:GL/Fonts/Roboto-Regular.ttf"), 18);
}
